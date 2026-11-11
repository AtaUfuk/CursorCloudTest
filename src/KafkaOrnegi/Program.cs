using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

// --- Olay (Event) tanımı ---
public record OrderCreatedEvent(int OrderId, string ProductName, int Quantity);

public static class KafkaSettings
{
    public const string BootstrapServers = "localhost:9092";
    public const string Topic = "order-created";
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

// --- Sipariş Servisi: olayı Kafka topic'ine yazan taraf ---
public class OrderService
{
    private readonly IProducer<string, string> _producer;

    public OrderService(IProducer<string, string> producer) => _producer = producer;

    public async Task CreateOrderAsync(int orderId, string productName, int quantity)
    {
        Console.WriteLine($"Sipariş #{orderId} oluşturuldu: {productName} x{quantity}");

        // Aynı key'e sahip mesajlar aynı partition'a gider, böylece bir siparişin olayları sırasıyla işlenir.
        await _producer.ProduceAsync(KafkaSettings.Topic, new Message<string, string>
        {
            Key = orderId.ToString(),
            Value = JsonSerializer.Serialize(new OrderCreatedEvent(orderId, productName, quantity))
        });
    }
}

public class Program
{
    public static async Task Main()
    {
        await EnsureTopicExistsAsync();

        using var cts = new CancellationTokenSource();

        // Her dinleyici farklı bir consumer group kullanır; böylece her biri tüm mesajları ayrı ayrı alır.
        // Gerçek bir sistemde bu dinleyiciler genellikle ayrı uygulamalarda (servislerde) çalışır.
        var emailTask = Task.Run(() =>
            Consume("email-service", new EmailNotificationHandler().Handle, cts.Token));
        var stockTask = Task.Run(() =>
            Consume("stock-service", new StockUpdateHandler().Handle, cts.Token));

        var producerConfig = new ProducerConfig { BootstrapServers = KafkaSettings.BootstrapServers };
        using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
        {
            var orderService = new OrderService(producer);

            await orderService.CreateOrderAsync(1, "Laptop", 2);
            await orderService.CreateOrderAsync(2, "Klavye", 5);
        }

        Console.WriteLine("Mesajlar Kafka üzerinden dinleyicilere iletiliyor. Çıkmak için Enter'a basın.");
        Console.ReadLine();

        cts.Cancel();
        await Task.WhenAll(emailTask, stockTask);
    }

    private static void Consume(string groupId, Action<OrderCreatedEvent> handler, CancellationToken token)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = KafkaSettings.BootstrapServers,
            GroupId = groupId,
            // Consumer group ilk kez bağlandığında topic'in en başından okumaya başlar.
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaSettings.Topic);

        try
        {
            while (!token.IsCancellationRequested)
            {
                var result = consumer.Consume(token);
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value)!;
                handler(@event);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private static async Task EnsureTopicExistsAsync()
    {
        var config = new AdminClientConfig { BootstrapServers = KafkaSettings.BootstrapServers };
        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = KafkaSettings.Topic, NumPartitions = 1, ReplicationFactor = 1 }
            });
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }
}
