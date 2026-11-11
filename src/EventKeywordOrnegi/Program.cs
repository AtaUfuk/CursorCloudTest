// --- Olay verisi: EventArgs'tan türetilir ---
public class OrderCreatedEventArgs : EventArgs
{
    public int OrderId { get; }
    public string ProductName { get; }
    public int Quantity { get; }

    public OrderCreatedEventArgs(int orderId, string productName, int quantity)
    {
        OrderId = orderId;
        ProductName = productName;
        Quantity = quantity;
    }
}

// --- Yayıncı (Publisher) ---
public class OrderService
{
    public event EventHandler<OrderCreatedEventArgs>? OrderCreated;

    public void CreateOrder(int orderId, string productName, int quantity)
    {
        Console.WriteLine($"Sipariş #{orderId} oluşturuldu: {productName} x{quantity}");
        OnOrderCreated(new OrderCreatedEventArgs(orderId, productName, quantity));
    }

    protected virtual void OnOrderCreated(OrderCreatedEventArgs e)
    {
        OrderCreated?.Invoke(this, e);
    }
}

// --- Dinleyiciler (Subscribers) ---
public class EmailNotificationHandler
{
    public void Handle(object? sender, OrderCreatedEventArgs e)
    {
        Console.WriteLine($"[E-posta] Sipariş #{e.OrderId} için onay maili gönderildi.");
    }
}

public class StockUpdateHandler
{
    public void Handle(object? sender, OrderCreatedEventArgs e)
    {
        Console.WriteLine($"[Stok] {e.ProductName} stoğundan {e.Quantity} adet düşüldü.");
    }
}

// --- Program ---
public class Program
{
    public static void Main()
    {
        var orderService = new OrderService();
        var emailHandler = new EmailNotificationHandler();
        var stockHandler = new StockUpdateHandler();

        orderService.OrderCreated += emailHandler.Handle;
        orderService.OrderCreated += stockHandler.Handle;

        // Lambda ile de abone olunabilir
        orderService.OrderCreated += (sender, e) =>
            Console.WriteLine($"[Log] Sipariş #{e.OrderId} kaydedildi.");

        orderService.CreateOrder(1, "Laptop", 2);

        Console.WriteLine("---");

        // Aboneliği iptal etme
        orderService.OrderCreated -= emailHandler.Handle;
        orderService.CreateOrder(2, "Klavye", 5);
    }
}
