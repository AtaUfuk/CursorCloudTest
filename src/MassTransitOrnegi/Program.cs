using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MassTransitOrnegi;

// --- Olay (Event) tanımı ---
public record OrderCreatedEvent(int OrderId, string ProductName, int Quantity);

// --- Consumer'lar (Olay Dinleyicileri) ---
public class EmailNotificationConsumer : IConsumer<OrderCreatedEvent>
{
    public Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var e = context.Message;
        Console.WriteLine($"[E-posta] Sipariş #{e.OrderId} için onay maili gönderildi. " +
                          $"Ürün: {e.ProductName}, Adet: {e.Quantity}");
        return Task.CompletedTask;
    }
}

public class StockUpdateConsumer : IConsumer<OrderCreatedEvent>
{
    public Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var e = context.Message;
        Console.WriteLine($"[Stok] Ürün: {e.ProductName}, stoktan {e.Quantity} adet düşüldü.");
        return Task.CompletedTask;
    }
}

// --- Sipariş Servisi: olayı yayınlayan taraf ---
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public OrderService(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public async Task CreateOrderAsync(int orderId, string productName, int quantity)
    {
        Console.WriteLine($"Sipariş #{orderId} oluşturuldu: {productName} x{quantity}");
        await _publishEndpoint.Publish(new OrderCreatedEvent(orderId, productName, quantity));
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<EmailNotificationConsumer>();
            x.AddConsumer<StockUpdateConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("localhost", "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                // Her consumer için exchange, kuyruk ve binding'leri otomatik oluşturur.
                cfg.ConfigureEndpoints(context);
            });
        });

        builder.Services.AddOptions<MassTransitHostOptions>()
            .Configure(options => options.WaitUntilStarted = true);

        builder.Services.AddScoped<OrderService>();

        using var host = builder.Build();
        await host.StartAsync();

        using (var scope = host.Services.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();

            await orderService.CreateOrderAsync(1, "Laptop", 2);
            await orderService.CreateOrderAsync(2, "Klavye", 5);
        }

        Console.WriteLine("Mesajlar MassTransit (RabbitMQ) üzerinden consumer'lara iletiliyor. Çıkmak için Enter'a basın.");
        Console.ReadLine();

        await host.StopAsync();
    }
}
