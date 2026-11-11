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
docker-compose.yml        # RabbitMQ ve Kafka container'ları
src/
├── EventBusOrnegi/       # 1. örnek: Basit bir Event Bus ile (uygulama içi)
├── EventKeywordOrnegi/   # 2. örnek: C# event keyword'ü ile (uygulama içi)
├── RabbitMqOrnegi/       # 3. örnek: RabbitMQ ile (message broker)
├── MassTransitOrnegi/    # 4. örnek: MassTransit + RabbitMQ ile
└── KafkaOrnegi/          # 5. örnek: Apache Kafka ile (event streaming)
```

İlk iki örnek yalnızca .NET kullanır ve olayları aynı uygulamanın içinde iletir. Son üç örnek ise olayları harici bir broker (RabbitMQ veya Kafka) üzerinden iletir. Bu sayede olaylar farklı uygulamalar ve sunucular arasında taşınabilir.

## Çalıştırma

.NET 8 SDK gereklidir. İlk iki örnek başka bir şeye ihtiyaç duymaz:

```bash
dotnet run --project src/EventBusOrnegi
dotnet run --project src/EventKeywordOrnegi
```

Son üç örnek için önce Docker ile RabbitMQ ve Kafka'yı başlatın:

```bash
docker compose up -d
```

Ardından örnekleri çalıştırın. Her biri siparişleri yayınladıktan sonra mesajların gelmesini bekler; çıkmak için Enter'a basın.

```bash
dotnet run --project src/RabbitMqOrnegi
dotnet run --project src/MassTransitOrnegi
dotnet run --project src/KafkaOrnegi
```

RabbitMQ yönetim paneline <http://localhost:15672> adresinden erişebilirsiniz (kullanıcı adı ve şifre: `guest`). İşiniz bitince container'ları `docker compose down` ile durdurabilirsiniz.

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

## Uygulama İçinden Broker'a Geçiş

İlk iki örnekte olaylar bellekte, aynı process içinde iletilir. Bunun bazı sınırları vardır:

- Olaylar yalnızca aynı uygulamanın içinde dinlenebilir; başka bir servis bu olaylardan haberdar olamaz.
- Uygulama kapanırsa veya çökerse işlenmemiş olaylar kaybolur.
- Bir handler hata fırlatırsa sonraki handler'lar çalışmaz; yeniden deneme mekanizması yoktur.

Bu sınırlar, olayları **RabbitMQ** veya **Kafka** gibi harici bir broker üzerinden ileterek aşılır. Aşağıdaki üç örnekte senaryo ve handler'lar aynı kalır; yalnızca olayın yayınlanma ve dinlenme şekli değişir. Örnekleri sade tutmak için yayıncı ve dinleyiciler aynı programdadır. Gerçek bir sistemde bunlar genellikle ayrı uygulamalarda (örneğin bir sipariş servisi, bir bildirim servisi ve bir stok servisi) çalışır.

Broker kullanan örneklerde mesajlar asenkron işlendiği için çıktıdaki satırların sırası her çalıştırmada değişebilir.

## 3. Örnek: RabbitMQ ile

Kaynak kod: [`src/RabbitMqOrnegi/Program.cs`](src/RabbitMqOrnegi/Program.cs) · Paket: [`RabbitMQ.Client`](https://www.nuget.org/packages/RabbitMQ.Client) 7.2.2

RabbitMQ'nun resmi .NET istemcisi doğrudan kullanılır. Kurulan yapı şöyledir:

```
OrderService ──► [order-created exchange (fanout)] ──┬──► [order-created.email kuyruğu] ──► EmailNotificationHandler
                                                     └──► [order-created.stock kuyruğu] ──► StockUpdateHandler
