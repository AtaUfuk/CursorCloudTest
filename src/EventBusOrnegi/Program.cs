// --- Olay (Event) tanımı ---
public record OrderCreatedEvent(int OrderId, string ProductName, int Quantity);

// --- Event Bus: olayları yayınlayan ve dinleyicileri yöneten yapı ---
public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        var eventType = typeof(TEvent);
        if (!_handlers.ContainsKey(eventType))
            _handlers[eventType] = new List<Delegate>();

        _handlers[eventType].Add(handler);
    }

    public void Publish<TEvent>(TEvent @event)
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        foreach (var handler in handlers)
        {
            ((Action<TEvent>)handler)(@event);
        }
    }
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

// --- Sipariş Servisi: olayı yayınlayan taraf ---
public class OrderService
{
    private readonly EventBus _eventBus;

    public OrderService(EventBus eventBus) => _eventBus = eventBus;

    public void CreateOrder(int orderId, string productName, int quantity)
    {
        Console.WriteLine($"Sipariş #{orderId} oluşturuldu: {productName} x{quantity}");
        _eventBus.Publish(new OrderCreatedEvent(orderId, productName, quantity));
    }
}

// --- Program: her şeyi bir araya getiren giriş noktası ---
public class Program
{
    public static void Main()
    {
        var eventBus = new EventBus();

        var emailHandler = new EmailNotificationHandler();
        var stockHandler = new StockUpdateHandler();

        eventBus.Subscribe<OrderCreatedEvent>(emailHandler.Handle);
        eventBus.Subscribe<OrderCreatedEvent>(stockHandler.Handle);

        var orderService = new OrderService(eventBus);

        orderService.CreateOrder(1, "Laptop", 2);
        Console.WriteLine("---");
        orderService.CreateOrder(2, "Klavye", 5);
    }
}
