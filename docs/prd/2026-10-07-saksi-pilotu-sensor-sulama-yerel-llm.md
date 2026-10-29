# PRD — Saksı Pilotu: Gerçek Sensörler, LLM Kararlı Sulama, Öneri Notları ve Eğitilmiş Tahmin Modeli

Tarih: 2026-10-07 · Sürüm: 2 · Durum: Taslak · Kapsam: yeni ESP32 firmware'i, API, web arayüzü, LLM katmanı, makine öğrenmesi modeli

## 1. Özet

SensSera bugün simülatörden gelen veriyle çalışıyor ve LLM yalnızca alarmları açıklayıp soruları cevaplıyor. Bu PRD sistemi gerçek saksılarda kendi kararını veren bir sisteme dönüştürür:

1. **Gerçek sensörler:** 2–3 saksılık bir deney setinde ESP32 tabanlı bir kart; toprak nemi, hava sıcaklığı ve nemi, ışık ve su deposu seviyesini ölçer.
2. **LLM kararlı sulama:** Yerel LLM, sensör verisine ve tahmin modeline bakarak hangi saksının ne zaman, kaç saniye sulanacağına karar verir ve kararı uygular. Kararlar kodla tanımlanmış bir güvenlik zarfının içinde kalır; LLM erişilemezse kural tabanlı kontrol devreye girer.
3. **Öneri notları:** LLM sulama dışında da öneri üretir: eşik ayarı, saksı yerleşimi, sensör arızası, depo doldurma zamanı. Her not gerekçesi ve dayandığı veriyle kaydedilir; ayar değiştiren öneriler kullanıcı onayıyla uygulanır.
4. **Eğitilmiş tahmin modeli:** Açık veri setleri ve kendi saksı verimizle eğitilen bir model, her saksının önümüzdeki saatlerdeki nemini ve eşiğe ne zaman düşeceğini tahmin eder. LLM bu tahmini bir araç olarak kullanır.
5. **Yerel LLM:** Tüm LLM özellikleri internet ve API anahtarı olmadan, masaüstü PC'de Ollama üzerinden çalışır.

Önce saksılarda denenir; sera ölçeği sonraki adımdır.

## 2. Mevcut durum (koddan)

| Konu | Bugün | Bu PRD için anlamı |
| --- | --- | --- |
| Veri kaynağı | `SensSera.Simulator` her cihaz için `/api/ingest`'e POST atıyor | ESP32 aynı uç noktayı kullanabilir; firmware simülatörün yerini alır |
| Cihaz modeli | `Device` = tek metrik + tek token (`<deviceId>.<secret>`, SHA-256) | Bir ESP32 kartında 5–6 sensör var; kart başına 6 token yönetmek hantal |
| Metrik kontrolü | `IngestionService`, okumanın metriğinin cihazın metriğiyle eşleştiğini kontrol etmiyor | Kanal modeliyle birlikte bu doğrulama eklenmeli |
| Ingest rate limit | `ingest` limiti **global** sabit pencere: tüm cihazlar toplam 100 istek/dk paylaşıyor | Gerçek kart + simülatör aynı anda çalışınca 429 riski; cihaz/kart bazına bölünmeli |
| Eşikler | `Threshold` sera + metrik bazında | Saksı başına farklı nem hedefi için yetmez |
| Komut kanalı | Yok; sunucu cihaza hiçbir şey gönderemiyor | LLM kararlarını cihaza iletmek için gerekli |
| LLM | `ILlmClient` + `GroqLlmClient` (OpenAI uyumlu `/chat/completions`, araç döngüsü hazır) + `AnthropicLlmClient`; Groq adresi `Program.cs`'te sabit | Ollama da OpenAI uyumlu; adresi ayara çevirmek yerel LLM'in büyük kısmını çözer. Araç döngüsü karar ajanı için de kullanılır |
| ML | Yok; mevcut tasarım bilinçli olarak "ML tahmini yok" duruşunda | Bu PRD ile tahmin modeli kapsama girer; LLM ile tahmin modelinin rolleri ayrı tutulur (§10) |

## 3. Hedefler ve hedef dışı

**Hedefler**

- Gerçek bir ESP32 kartından en az 2 hafta kesintisiz veri toplamak ve canlı panoda göstermek.
- Saksı nemini, LLM'in kararlarıyla bant içinde tutmak ve bunu kural tabanlı kontrolle karşılaştırmak.
- LLM'in her kararı ve her önerisi için gerekçeyi ve dayandığı veriyi kaydetmek.
- Açık veri seti ve kendi verimizle eğitilmiş, hatası ölçülmüş bir nem tahmin modeli.
- Sulamayı güvenli yapmak: kuru çalışma, taşma ve sonsuz pompa döngüsü olmamalı; LLM ne derse desin.
- Mevcut çok kiracılı yapı, testler ve bulut dağıtımı bozulmadan kalması.

**Hedef dışı**

- Sera ölçeğinde vana, LoRa, güneş paneli gibi saha donanımı.
- LLM'in güvenlik sınırlarını (en uzun sulama süresi, aralık, günlük sınır) kendisinin değiştirmesi. Bunları yalnızca öneri olarak sunabilir.
- Özel PCB ve 3D baskı kutu.
- Backend'in başka bir dile taşınması (bkz. §16).

## 4. Kullanıcı senaryoları

- Kullanıcı olarak saksılarımın anlık nemini, tahmini kuruma zamanını, son sulamayı ve depo durumunu tek ekranda görmek istiyorum.
- Kullanıcı olarak her saksı için kontrol modunu seçmek istiyorum: Manuel, Kural veya LLM.
- Kullanıcı olarak LLM modundaki bir saksının neden sulandığını ya da neden sulanmadığını, kararın gerekçesiyle birlikte görmek istiyorum.
- Kullanıcı olarak LLM'in bıraktığı öneri notlarını okumak, ayar değiştiren bir öneriyi tek tıkla kabul etmek veya reddetmek istiyorum.
- Kullanıcı olarak depo boşaldığında veya bir sensör tuhaf davrandığında uyarı ve öneri almak istiyorum.
- Kullanıcı olarak "2 numaralı saksı bugün neden iki kez sulandı?" diye sorup verilere dayanan bir cevap almak istiyorum, internet olmasa bile.

## 5. Donanım

### 5.1 Kart seçimi