```

- **Exchange**: `OrderService` mesajı doğrudan bir kuyruğa değil, `order-created` adlı exchange'e gönderir. Exchange tipi `fanout` olduğu için gelen her mesaj kendisine bağlı tüm kuyruklara kopyalanır.
- **Kuyruk (Queue)**: Her dinleyicinin kendi kuyruğu vardır. Böylece her dinleyici her mesajı alır ve diğerlerinden bağımsız çalışır.
- **Onay (Ack)**: Mesajlar `autoAck: false` ile alınır ve ancak handler başarıyla çalıştıktan sonra `BasicAckAsync` ile onaylanır. Onaylanmayan mesaj kaybolmaz, RabbitMQ tarafından tekrar teslim edilir.
- **Kalıcılık**: Kuyruklar `durable: true` ile tanımlanmıştır. Dinleyici uygulaması kapalıyken gelen mesajlar kuyrukta bekler ve uygulama tekrar açıldığında işlenir.

### Çıktı

```
Sipariş #1 oluşturuldu: Laptop x2
Sipariş #2 oluşturuldu: Klavye x5
Mesajlar RabbitMQ üzerinden dinleyicilere iletiliyor. Çıkmak için Enter'a basın.
[E-posta] Sipariş #1 için onay maili gönderildi. Ürün: Laptop, Adet: 2
[Stok] Ürün: Laptop, stoktan 2 adet düşüldü.
[Stok] Ürün: Klavye, stoktan 5 adet düşüldü.
[E-posta] Sipariş #2 için onay maili gönderildi. Ürün: Klavye, Adet: 5
```

## 4. Örnek: MassTransit ile

Kaynak kod: [`src/MassTransitOrnegi/Program.cs`](src/MassTransitOrnegi/Program.cs) · Paket: [`MassTransit.RabbitMQ`](https://www.nuget.org/packages/MassTransit.RabbitMQ) 8.5.12

MassTransit, RabbitMQ gibi broker'ların üzerinde çalışan bir mesajlaşma kütüphanesidir. 3. örnekte elle yapılan işlerin çoğunu otomatik olarak üstlenir:

- **Consumer'lar**: Dinleyiciler `IConsumer<OrderCreatedEvent>` arayüzünü uygular. `AddConsumer<T>()` ile kaydedilmeleri yeterlidir.
- **Otomatik topoloji**: `cfg.ConfigureEndpoints(context)` her consumer için exchange, kuyruk ve binding'leri kendisi oluşturur. Exchange veya kuyruk isimlerini elle yazmaya gerek kalmaz.
- **Yayınlama**: `OrderService`, RabbitMQ'yu hiç bilmez; sadece `IPublishEndpoint.Publish(...)` çağırır. Bu yapı 1. örnekteki `EventBus.Publish` çağrısına çok benzer.
- **Serileştirme, onay ve hata yönetimi**: JSON dönüşümü, mesaj onayı, yeniden deneme (retry) ve hatalı mesajları ayrı bir kuyruğa taşıma gibi işler kütüphane tarafından yapılır.
- **Broker bağımsızlığı**: `UsingRabbitMq` yerine `UsingAzureServiceBus` veya `UsingAmazonSqs` yazarak, consumer ve yayıncı koduna dokunmadan farklı bir broker'a geçilebilir.
- **Dependency Injection**: .NET'in standart `Host` ve DI yapısıyla bütünleşik çalışır.

> **Lisans notu:** MassTransit v9 ve sonraki sürümler ticari lisans gerektirir. Bu örnek, açık kaynak (Apache 2.0) olan v8 sürümüne sabitlenmiştir.

### Çıktı

```
Sipariş #1 oluşturuldu: Laptop x2
Sipariş #2 oluşturuldu: Klavye x5
Mesajlar MassTransit (RabbitMQ) üzerinden consumer'lara iletiliyor. Çıkmak için Enter'a basın.
[E-posta] Sipariş #2 için onay maili gönderildi. Ürün: Klavye, Adet: 5
[E-posta] Sipariş #1 için onay maili gönderildi. Ürün: Laptop, Adet: 2
[Stok] Ürün: Klavye, stoktan 5 adet düşüldü.
[Stok] Ürün: Laptop, stoktan 2 adet düşüldü.
```

MassTransit mesajları varsayılan olarak paralel işlediği için aynı consumer'da bile 2. sipariş 1. siparişten önce işlenebilir. Sıranın önemli olduğu durumlarda bu ayrıca yapılandırılmalıdır.

## 5. Örnek: Kafka ile

Kaynak kod: [`src/KafkaOrnegi/Program.cs`](src/KafkaOrnegi/Program.cs) · Paket: [`Confluent.Kafka`](https://www.nuget.org/packages/Confluent.Kafka) 2.16.0

Kafka bir mesaj kuyruğundan çok, **olay akışı (event streaming)** platformudur. Mesajlar okunduktan sonra silinmez; bir **topic** içinde, sırayla eklenen bir kayıt defteri (log) gibi saklanır.

```
OrderService ──► [order-created topic'i] ──┬──► consumer group: email-service ──► EmailNotificationHandler
                                           └──► consumer group: stock-service ──► StockUpdateHandler
```

- **Topic**: Olaylar `order-created` topic'ine yazılır. Program başlarken topic yoksa `AdminClient` ile oluşturulur.
- **Consumer group**: Her dinleyici farklı bir `GroupId` kullanır. Kafka her grubun nerede kaldığını (offset) ayrı ayrı tuttuğu için her grup tüm mesajları alır. Aynı gruba birden fazla consumer eklenirse mesajlar bu consumer'lar arasında paylaştırılır; bu da yatay ölçeklemeyi sağlar.
- **Key ve sıralama**: Mesajlar sipariş numarası key olarak verilerek gönderilir. Aynı key'e sahip mesajlar aynı partition'a gider ve sırasıyla işlenir.
- **Offset**: `AutoOffsetReset.Earliest` sayesinde yeni bir consumer group topic'in en başından okumaya başlar. Daha önce bağlanmış bir grup ise kaldığı yerden devam eder. Örneği ikinci kez çalıştırdığınızda yalnızca yeni siparişlerin işlendiğini görebilirsiniz.
- **Tekrar oynatma (replay)**: Mesajlar silinmediği için yeni bir consumer group ile geçmişteki tüm olaylar baştan okunabilir. Örneğin sonradan eklenen bir raporlama servisi, geçmiş siparişlerin hepsini işleyebilir.

### Çıktı

```
Sipariş #1 oluşturuldu: Laptop x2
Sipariş #2 oluşturuldu: Klavye x5
Mesajlar Kafka üzerinden dinleyicilere iletiliyor. Çıkmak için Enter'a basın.
[Stok] Ürün: Laptop, stoktan 2 adet düşüldü.
[E-posta] Sipariş #1 için onay maili gönderildi. Ürün: Laptop, Adet: 2
[Stok] Ürün: Klavye, stoktan 5 adet düşüldü.
[E-posta] Sipariş #2 için onay maili gönderildi. Ürün: Klavye, Adet: 5
```

Consumer group'un Kafka'ya ilk bağlanması birkaç saniye sürebilir; mesajların çıktıda görünmesi için biraz bekleyip ardından Enter'a basın.

## Örneklerin Karşılaştırması

| | Event Bus | `event` keyword | RabbitMQ | MassTransit | Kafka |
|---|---|---|---|---|---|
| Harici bağımlılık | Yok | Yok | RabbitMQ sunucusu | RabbitMQ sunucusu | Kafka sunucusu |
| Servisler arası iletişim | Hayır | Hayır | Evet | Evet | Evet |
| Uygulama kapanınca mesajlar | Kaybolur | Kaybolur | Kuyrukta bekler | Kuyrukta bekler | Topic'te saklanır |
| İşlenen mesaj | - | - | Kuyruktan silinir | Kuyruktan silinir | Saklanmaya devam eder |
| Yeniden deneme / hata yönetimi | Elle | Elle | Elle (ack/nack) | Hazır | Elle |
| Tipik kullanım | Basit uygulama içi senaryolar | UI ve bileşen olayları | Servisler arası görev ve bildirim iletimi | .NET mikroservisleri | Yüksek hacimli olay akışı, analitik |

## Gerçek Dünya Alternatifleri

Üretim ortamlarında bu yaklaşım genellikle şu araçlarla uygulanır:

- **C# `event` keyword'ü**: Dil seviyesinde basit pub/sub mekanizması.
- **MediatR**: Uygulama içi (in-process) mediator / event bus kütüphanesi.
- **RabbitMQ, Azure Service Bus, Amazon SQS**: Servisler arası mesajlaşma için message broker'lar.
- **MassTransit / NServiceBus**: Bu broker'ların üzerinde çalışan, retry ve hata yönetimi gibi özellikleri hazır sunan .NET kütüphaneleri.
- **Apache Kafka, Azure Event Hubs**: Yüksek hacimli olay akışı (event streaming) platformları.
