# CursorCloudTest

.NET ile Event-Driven Design (Olay Tabanlı Tasarım) örnekleri.

## Event-Driven Design Nedir?

Event-Driven Design, yazılım bileşenlerinin birbirleriyle **olaylar (events)** aracılığıyla iletişim kurduğu bir mimari yaklaşımdır. Temel prensipler şunlardır:

- **Gevşek Bağlılık (Loose Coupling):** Olayı yayınlayan (publisher) ve olayı dinleyen (subscriber) birbirini doğrudan tanımak zorunda değildir.
- **Asenkron İletişim:** Bileşenler olayları yayınlar, ilgilenen taraflar bu olayları dinleyip tepki verir.
- **Ölçeklenebilirlik:** Yeni dinleyiciler eklemek, mevcut kodu değiştirmeden yapılabilir.

Tipik akış şu şekildedir:

1. Bir **olay** meydana gelir (örn. sipariş oluşturuldu).
2. Olay bir **event bus** veya **mediator** üzerinden yayınlanır.
3. Kayıtlı **handler**'lar olayı yakalar ve ilgili işlemleri gerçekleştirir.

Her iki örnekte de aynı senaryo kullanılır: bir sipariş oluşturulduğunda hem e-posta gönderimi hem de stok güncelleme işlemi tetiklenir.

## Proje Yapısı

```
EventDrivenOrnekleri.sln
src/
├── EventBusOrnegi/       # 1. örnek: Basit bir Event Bus ile
└── EventKeywordOrnegi/   # 2. örnek: C# event keyword'ü ile
```

## Çalıştırma

.NET 8 SDK gereklidir.

```bash
dotnet run --project src/EventBusOrnegi
dotnet run --project src/EventKeywordOrnegi
```

## 1. Örnek: Event Bus ile

Kaynak kod: [`src/EventBusOrnegi/Program.cs`](src/EventBusOrnegi/Program.cs)

Herhangi bir harici kütüphane kullanmadan, saf C# ile yazılmış basit bir event bus. `EventBus` sınıfı, olay tipine göre handler'ları bir sözlükte tutar. `Subscribe` ile handler kaydedilir, `Publish` ile olay o tipe kayıtlı tüm handler'lara iletilir. `OrderService` yalnızca olayı yayınlar; e-posta veya stok servisinden haberi yoktur.

### Çıktı

```
Sipariş #1 oluşturuldu: Laptop x2
[E-posta] Sipariş #1 için onay maili gönderildi. Ürün: Laptop, Adet: 2
[Stok] Ürün: Laptop, stoktan 2 adet düşüldü.
---
Sipariş #2 oluşturuldu: Klavye x5
[E-posta] Sipariş #2 için onay maili gönderildi. Ürün: Klavye, Adet: 5
[Stok] Ürün: Klavye, stoktan 5 adet düşüldü.
```

### Temel Avantajları

| Avantaj | Açıklama |
|---|---|
| **Gevşek Bağlılık** | `OrderService`, e-posta veya stok servisini bilmez; sadece olayı yayınlar. |
| **Genişletilebilirlik** | Yeni bir handler (örn. loglama) eklemek için mevcut kodu değiştirmeye gerek yoktur; sadece `Subscribe` çağrısı eklenir. |
| **Tek Sorumluluk** | Her handler yalnızca kendi işinden sorumludur. |

## 2. Örnek: C# `event` Keyword'ü ile

Kaynak kod: [`src/EventKeywordOrnegi/Program.cs`](src/EventKeywordOrnegi/Program.cs)

Bu yöntemde ayrı bir `EventBus` sınıfına gerek yoktur. Olay doğrudan yayıncı sınıfın (`OrderService`) içinde tanımlanır, dinleyiciler de `+=` ile abone olur, `-=` ile aboneliklerini iptal eder.

### Çıktı

```
Sipariş #1 oluşturuldu: Laptop x2
[E-posta] Sipariş #1 için onay maili gönderildi.
[Stok] Laptop stoğundan 2 adet düşüldü.
[Log] Sipariş #1 kaydedildi.
---
Sipariş #2 oluşturuldu: Klavye x5
[Stok] Klavye stoğundan 5 adet düşüldü.
[Log] Sipariş #2 kaydedildi.
```

İkinci siparişte e-posta satırı çıkmaz, çünkü o dinleyicinin aboneliği `-=` ile iptal edilmiştir.

### Önemli Noktalar

- **`event EventHandler<T>`**: .NET'in standart olay imzası `(object? sender, TEventArgs e)` şeklindedir. `sender` olayı tetikleyen nesnedir.
- **`event` kelimesinin sağladığı koruma**: Dışarıdaki kod olaya yalnızca `+=` ve `-=` ile erişebilir. `OrderCreated.Invoke(...)` ile olayı tetikleyemez veya `OrderCreated = null` ile tüm aboneleri silemez. Bunu sadece sınıfın kendisi yapabilir.
- **`?.Invoke`**: Hiç abone yoksa olay `null` olur. Null kontrolü yapılmazsa `NullReferenceException` alınır.
- **`protected virtual OnXxx` metodu**: .NET'te yaygın bir kalıptır. Türetilmiş sınıfların olayın tetiklenme davranışını değiştirmesine olanak tanır.
- **Bellek sızıntısına dikkat**: Yayıncı, abonelerine referans tutar. Uzun ömürlü bir yayıncıya kısa ömürlü nesneler abone oluyorsa, işleri bitince `-=` ile aboneliklerini iptal etmeleri gerekir. Aksi halde garbage collector bu nesneleri temizleyemez.

## Event Bus ile `event` Keyword'ü Arasındaki Fark

`event` keyword'ü yayıncı ile dinleyici arasında doğrudan bir bağ kurar: dinleyicinin `OrderService` nesnesini tanıması gerekir. Event Bus ise iki tarafı birbirinden tamamen ayırır. Bu yüzden `event` keyword'ü tek uygulama içindeki basit senaryolar için uygundur. Bileşen sayısı arttığında bir mediator, servisler arası iletişimde ise bir mesajlaşma çözümü daha uygun olur.

## Gerçek Dünya Alternatifleri

Üretim ortamlarında bu yaklaşım genellikle şu araçlarla uygulanır:

- **MediatR**: Uygulama içi (in-process) mediator / event bus kütüphanesi.
- **MassTransit / NServiceBus**: Dağıtık mesajlaşma (RabbitMQ, Azure Service Bus vb. ile).
- **C# `event` keyword'ü**: Dil seviyesinde basit pub/sub mekanizması.