"Arduino" olarak **ESP32-S3 DevKitC (N16R8: 16 MB flash, 8 MB PSRAM)** kullanılır. PlatformIO ile, Arduino çatısıyla programlanır. Klasik Arduino Uno'nun Wi-Fi'ı yok; ek modülle uğraşmak yerine Wi-Fi'ı dahili ESP32 seçilir. S3, ilk plandaki WROOM-32'ye göre daha fazla bellek ve yerel USB sunar; yerel USB, firmware'in tarayıcıdan sürücüsüz yüklenmesini sağlar (§6). Kart yalnızca 2,4 GHz Wi-Fi'a bağlanır.

### 5.2 Malzeme listesi (3 saksı için)

| Parça | Adet | Not |
| --- | --- | --- |
| ESP32-S3 DevKitC N16R8 (pin başlıkları lehimli) | 1 | Wi-Fi dahili, Arduino çatısıyla programlanır |
| Kapasitif toprak nem sensörü v1.2 | 3 (+1 yedek) | Rezistif (FC-28 gibi) değil: rezistif problar toprakta korozyona uğrar. Ucuz v1.2 kartların bir kısmı hatalı çıkabiliyor, yedek alıp kalibrasyonda test et |
| DHT22 (AM2302) sıcaklık/nem | 1 | Hava sıcaklığı ve nemi. Daha hassası istenirse SHT31 (I²C) |
| BH1750 ışık sensörü | 1 | I²C, lux cinsinden; mevcut `Light` metriğinin 0–100.000 aralığına uyar |
| 3–6 V mini dalgıç pompa | 3 | Saksı başına bir pompa; aynı anda yalnızca biri çalışır |
| Silikon hortum | 2–3 m | Pompanın çıkış çapına uygun |
| 4 kanallı 5 V röle modülü (optokuplörlü) | 1 | Alternatif: mantık seviyeli MOSFET (IRLZ44N) + 1N4007 diyot |
| Şamandıra şalter (float switch) | 1 | Depo boşken pompayı kilitler |
| Su deposu (bidon, 5 L civarı) | 1 | |
| 5 V 2 A adaptör + DC jak | 1 | Pompalar ve röle; geliştirmede ESP32 USB'den beslenebilir |
| Breadboard, jumper kablo | 1 set | Tezgah testi için |
| Delikli plaket, erkek pin başlıkları, makaron | 1 set | Kalıcı düzenek lehimlenir; sensörler pin başlığıyla takılıp çıkarılabilir kalır |
| Kablo rakoru (PG7/PG9) | 4–6 | Kabloların buata su geçirmeden girmesi için |
| Su geçirmez buat (IP65) | 1 | Elektronik saksıların yanında, su sıçramasına karşı |
| Ölçü kabı | 1 | Pompa debisini ölçmek için |

Deney karşılaştırması (§12) için üç saksıda **aynı bitki türü, aynı toprak ve aynı saksı boyutu** kullanılır. Fiyat sütunu bilinçli olarak boş bırakıldı; Robotistan veya Direnc.net gibi yerel tedarikçilerden güncel fiyatlarla doldurulmalı.

### 5.3 Pin planı

