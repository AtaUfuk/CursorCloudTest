using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

// --- Olay (Event) tanımı ---
public record OrderCreatedEvent(int OrderId, string ProductName, int Quantity);

public static class Topology
{
    // Fanout exchange, gelen her mesajı kendisine bağlı tüm kuyruklara kopyalar.
    public const string ExchangeName = "order-created";
    public const string EmailQueue = "order-created.email";
    public const string StockQueue = "order-created.stock";
}

// --- Handler'lar (Olay Dinleyicileri) ---
public class EmailNotificationHandler
{
    public void Handle(OrderCreatedEvent e)
    {
        Console.WriteLine($"[E-posta] Sipariş #{e.OrderId} için onay maili gönderildi. " +
                          $"Ürün: {e.ProductName}, Adet: {e.Quantity}");
    }
}

public class StockUpdateHandler
{
    public void Handle(OrderCreatedEvent e)
    {
        Console.WriteLine($"[Stok] Ürün: {e.ProductName}, stoktan {e.Quantity} adet düşüldü.");
    }
}

// --- Sipariş Servisi: olayı RabbitMQ'ya yayınlayan taraf ---
public class OrderService
{
    private readonly IChannel _channel;

    public OrderService(IChannel channel) => _channel = channel;

    public async Task CreateOrderAsync(int orderId, string productName, int quantity)
    {
        Console.WriteLine($"Sipariş #{orderId} oluşturuldu: {productName} x{quantity}");

        var body = JsonSerializer.SerializeToUtf8Bytes(new OrderCreatedEvent(orderId, productName, quantity));
        await _channel.BasicPublishAsync(exchange: Topology.ExchangeName, routingKey: string.Empty, body: body);
    }
}

public class Program
{
    public static async Task Main()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();

        await using var publisherChannel = await connection.CreateChannelAsync();
        await publisherChannel.ExchangeDeclareAsync(Topology.ExchangeName, ExchangeType.Fanout);

        // Gerçek bir sistemde bu dinleyiciler genellikle ayrı uygulamalarda (servislerde) çalışır.
        await using var emailChannel = await SubscribeAsync(
            connection, Topology.EmailQueue, new EmailNotificationHandler().Handle);
        await using var stockChannel = await SubscribeAsync(
            connection, Topology.StockQueue, new StockUpdateHandler().Handle);

        var orderService = new OrderService(publisherChannel);

        await orderService.CreateOrderAsync(1, "Laptop", 2);
        await orderService.CreateOrderAsync(2, "Klavye", 5);

        Console.WriteLine("Mesajlar RabbitMQ üzerinden dinleyicilere iletiliyor. Çıkmak için Enter'a basın.");
        Console.ReadLine();
    }

    private static async Task<IChannel> SubscribeAsync(
        IConnection connection, string queueName, Action<OrderCreatedEvent> handler)
    {
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(Topology.ExchangeName, ExchangeType.Fanout);
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueName, Topology.ExchangeName, routingKey: string.Empty);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(ea.Body.Span)!;
            handler(@event);

            // Mesaj ancak başarıyla işlendikten sonra onaylanır; onaylanmazsa RabbitMQ tekrar teslim eder.
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer);
        return channel;
    }
}