| Bağlantı | ESP32-S3 pini | Gerekçe |
| --- | --- | --- |
| Nem sensörü 1 / 2 / 3 (yedek: 4) | GPIO4 / GPIO5 / GPIO6 (GPIO7) | ADC1 pinleri (S3'te GPIO1–10); ADC2 Wi-Fi açıkken kullanılamaz |
| DHT22 veri | GPIO16 | 10 kΩ pull-up (çoğu modülde hazır) |
| BH1750 SDA / SCL | GPIO8 / GPIO9 | S3 DevKitC'nin Arduino'daki varsayılan I²C pinleri |
| Şamandıra şalter | GPIO17 | `INPUT_PULLUP`; LOW = depo boş (kalibrasyonda doğrulanır) |
| Röle 1 / 2 / 3 (pompalar) | GPIO11 / GPIO12 / GPIO13 | Strapping pinlerinden (GPIO0, 3, 45, 46) kaçınılır; açılışta pompa tetiklenmez |

Kaçınılan pinler: GPIO19/20 (yerel USB), GPIO26–32 (flash), GPIO35–37 (N16R8'de sekizli PSRAM), GPIO48 (kart üstü RGB LED). İlk plandaki WROOM-32 pinleri (GPIO34/35 gibi) S3'te ya yok ya da PSRAM'e ayrılmış durumda.

Sensörler 3,3 V ile beslenir; böylece analog çıkış ADC aralığında kalır. Pompa ve röle 5 V hattındadır, toprak hattı (GND) ESP32 ile ortaktır. Bazı 5 V röle modülleri 3,3 V mantıkla güvenilir tetiklenmez; modül alınırken bu kontrol edilmeli, sorun çıkarsa MOSFET'e geçilmeli.

### 5.4 Kalibrasyon

Her nem sensörü için iki ham değer kaydedilir: havada (kuru) ve suda (ıslak). Okuma bu iki değer arasında 0–100 %'ye eşlenir. Her pompa için debi ölçülür: pompa 10 saniye ölçü kabına çalıştırılır ve ml/s hesaplanır. Böylece her sulama ml olarak kaydedilir ve deneyde kullanılan toplam su karşılaştırılabilir. Kalibrasyon değerleri kanal ayarı olarak sunucuda tutulur ve karta indirilir.

## 6. Firmware gereksinimleri

- **Çatı:** PlatformIO + Arduino çatısı. Kütüphaneler: `WiFi`, `HTTPClient`, `ArduinoJson`, DHT, BH1750, `Preferences`.
- **Kimlik:** Kart tokenı ve Wi-Fi bilgileri NVS'te (`Preferences`) saklanır; repoya girmez. İlk kurulum seri port üzerinden basit bir komutla yapılır.
- **Zaman:** Açılışta NTP ile saat alınır. `recordedAt` her zaman UTC gönderilir.
- **Ölçüm:** Her kanal için 10 örneğin ortalaması alınır; varsayılan ölçüm aralığı 30 sn, sunucudan değiştirilebilir.
- **Gönderim:** Okumalar kart başına toplu gönderilir (`/api/ingest/batch`, en fazla 100 okuma).
- **Kesinti dayanıklılığı:** Bağlantı yoksa okumalar halka tamponda (ring buffer) birikir ve bağlantı gelince toplu gönderilir. Tampon dolarsa en eski okuma atılır.
- **Ayar ve komut:** 30 sn'de bir ayar uç noktası sorgulanır (§7.3). Gelen sulama komutu, kartın kendi güvenlik sınırlarıyla yeniden denetlenir (§8.3); sınırı aşan komut kırpılır veya reddedilir ve sonuç sunucuya bildirilir.
- **Kontrol modları:** Saksının modu `Rule` ise kart kendi kararını verir. `Llm` veya `Manual` ise yalnızca sunucudan gelen komutları uygular. Sunucuya 15 dk ulaşılamazsa `Llm` modundaki saksılar yerel `Rule` moduna düşer ve bağlantı gelince geri döner.
- **Güvenlik:** Açılışta tüm röleler kapalı konuma çekilir. Watchdog etkin. Yerel ağdaki demo HTTP kullanır; buluta bağlanırken HTTPS ve sunucunun kök sertifikası gömülür (`setInsecure` yalnızca tezgah testinde).
- **Sürüm:** Firmware sürümü her ayar isteğinde gönderilir ve panoda görünür.
- **Uzaktan bakım:** Düzenek, geliştiricinin erişemediği bir evde çalışır (§13). Bu yüzden ayarların tamamı sunucudan gelir ve yeniden yükleme gerektirmez; yeniden yükleme yalnızca firmware mantığı değiştiğinde gerekir. CI her sürümde `.bin` üretir; web uygulamasındaki kurulum sayfası (ESP Web Tools) firmware'i tarayıcıdan USB ile yükler ve Wi-Fi ile kart tokenını aynı sayfadan NVS'e yazar. Yükleme flash'ı silmez, böylece Wi-Fi ve token korunur. Kart kendi kendine güncelleme (OTA) yalnızca ihtiyaç olursa Faz 2'de ve imza doğrulamasıyla eklenir.

## 7. Backend değişiklikleri

### 7.1 Veri modeli

| Varlık | Alanlar (özet) | Not |
| --- | --- | --- |
| `Node` (yeni) | `Id`, `GreenhouseId`, `Name`, `TokenHash`, `FirmwareVersion`, `LastSeenAt` | Fiziksel kart. Tek token, kendi kanalları |
| `Device` (değişir) | + `NodeId?`, + `Channel?` (ör. `soil1`, `air_temp`) | Mevcut tek tokenlı cihazlar (simülatör) aynen çalışır |
| `Zone` (yeni) | `Id`, `GreenhouseId`, `Name`, `PlantType?`, `SoilDeviceId`, `PumpChannel`, `PumpMlPerSecond`, `MinMoisture`, `MaxMoisture`, `MaxPulseSeconds`, `MinIntervalMinutes`, `MaxDailyWaterings`, `ControlMode` (Manual / Rule / Llm) | Bir saksı. Bant ve güvenlik sınırları burada |
| `IrrigationDecision` (yeni) | `Id`, `ZoneId`, `CreatedAt`, `Source` (Llm / Rule / User), `Action` (Water / Wait / Alert), `RequestedSeconds`, `Reason`, `Confidence?`, `PolicyResult` (Allowed / Clamped / Rejected + neden), `ApprovedSeconds`, `Model?`, `LatencyMs?`, `ToolCallsJson?` | Her karar, uygulanmasa da kaydedilir |
| `IrrigationCommand` (yeni) | `Id`, `DecisionId`, `ZoneId`, `NodeId`, `Seconds`, `Status` (Pending / Delivered / Done / RejectedByDevice / Expired), `CreatedAt`, `ExpiresAt` | Karttan 2 dk içinde alınmayan komut düşer |
| `IrrigationEvent` (yeni) | `Id`, `ZoneId`, `CommandId?`, `StartedAt`, `DurationMs`, `WaterMl`, `Trigger` (Rule / Llm / Manual), `Result` (Completed / BlockedLowWater / BlockedInterval / BlockedDailyLimit), `MoistureBefore`, `MoistureAfter?` | Pompanın gerçekten çalıştığı (veya cihazda engellendiği) her olay |
| `RecommendationNote` (yeni) | `Id`, `GreenhouseId`, `ZoneId?`, `CreatedAt`, `Kind` (Watering / Threshold / Placement / Sensor / Reservoir / General), `Text`, `EvidenceJson`, `ProposedChangeJson?`, `Status` (Open / Accepted / Dismissed), `Model` | LLM öneri notu. `ProposedChangeJson` varsa kabul edildiğinde ayar uygulanır |
| `ForecastLog` (yeni) | `Id`, `ZoneId`, `MadeAt`, `HorizonHours`, `PredictedMoisture`, `ActualMoisture?`, `ModelVersion` | Tahmin hatasını canlıda ölçmek için |
| `MetricType` | + `WaterLevel` | Şamandıra: 0 (boş) veya 100 (dolu) |

Tüm yeni varlıklar `OrganizationId` taşır ve mevcut global sorgu filtresine bağlanır.

### 7.2 Kimlik doğrulama ve ingest

- `DeviceTokenAuthenticationHandler` önce `Device`, bulamazsa `Node` tablosunda arar. Token biçimi aynı kalır (`<id>.<secret>`). Node için `node_id` claim'i eklenir.
- Node tokenıyla gelen okuma `channel` alanı taşır; API bunu `(NodeId, Channel)` ile ilgili `Device`'a eşler ve metriğin o kanalın metriğiyle eşleştiğini doğrular. Eşleşmeyen veya bilinmeyen kanal `400` döner.
- `ingest` rate limiti global pencereden token bazlı (claim'deki `device_id` / `node_id`) bölmeli pencereye geçer.

### 7.3 Yeni uç noktalar

| Uç nokta | Kimlik | Açıklama |
| --- | --- | --- |
| `GET /api/ingest/config` | Node token | Ölçüm aralığı, kanal kalibrasyonları, saksı modları, güvenlik sınırları ve bekleyen komutlar. Komutlar bu yanıtta `Delivered` olur |
| `POST /api/ingest/events` | Node token | Sulama olayları ve komut sonuçları (`Done` / `RejectedByDevice`) |
| `GET/POST/PUT/DELETE /api/greenhouses/{id}/zones` | JWT | Saksı yönetimi, kontrol modu dahil |
| `POST /api/zones/{id}/water` | JWT | Manuel sulama; aynı politika denetiminden geçer |
| `GET /api/zones/{id}/decisions` | JWT | Karar geçmişi ve gerekçeler |
| `GET /api/zones/{id}/irrigation-events` | JWT | Sulama geçmişi |
| `GET /api/zones/{id}/forecast` | JWT | Tahmin eğrisi ve eşiğe düşme zamanı |
| `GET /api/greenhouses/{id}/notes`, `POST /api/notes/{id}/accept`, `POST /api/notes/{id}/dismiss` | JWT | Öneri notları; kabul edilen ayar önerisi politika sınırları içinde uygulanır |
| `POST /api/nodes`, `POST /api/nodes/{id}/rotate-token` | JWT | Kart kaydı; token yalnızca bir kez gösterilir |

MQTT bilinçli olarak ilk aşamaya alınmadı: HTTP ile ayar sorgulama, bir broker işletmeden aynı işi görür ve mevcut kimlik doğrulama hattını kullanır.

### 7.4 Arka plan işleri ve gerçek zamanlılık

| İş | Sıklık | Görev |
| --- | --- | --- |
| `IrrigationDecisionJob` (yeni) | 30 dk'da bir + tetiklemeli | `Llm` modundaki saksılar için karar ajanını çalıştırır (§9.3). Nem bandın alt sınırına yaklaştığında veya tahmin 2 saat içinde eşiğin altına düşüleceğini söylediğinde döngüyü beklemeden tetiklenir |
| `RecommendationJob` (yeni) | Günde bir (akşam) + olay tetiklemeli | Günün özetini ve öneri notlarını üretir (§9.4). Depo boşalması, sensör anomalisi ve art arda reddedilen kararlar anlık not tetikler |
| `ForecastJob` (yeni) | Saatte bir | Her saksı için tahmin üretir, `ForecastLog`'a yazar, süresi dolan tahminlerin gerçek değerini doldurur |
| `DeviceHeartbeatJob` (değişmez) | Dakikada bir | Kanal cihazlarını 5 dk sessizlikte çevrimdışı işaretler. Node durumu saklanmaz; her okumada `LastSeenAt`'ten hesaplanır (`NeverSeen` / `Online` / `Offline`) |

Depo boşalması ve çevrimdışı kart mevcut alarm akışından uyarı üretir. Yeni SignalR olayları: `DecisionMade`, `IrrigationStarted`, `IrrigationCompleted`, `NoteCreated`, `NodeStatusChanged`.

## 8. Sulama kontrolü

### 8.1 Üç katman

Sulama kararı LLM'den gelebilir, ama sınırları kod koyar. Her sulama üç katmandan geçer:

1. **Karar katmanı:** Saksının moduna göre LLM karar ajanı (§9.3), kural motoru (§8.2) veya kullanıcı karar verir. Çıktı: sula / bekle / uyar, saniye ve gerekçe.
2. **Politika katmanı (sunucu):** Karar şu sınırlara göre denetlenir: süre `MaxPulseSeconds`'ı aşamaz; son sulamadan bu yana `MinIntervalMinutes` dolmuş olmalı; günlük `MaxDailyWaterings` aşılmamalı; depo dolu olmalı; nem `MaxMoisture`'ın altında olmalı; aynı anda tek pompa çalışır. Uygun karar onaylanır, aşırı süre kırpılır, ihlal eden karar reddedilir. Her sonuç `IrrigationDecision.PolicyResult` olarak kaydedilir.
3. **Cihaz katmanı (ESP32):** Aynı sınırlar kartta bir kez daha uygulanır. Sunucuda bir hata olsa veya komut bozulsa da pompa sınırın dışına çıkamaz.

### 8.2 Kural motoru (yedek ve karşılaştırma tabanı)

`Rule` modunda ve LLM'e ulaşılamadığında devreye girer:

1. Nem `MinMoisture`'ın altındaysa saksı sulama adayıdır.
2. Politika sınırları sağlanıyorsa pompa en fazla `MaxPulseSeconds` çalışır.
3. Sulamadan sonra 10 dk bekleme (soak) boyunca o saksı için karar verilmez; su toprağa yayılmadan ölçüm yanıltıcıdır.
4. Bekleme sonunda nem hâlâ düşükse döngü tekrar başlar.

### 8.3 Varsayılan güvenlik değerleri

Tek sulama en fazla 8 sn, aynı saksı için en az 30 dk ara, günde en fazla 6 sulama, sulamadan sonra 10 dk bekleme. Değerler pompa debisi ölçüldükten sonra güncellenir. LLM bu değerleri değiştiremez; değişiklik önerisi yalnızca öneri notu olarak gelir ve kullanıcı onayıyla uygulanır.

### 8.4 Hata durumları

| Durum | Davranış |
| --- | --- |
| LLM zaman aşımı, erişilemiyor veya geçersiz JSON | Karar `Rule` motoruna düşer; olay `Source = Rule` ve "LLM yedeği" nedeniyle kaydedilir |
| LLM art arda 3 kez reddedilen karar üretir | Saksı 2 saat `Rule` moduna geçer ve bir öneri notu açılır |
| Kart sunucuya ulaşamıyor | Kart 15 dk sonra yerel kural moduna geçer (§6) |
| Depo boş | Tüm sulamalar engellenir, uyarı ve "depoyu doldur" notu |

## 9. LLM katmanı

### 9.1 Sağlayıcı: yerel LLM

`GroqLlmClient` zaten OpenAI uyumlu `/chat/completions` biçimini konuşuyor ve araç döngüsünü bu biçimle yürütüyor. Ollama da aynı biçimde bir uç nokta sunduğu için yeni bir LLM istemcisi yazılmaz:

- `GroqLlmClient` → `OpenAiCompatibleLlmClient` olarak yeniden adlandırılır.
- `LlmOptions`'a `BaseUrl` ve `TimeoutSeconds` eklenir; `Program.cs`'teki sabit Groq adresi kaldırılır.
- `Llm:Provider` değerleri: `Groq`, `Ollama`, `Anthropic`. Ollama için API anahtarı gerekmez.

| Ayar | Bulut (bugün) | Yerel |
| --- | --- | --- |
| `Llm:Provider` | `Groq` | `Ollama` |
| `Llm:BaseUrl` | `https://api.groq.com/openai/v1/` | `http://host.docker.internal:11434/v1/` (Compose içinden) |
| `Llm:Model` | `openai/gpt-oss-120b` | `qwen3:8b` (yedek: `qwen3:4b`) |

### 9.2 Model seçimi

Hedef makine: RTX 4060 (8 GB VRAM) masaüstü. `qwen3:8b` 4-bit nicemlemeyle bu belleğe sığar, araç çağırmayı destekler ve Türkçe cevap verebilir. Gecikme yüksek çıkarsa `qwen3:4b` denenir. Karar döngüsü 30 dakikalık olduğu için saniyeler süren yanıt kabul edilebilir.

Qwen3 varsayılan olarak "düşünme" çıktısı üretir. Bu çıktı ya istemde `/no_think` ile kapatılır ya da cevaptan `<think>…</think>` blokları ayıklanır. Karar ajanında düşünme açık bırakılıp ayıklanabilir; değerlendirmede iki seçenek de ölçülür.

### 9.3 Karar ajanı

`IrrigationDecisionJob` her `Llm` modundaki saksı için ajanı çalıştırır.

**Araçlar** (hepsi tenant filtresinden geçer, sadece okur):

| Araç | Döndürdüğü |
| --- | --- |
| `get_zone_status` | Anlık nem, bant, mod, son sulama, depo durumu, güvenlik sınırları ve bugün kalan sulama hakkı |
| `get_moisture_forecast` | Tahmin modelinin 1–6 saatlik nem tahmini ve eşiğe düşme zamanı (§10) |
| `get_recent_readings` | Saksının son 6 saatlik nemi ile ortam sıcaklığı, nemi ve ışığı |
| `get_irrigation_history` | Son 24 saatin kararları, sulamaları ve sulama sonrası nem tepkisi |

**Çıktı:** Araç turlarından sonra model tek bir JSON nesnesi döndürür ve API bunu şemaya göre doğrular:

```json
{
  "zoneId": "…",
  "action": "water | wait | alert",
  "seconds": 5,
  "confidence": 0.8,
  "reason": "Nem 31 %, bandın altı 35 %; tahmin 1 saatte 28 %'e düşüyor. Son sulama 3 saat önce.",
  "note": "İsteğe bağlı: kullanıcıya kısa öneri"
}
```

**İstem ilkeleri:**

- Sayılar yalnızca araç çıktılarından gelir; gerekçede geçen her sayı araç çıktısında bulunmalıdır.
- Model güvenlik sınırlarını bilir ama onları uygulayan kod değildir; sınırı aşan karar politika katmanında kırpılır veya reddedilir.
- Belirsizlikte "bekle" tercih edilir; az sulamak fazla sulamaktan daha kolay telafi edilir.
- Gerekçe Türkçe ve iki cümleyi geçmez.

Karar, araç çağrıları, gecikme ve politika sonucu `IrrigationDecision` olarak saklanır. Panoda her sulamanın yanında "neden" gösterilir.

### 9.4 Öneri notları

LLM yalnızca sulamada değil, saksı bakımının geneli için öneri üretir. Notlar `RecommendationNote` olarak kaydedilir ve panoda "Saha notu — AI" bloğunda (`FieldNote` bileşeni) gösterilir.

| Not türü | Örnek | Uygulama |
| --- | --- | --- |
| Watering | "Saksı 2 öğleden sonra hızlı kuruyor; sulamayı sabaha almak daha az su harcar." | Bilgi |
| Threshold | "Saksı 1'in bandı 35–55 %; son bir haftada sürekli alt sınırda. Bandı 40–60 %'a çekmeyi öneriyorum." | Kabul edilirse ayar uygulanır |
| Placement | "Saksı 3 diğerlerinden yarı ışık alıyor; nem tüketimi de yarı. Bitki türü için ışık yetersiz olabilir." | Bilgi |
| Sensor | "Saksı 1 nem sensörü 6 saattir aynı değeri gösteriyor; bağlantıyı kontrol et." | Bilgi + uyarı |
| Reservoir | "Mevcut tüketimle depo yaklaşık 3 gün sonra boşalacak." | Bilgi |

Kurallar:

- Her not, dayandığı araç çıktılarını `EvidenceJson` olarak saklar.
- Ayar değiştiren öneriler (`ProposedChangeJson`) **otomatik uygulanmaz**; kullanıcı kabul edince uygulanır ve politika sınırları içinde kalmak zorundadır.
- Aynı konuda açık bir not varsa yenisi açılmaz, mevcut not güncellenir.
- Günde saksı başına en fazla 3 not; not yorgunluğu önlenir.

### 9.5 Soru-cevap ve açıklama

Mevcut "Açıkla" ve "Defterine sor" özellikleri yerel modelle çalışır ve yeni araçlara (`get_zone_status`, `get_irrigation_history`, `get_moisture_forecast`) erişir. Örnek: "2 numaralı saksı bugün neden iki kez sulandı?" sorusu karar kayıtlarındaki gerekçelerle cevaplanır.

### 9.6 LLM değerlendirmesi

İki ayrı değerlendirme seti hazırlanır:

1. **Karar seti (30 senaryo):** Saksı durumu, tahmin ve geçmiş sabitlenmiş, doğru aksiyonu bilinen senaryolar. Örnekler: bandın altında ve depo dolu (sula); bandın altında ama 10 dk önce sulanmış (bekle); depo boş (uyar); nem normal ama tahmin 1 saatte düşüyor (sula veya kısa bekle). Ölçülenler: doğru aksiyon oranı, politika ihlali öneren karar oranı, gerekçedeki sayıların araç çıktısıyla eşleşmesi.
2. **Soru seti (20 Türkçe soru):** Doğru araç seçimi, uydurma sayı içermeme, Türkçe cevap kalitesi (1–5, iki kişi bağımsız puanlar).

Her iki set üç yapılandırmada çalıştırılır: Groq `gpt-oss-120b`, Ollama `qwen3:8b`, Ollama `qwen3:4b`. Medyan ve p95 yanıt süresi de raporlanır.

## 10. Tahmin modeli ve veri setiyle eğitim

### 10.1 Rol ayrımı

- **Tahmin modeli** sayısal bir soruyu cevaplar: "Bu saksının nemi önümüzdeki saatlerde ne olacak, eşiğe ne zaman düşecek?" Veriyle eğitilir, hatası MAE ile ölçülür.
- **LLM** karar verir ve açıklar: tahmini, geçmişi ve sınırları birlikte değerlendirip sulama kararı ve öneri notu üretir.

Böylece "eğitilmiş model" iddiası ölçülebilir bir modele dayanır; LLM'in rolü ayrıca değerlendirilir.

### 10.2 Problem tanımı

Girdi: bir saksının son ölçümleri. Çıktı: 1, 3 ve 6 saat sonraki toprak nemi (%) ve `MinMoisture`'a düşme zamanı.

Özellikler:

- Anlık nem ve son 1 / 3 saatteki değişim hızı
- Hava sıcaklığı, hava nemi, ışık (anlık ve son 3 saat ortalaması)
- Son sulamadan bu yana geçen süre ve son sulamada verilen su (ml)
- Günün saati (sinüs/kosinüs olarak)

### 10.3 Veri

| Kaynak | İçerik | Kullanım |
| --- | --- | --- |
| [Soil Moisture, Air Temperature, Humidity and Motor on/off Monitoring Data](https://data.mendeley.com/datasets/fpdwmm7nrb/1) (Manipal University Jaipur, 2023) | Kapasitif nem sensörü, DHT-11 sıcaklık/nem, pompa açık/kapalı durumu | Donanımımıza en yakın açık veri; ön eğitim ve yöntem geliştirme |
| [Smart agriculture dataset in a tomato cultivation under different irrigation regimes](https://www.tlc.unipr.it/ferrari/Publications/Journals/Be_et_al_DiB25.pdf) (Belli ve ark., Data in Brief, 2025; veri: Mendeley DOI 10.17632/35wh56287y.2, CC BY 4.0) | 10 dk aralıklı toprak nemi/sıcaklığı/EC, hava verisi, sulama hacmi; 76 gün, üç farklı sulama rejimi | Farklı sulama miktarlarına nem tepkisini öğrenmek; tarla verisi olduğu için saksıya aktarımı ayrıca ölçülür |
| [Environmental Sensor Dataset for LSTM-Based Predictive Irrigation in Smart Home Gardening Using Edge IoT](https://data.mendeley.com/datasets/jtsyb593fy/1) | Ev bahçeciliği için sensör verisi (içeriği henüz doğrulanmadı) | Aday; uygun çıkarsa saksı ölçeğine en yakın ikinci kaynak |
| Kendi saksı verimiz | Faz 1'den itibaren her 30 sn'de bir; sulama olaylarıyla birlikte | Asıl eğitim ve test verisi |

Kendi verimizin yeterli olması için veri toplama **Faz 1'de, sulama kodu yazılmadan önce** başlar. Bu dönemde saksılar elle veya kural moduyla sulanır, olaylar kaydedilir.

### 10.4 Modeller ve değerlendirme

1. **Taban:** Son 3 saatteki kuruma hızının doğrusal uzatılması.
2. **Gradient boosting (LightGBM):** Ana model.
3. **LSTM (isteğe bağlı):** Zaman alırsa karşılaştırma için.

Değerlendirme zaman bazlı bölmeyle yapılır: geçmişle eğitilir, sonraki günlerle test edilir. Ölçüt her ufuk için MAE (nem yüzdesi). Üç senaryo karşılaştırılır: yalnızca açık veriyle eğitilmiş, yalnızca kendi verimizle eğitilmiş, açık veriyle ön eğitilip kendi verimizle güncellenmiş.

### 10.5 Çalıştırma

Eğitim Python'da (pandas, scikit-learn, LightGBM, Jupyter) yapılır ve repoda `ml/` klasöründe tekrar üretilebilir not defterleri olarak tutulur. Model ONNX'e aktarılır ve .NET API içinde ONNX Runtime ile çalıştırılır. Ayrı bir Python servisi işletmeye gerek kalmaz. Model sürümü `ForecastLog.ModelVersion` ile izlenir.

### 10.6 LLM ince ayarı (kapsam dışı, gelecek çalışma)

Tek dönemlik takvimde (§15) bu adıma yer yoktur; aşağıdaki plan tezde gelecek çalışma olarak anlatılır.

Danışman "eğitim"le LLM'in kendisini kastediyorsa: deney boyunca biriken karar kayıtları ve gerekçeler, doğru aksiyonlarla etiketlenip küçük bir talimat veri setine dönüştürülür. `qwen3:4b` bu veriyle LoRA ince ayarından geçirilir (8 GB VRAM'e uygun boyut) ve Ollama'ya aktarılır. Etkisi §9.6'daki karar setinde, ince ayarsız modelle karşılaştırılarak ölçülür. Bu adım ana kapsamın dışındadır; tahmin modeli tamamlanmadan başlanmaz.

## 11. Web arayüzü

- Sera sayfasına **Saksılar** bölümü: her saksı için nem bant göstergesi (`BandGauge`), tahmin eğrisi ve "eşiğe düşme: ~2 sa" etiketi, son sulama, kontrol modu seçici (Manuel / Kural / LLM) ve "Şimdi sula" düğmesi.
- **Karar günlüğü:** zaman, saksı, kaynak (LLM / Kural / Kullanıcı), aksiyon, istenen ve onaylanan süre, politika sonucu ve gerekçe.
- **Saha notları:** açık öneri notları; ayar önerilerinde "Uygula" ve "Reddet" düğmeleri; her notta "neye dayanıyor" açılır alanı.
- Sulama geçmişi ve kullanılan su (ml, günlük toplam).
- Kart durumu: çevrimiçi/çevrimdışı, son görülme, firmware sürümü, depo durumu.
- Kart kayıt ve token gösterim diyaloğu (mevcut cihaz token diyaloğuyla aynı desen).
- Ayarlar ekranında etkin LLM sağlayıcısı, modeli ve tahmin modeli sürümü salt okunur gösterilir.

## 12. Deney tasarımı

Üç özdeş saksı (aynı bitki, toprak, saksı boyutu, yakın konum):

| Saksı | Kontrol | Amaç |
| --- | --- | --- |
| A | Kural motoru | Karşılaştırma tabanı |
| B | LLM + tahmin modeli | Önerilen sistem |
| C | LLM, tahmin aracı kapalı | Tahmin modelinin katkısını ayırmak |

İlk hafta sonunda modlar saksılar arasında döndürülür ve 1 hafta daha çalıştırılır (takvim tek döneme sığsın diye 2 + 2 haftadan kısaltıldı; süre kısalığı tezde sınırlılık olarak belirtilir); böylece saksının konumundan (ışık, sıcaklık) gelen fark sonuçlara karışmaz. Karşılaştırılanlar: bant içinde geçen süre, kullanılan toplam su, sulama sayısı, politika tarafından kırpılan/reddedilen karar sayısı ve öneri notlarının kabul oranı.

## 13. Dağıtım modları

| Mod | Nerede | LLM | Veri kaynağı | Kullanım |
| --- | --- | --- | --- | --- |
| Yerel demo | Masaüstü PC: `docker compose` (db, api, web) + host'ta Ollama | Ollama | ESP32 → `http://<PC-LAN-IP>:5010` | Bitirme sunumu ve saksı denemesi |
| Bulut | Azure App Service + Vercel (bugünkü kurulum) | Groq | Simülatör (+ isteğe bağlı HTTPS'li gerçek kart) | Portföy demosu |
| Uzak dönem (12 Kas – 12 Ara) | Azure App Service + Vercel | Groq (geliştirme sırasında Mac'te Ollama) | ESP32 → Azure, HTTPS | Düzenek bir arkadaşın evinde, geliştirici İzmir'de. Veri toplama ve kural modunda sulama; hiçbir bilgisayarın açık kalmasına bağlı değil. Yerinde işler (depo, bağlantı, firmware yükleme) arkadaş tarafından yapılır |

Azure'daki API, PC'deki Ollama'ya erişemez; LLM kararlı sulama bu yüzden yerel modda çalışır. Compose'a `api` servisi için `extra_hosts: host.docker.internal:host-gateway` eklenir. Simülatör yerel modda isteğe bağlı profile alınır, böylece gerçek veriyle karışmaz. Deney boyunca PC'nin açık kalması gerekir; PC kapanırsa kartlar 15 dk sonra yerel kural moduna geçer.

## 14. Başarı ölçütleri

| Ölçüt | Hedef |
| --- | --- |
| Veri tamlığı (beklenen okumaların ulaşan oranı) | En az %95 |
| Nem kontrolü (B saksısı) | Bekleme süreleri hariç, ölçümlerin en az %80'i bant içinde; A saksısından düşük değil |
| Su kullanımı | B saksısı A'dan fazla su kullanmıyor (raporlanır) |
| Sulama güvenliği | Deney boyunca sıfır kuru çalışma, sıfır taşma; cihaz katmanına ulaşan politika ihlali sıfır |
| LLM karar doğruluğu | Karar setinde en az %85 doğru aksiyon (yerel model) |
| Gerekçe doğruluğu | Karar ve notlarda araç çıktısında olmayan sayı yok |
| Tahmin modeli | 3 saatlik ufukta MAE taban modelden düşük; değer raporlanır |
| Öneri notları | Notların doğruluğu iki kişi tarafından puanlanır; kabul oranı raporlanır |
| LLM yanıt süresi | Yerel modelde karar başına p95 30 sn altında |
| Regresyon | Mevcut testler yeşil; yeni uç noktalar ve politika katmanı için birim ve entegrasyon testleri |

## 15. Fazlar

Takvim tek döneme sığar: teslim Ocak 2027 başı. 12 Kasım – 12 Aralık arası düzenek bir arkadaşın evinde, geliştiricinin uzağında çalışır (§13); 12 Aralık civarında saksılar geliştiricinin evine taşınır. Bu yüzden veri toplamayı başlatan işler 12 Kasım'dan önce biter. Donanımdan bağımsız işler (politika katmanı, açık veriyle tahmin modeli, karar ajanı) İzmir'de laptoptan sürer.

| Faz | Hedef tarih | İçerik | Bitti sayılır |
| --- | --- | --- | --- |
| 0 — Tezgah testi | 29 Eki – 7 Kas | Parça siparişi (bu hafta); tek nem sensörü + ESP32; kalibrasyon; mevcut `/api/ingest`'e mevcut cihaz tokenıyla POST | Panoda gerçek nem verisi görünüyor; backend değişikliği yok |
| 1 — İzleme ve veri toplama | 29 Eki – 15 Kas | `Node` + kanal modeli, toplu gönderim, NTP, tampon, rate limit düzeltmesi; `OpenAiCompatibleLlmClient` + Ollama; kart HTTPS ile Azure'a gönderir; 3 saksı arkadaşın evinde kurulur ve **veri toplama başlar** (elle sulama; sulama anları nemdeki sıçramadan çıkarılır); tarayıcıdan firmware kurulum sayfası | 3 saksı 48 saat kesintisiz; "Açıkla" yerel modelle çalışıyor |
| 2 — Sulama ve politika | 15 – 30 Kas | Pompa, röle, şamandıra; `Zone`, karar/komut/olay modeli; politika katmanı; ayar ve olay uç noktaları; kural motoru; firmware güvenlik katmanı | Kural modunda otomatik ve manuel sulama çalışıyor; tüm engel durumları test edildi |
| 3 — Tahmin modeli | Kasım (açık veri) – 12 Ara (kendi veri) | Açık veriyle yöntem geliştirme donanım beklemeden başlar; kendi verimizle eğitim; ONNX entegrasyonu; `ForecastJob` | Tahmin panoda; MAE raporlandı |
| 4 — LLM karar ajanı ve notlar | 1 – 14 Ara | Karar ajanı, öneri notları, değerlendirme setleri, arayüz | Karar ve soru setleri üç yapılandırmada çalıştırıldı |
| 5 — Deney | 15 – 28 Ara | 1 + 1 haftalık karşılaştırmalı deney (§12), masaüstünde yerel modda | §14 ölçütleri raporlandı |
| Tez | Sürekli; son hal 29 Ara – Ocak başı | Her fazın sonunda ilgili bölüm yazılır | Teslim |

LLM LoRA ince ayarı (§10.6) bu takvime sığmaz; tezde gelecek çalışma olarak yer alır.

Bahar dönemindeki zorunlu staj nedeniyle geliştirme Ocak ortasında kapanır. Deney kendi kendine çalışacak şekilde tasarlandığı için stajla çakışan kısmı panodan uzaktan izlenir; yalnızca depo doldurma için fiziksel müdahale gerekir.

## 16. Kararlar

| Karar | Seçim | Gerekçe |
| --- | --- | --- |
| Mikrodenetleyici | ESP32 (Arduino çatısı) | Wi-Fi dahili; Uno + Wi-Fi modülünden daha az kablo ve daha az sorun |
| LLM'in rolü | Sulama kararını verir ve öneri notu yazar; güvenlik sınırlarını kod belirler | Danışmanın istediği otonomi sağlanır; model hatası fiziksel zarara dönüşmez |
| Güvenlik | Sunucu politika katmanı + kartta tekrar denetim | Tek bir katmandaki hata pompayı sınırsız çalıştıramaz |
| LLM yedeği | Kural motoru | LLM veya PC kapalıyken saksılar susuz kalmaz; aynı zamanda karşılaştırma tabanı |
| Ayar değiştiren öneriler | Kullanıcı onayıyla | Sulama otomatik, sınır ve bant değişikliği insan onaylı |
| Downlink kanalı | HTTP ile ayar sorgulama | Broker işletmeye gerek yok; mevcut token doğrulaması kullanılır. MQTT sonraki adım |
| Yerel LLM entegrasyonu | Mevcut OpenAI uyumlu istemcinin genelleştirilmesi | Yeni istemci yazmadan Groq, Ollama ve benzerleri tek sınıfla desteklenir |
| Tahmin modelinin çalıştığı yer | Python'da eğitim, ONNX ile .NET içinde çıkarım | Ayrı servis yok; eğitim tarafı yine de Python ekosisteminde |
| "Eğitim"in kapsamı | Ana kapsam: veri setiyle eğitilmiş tahmin modeli; LLM ince ayarı isteğe bağlı | Tahmin modeli sınırlı veriyle bile ölçülebilir sonuç verir; LLM ince ayarı daha fazla etiketli veri ve zaman ister |
| Backend dili | **Önerilen:** .NET 10'da kalır | Backend tamamlanmış, test edilmiş ve dağıtılmış durumda. Bu PRD'deki işlerin hiçbiri dil değişikliği gerektirmiyor. Express'e geçiş; refresh token rotasyonu, EF global sorgu filtresiyle çok kiracılık, SignalR, rollup işleri, LLM araç döngüsü ve Testcontainers testlerinin yeniden yazılması demek. Bu haftalarca iş, kullanıcıya görünen yeni bir şey getirmiyor |

## 17. Riskler

| Risk | Önlem |
| --- | --- |
| LLM yanlış veya tutarsız karar verir | Politika ve cihaz katmanları; art arda reddedilen kararlarda kural moduna düşme; karar seti ile önceden ölçüm |
| LLM gerekçede sayı uydurur | Gerekçedeki sayılar araç çıktısıyla otomatik karşılaştırılır; eşleşmeyen gerekçe işaretlenir |
| Öneri notları gereksiz veya tekrarlı olur | Konu başına tek açık not, günlük üst sınır, kullanıcı geri bildirimi (kabul/ret) |
| Kendi verimiz model eğitmeye yetmez | Veri toplamanın Faz 1'de başlaması; açık veriyle ön eğitim; taban modelle dürüst karşılaştırma |
| Parçalar 12 Kasım'dan önce gelmez | Firmware ve kurulum sayfası donanımsız yazılıp derlenir; parçalar gelince kurulum arkadaş tarafından yapılır, tezgah testi uzaktan (pano + görüntülü görüşme) izlenir |
| Geliştirici düzeneğe uzaktan erişemez | Ayarlar sunucudan; firmware tarayıcıdan yüklenir; sürüm panoda doğrulanır; kart çevrimdışı uyarısı; Faz 2 pompa testleri boş saksıda ve görüntülü görüşmeyle |
| Azure ücretsiz katmanı gerçek kartın sürekli gönderimine yetmez (CPU kotası, uyku) | Gerçek kart bağlanınca kota izlenir; gerekirse ölçüm aralığı büyütülür veya B1 katmanına geçilir (öğrenci kredisiyle) |
| Düzenek arkadaşın evinden geliştiricinin evine taşınırken bitkiler etkilenir | Deney taşımadan sonra yeni bir uyum süresiyle başlar; uzak dönem verisi yalnızca model eğitiminde kullanılır |
| Açık verinin koşulları saksıdan farklı | Aktarım performansı ayrıca raporlanır; ana test her zaman kendi verimizde |
| Hatalı veya tutarsız nem sensörü | Yedek sensör; kanal bazında kalibrasyon; sabit değer ve ani sıçrama için sensör notu |
| Su elektronikle temas eder | Elektronik buatta ve saksı seviyesinin üstünde; hortum bağlantıları kelepçeli; ilk testler boş saksıda |
| Röle açılışta pompayı tetikler | Strapping olmayan pinler; açılışta pinler önce kapalıya çekilir; masada pompasız test |
| PC kapanır veya uyku moduna geçer | Kartta yerel kural moduna düşme; deney süresince uyku kapalı |
| Yerel model yavaş | `qwen3:4b` yedeği; 30 dk'lık karar döngüsü gecikmeyi tolere eder |
| Qwen3 düşünme metni cevaba karışır | `/no_think` veya `<think>` ayıklama; birim testi |
| Kapsam büyür (sera, MQTT, PCB, ince ayar) | Hedef dışı listesi sabit; ince ayar yalnızca Faz 6'da |

## 18. Açık sorular

- Danışman "veri setiyle eğitin" derken tahmin modelini mi, LLM'in ince ayarını mı kastediyor? Bu PRD ana kapsam olarak tahmin modelini, isteğe bağlı olarak LLM ince ayarını alıyor.
- LLM'in otomatik uygulayabileceği kararlar yalnızca sulama mı olsun, yoksa bant ayarları da onaysız değişebilsin mi? Bu PRD yalnızca sulamayı otomatik bırakıyor.
- Kaç saksı ve hangi bitki? Nem bantları bitkiye göre belirlenecek.
- Sunum yerel demo modunda mı yapılacak, yoksa bulut demosu da gösterilecek mi?
- Proje ekip çalışması mı? Firmware, ML ve backend işleri bölünecekse sorumlular bu dokümana eklenmeli.

## Kaynaklar

- Manipal University Jaipur (2023). [Soil Moisture, Air temperature, humidity, and Motor on/off Monitoring data](https://data.mendeley.com/datasets/fpdwmm7nrb/1). Mendeley Data. DOI: 10.17632/fpdwmm7nrb.1
- Belli, Davoli, Oddi, Preite, Galaverni, Ganino, Ferrari (2025). [Smart agriculture dataset in a tomato cultivation under different irrigation regimes](https://www.tlc.unipr.it/ferrari/Publications/Journals/Be_et_al_DiB25.pdf). *Data in Brief*, 60. Veri: Mendeley Data, DOI: 10.17632/35wh56287y.2
- [Environmental Sensor Dataset for LSTM-Based Predictive Irrigation in Smart Home Gardening Using Edge IoT](https://data.mendeley.com/datasets/jtsyb593fy/1). Mendeley Data (içerik henüz doğrulanmadı)
