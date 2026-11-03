# GeoWar — Oyun Özellikleri

Durum: Taslak. Netleşen kararlar ve açık sorular ayrı işaretlendi.

## 1. Genel

- Platform: Android + iOS (React Native / Expo düşünülüyor).
- Tek oyunculu, yapay zekaya (AI) karşı.
- Gerçek dünya haritası, bir harita kütüphanesi üzerinde.
- Oyuncu bir ülke seçer ve o ülkeyi yönetir.

## 2. Harita ve Bölgeler

- Gerçek harita (harita kütüphanesi ile; seçim açık, bkz. Açık Sorular).
- **Çevrimdışı çalışır:** Harita ve oyun verisi uygulamayla birlikte cihazda bulunur, internet gerekmez.
- **Başlangıç kapsamı:** Türkiye ve çevre ülkeler (Yunanistan, Bulgaristan, Gürcistan, Ermenistan, Azerbaycan/Nahçıvan, İran, Irak, Suriye vb.). Diğer ülkeler sonra eklenir.
- Her ülke **idari seviyelere (administrative level)** göre açılır: ülke -> bölge/eyalet -> şehir/ilçe. Hangi seviyenin "şehir" sayılacağı ülkeye göre veri dosyasında tanımlanır.
- Şehre tıklanınca şehir paneli açılır (kaynaklar, slotlar, binalar, depo, askerler).

## 3. Şehir

- Her şehirde **inşaat slotu** vardır. Slot sayısı yalnızca nüfusa göre değil, şehrin **gerçek ekonomik durumuna** (ör. GSYH/kişi başı gelir, sanayi ve tarım yoğunluğu) göre belirlenir. Veri dosyasında şehir başına hesaplanıp saklanır.
- Oyuncu boş slota bina inşa eder.
- Her şehirde **en az 1, en fazla 3 doğal kaynak** bulunur.
- Kaynaklar mümkün olduğunca **gerçek verilerle eşlenir** (ör. petrol, doğalgaz, kömür, demir, bakır, tarım arazisi, su). Veri kaynağı ve eşleme yöntemi açık (bkz. Açık Sorular).
- Şehirde asker tutulabilir (garnizon).

## 4. Binalar

Bina kategorileri:

| Kategori | Örnekler | Görev |
|---|---|---|
| Kaynak çıkarma | Maden, petrol kuyusu, tarla, çiftlik | Şehrin doğal kaynağından ham madde üretir |
| İşleme tesisi | Rafineri, çelik fabrikası, un fabrikası | Ham maddeyi ürüne dönüştürür |
| Gıda üretimi | Gıda tesisi, paketleme | Nüfus ve ordu için gıda üretir |
| Fabrika | Silah, mühimmat, araç fabrikası | Askeri/sivil mal üretir |
| Depo | Depo, silo, petrol tankı | Ürün stoklar |
| Askeri | Kışla, hava savunma, radar | Asker üretimi, savunma, görüş |

- Bina bir şehrin kaynağına bağlıysa (maden, kuyu vb.) yalnızca o kaynağa sahip şehirde kurulabilir.
- Binalar seviye atlatılabilir (kapasite/verim artar).
- İnşaat maliyet ve süre gerektirir; süre oyun zamanıyla akar.

## 5. Üretim Zinciri

- Üretim zinciri: **ham madde -> işleme -> ürün -> tüketim** (ör. demir cevheri -> çelik -> tank).
- Bir binanın girdileri ve çıktıları veri dosyasında tanımlıdır.
- Girdi yoksa bina üretim yapmaz (verimsiz bekler).

## 6. Envanter ve Depolar

- Ürünler **depolarda** tutulur; depo kapasitesi sınırlıdır.
- Üretilen ürün, üretildiği şehrin deposuna gider; depo doluysa üretim durur.
- Ürünler şehirler arası **sevk (nakliye)** edilir: oyuncu kaynak şehir, hedef şehir ve miktar seçer.
- Sevkiyat süresi mesafeye göre hesaplanır, yolda geçen süre oyun zamanına bağlıdır.
- Sevkiyat sırasında ürün hedefe varana kadar ne depoda ne kullanılabilir durumdadır.

## 7. Ordu

- Askerler (ve araçlar) **şehirlerde tutulur**; her birlik bir şehre bağlıdır.
- Birlikler kışlada üretilir; ürün (silah, gıda, yakıt) tüketir.
- Birlikler şehirler arasında hareket ettirilebilir.
- Savaş mekaniği ileride detaylandırılacak (bkz. Açık Sorular).

### Askeri Binalar
- **Hava savunma:** Şehri/bölgeyi hava saldırısına karşı korur.
- **Radar:** Menzil içindeki düşman hareketlerini ve saldırıları gösterir.
- **Kışla:** Asker üretir ve barındırır.

## 8. Zaman Sistemi

- Ne tur tabanlı ne de gerçek zamanlı: **oyun zamanı** vardır.
- Oyuncu zamanı **durdurabilir, hızlandırabilir** (ör. 1x, 2x, 4x...).
- Tüm süreler (inşaat, üretim, sevkiyat, hareket) oyun zamanıyla ilerler.
- Oyun duraklatılmışken oyuncu emir verebilir (inşaat, sevkiyat, birlik hareketi).

## 9. Yapay Zeka (AI)

- Rakip ülkeleri AI yönetir: bina kurar, üretir, ordu geliştirir.
- Zorluk seviyeleri (kolay/orta/zor): bkz. aşağıdaki "Zorluk Seviyeleri".
- AI de oyuncuyla aynı kurallara tabi olmalı (hile yapmaz), en azından varsayılan olarak.

### Zorluk Seviyeleri

Rakip ülkelerin gücü iki katmandan oluşur:

1. **Temel güç (ülke verisi):** Nüfus, şehirler, slotlar, kaynaklar ve başlangıç ordusu gerçek veriden gelir; her ülkede farklıdır. Zorluktan etkilenmez.
2. **Zorluk çarpanı:** Oyun başında seçilen zorluk, AI ülkelerinin belirli değerlerini çarpanla güçlendirir veya zayıflatır. **Oyuncunun ülkesine uygulanmaz.**

Tek bir "genel güç" çarpanı yerine, gücü oluşturan girdiler ayrı kanallarda çarpılır:

| Kanal | Etkilediği | Kolay | Orta | Zor |
|---|---|---|---|---|
| `production` | Binaların üretim hızı | %75 | %100 | %125 |
| `buildSpeed` | İnşaat ve asker üretim hızı | %80 | %100 | %115 |
| `startingResources` | Başlangıç stokları | %60 | %100 | %150 |
| `combat` | Birlik saldırı/savunma gücü (savaş eklenince) | %85 | %100 | %115 |

Değerler başlangıç önerisidir; denge testiyle ayarlanır.

Kurallar:
- **Orta = her şey %100.** AI oyuncuyla birebir aynı kurallarla oynar; yukarıdaki "hile yapmaz" varsayılanı korunur. Kolay ve zor bilinçli olarak bundan sapar.
- **Çarpan girdiye uygulanır, sonuca değil.** AI'ın ordusu doğrudan büyütülmez; daha hızlı ürettiği için daha büyük ordu kurar. AI'ın gücü oyun içinde görünür ve tutarlı kalır.
- **Kanallar birbirini katlar.** Üretim %125 ve inşaat hızı %115 birlikte, AI'ın toplam gelişme hızını %125'in üstüne çıkarır. Denge ayarında kanallar birlikte değerlendirilir.
- **Tam sayı yüzde olarak saklanır** (125 = 1.25x). Kayan nokta sapması olmaz, mantık deterministik kalır.
- Zorluk oyun başında seçilir ve **kayıt dosyasına yazılır**.
- İsteğe bağlı: belirli bir ülkeye o zorlukta ek çarpan verilebilir (varsayılanın üstüne yazar).
- İleride zorluk yalnızca çarpan değil, AI davranışını da (karar sıklığı, saldırganlık) ayarlayabilir; AI geliştirilirken eklenir.

Veri dosyası (`difficulty.json`; konumu motor kararından sonra belirlenir):

```json
{
  "difficulties": [
    { "id": "easy",   "aiModifiers": { "production": 75,  "buildSpeed": 80,  "startingResources": 60,  "combat": 85 } },
    { "id": "medium", "aiModifiers": { "production": 100, "buildSpeed": 100, "startingResources": 100, "combat": 100 } },
    { "id": "hard",   "aiModifiers": { "production": 125, "buildSpeed": 115, "startingResources": 150, "combat": 115 } }
  ],
  "countryOverrides": {
    "hard": { "<ULKE_KODU>": { "production": 140 } }
  }
}
```

Mantıkta uygulanışı:
- Oyun başlarken her ülkeye bir `CountryModifiers` atanır: AI ülkeleri zorluktan (ve varsa override'dan) gelen değerleri, oyuncunun ülkesi her kanalda %100 alır.
- Üretim, inşaat ve savaş hesapları çarpanı her zaman bu nesneden okur, ör. `üretim = temelÜretim × seviye × production / 100`. Hesaplarda "oyuncu mu, AI mı?" kontrolü yapılmaz; tek formül herkes için geçerlidir.
- İleride teknoloji, olaylar vb. de aynı modifier nesnesini kullanabilir.

## 10. Para Kazanma

- Gelir modeli **reklam**.
- Oyun akışını bozmayan yerleşim tercih edilir (ör. isteğe bağlı ödüllü reklam).

## 11. MVP Önerisi (ilk sürüm)

1. Gerçek harita (çevrimdışı), Türkiye ve çevre ülkeler, idari seviyeli şehirler
2. Şehir paneli, kaynaklar, boş slotlara inşaat
3. Kaynak çıkarma + işleme + depo + üretim zinciri
4. Oyun zamanı (durdur/hızlandır)
5. Sevkiyat
6. Asker üretimi ve şehirde tutma
7. Basit AI (sadece ekonomi)

Sonraya: hava savunma, radar, savaş, gelişmiş AI.

## 12. Açık Sorular

- **Harita kütüphanesi:** MapLibre (react-native), Mapbox, react-native-maps? Çevrimdışı çalışma gerekli mi?
- **Veri kaynakları:** İdari sınırlar (GADM, Natural Earth, OSM), gerçek kaynak verisi (USGS, FAO, Global Energy Monitor vb.)? Veri boyutu mobil için büyük olabilir.
- **Ekonomik veri:** Şehir/il düzeyinde ekonomik veri (GSYH, sanayi, tarım) çevre ülkeler için bulunabilir mi? Yoksa ülke düzeyi veriden tahmin mi edilir?
- **Çevre ülkelerin listesi:** Tam olarak hangi ülkeler dahil?
- **Nüfus ve ekonomi:** Nüfus, para, vergi, gıda tüketimi modele dahil mi?
- **Savaş:** Otomatik hesap mı, oyuncunun yönettiği mi? Şehir ele geçirme nasıl olur?
- **Sevkiyat:** Mesafe düz çizgi mi, gerçek yol mu? Nakliye kapasitesi/aracı var mı?
- **Kayıt:** Oyun kapalıyken oyun zamanı akar mı, yoksa durur mu?
- **Zorluk:** Oyun ortasında değiştirilebilir mi? Kolay/orta/zor dışında oyuncunun çarpanları kendisi ayarladığı "özel" mod olacak mı?
- **Reklam:** Hangi türler (ödüllü, geçiş, banner)? Ödüllü reklam karşılığında ne verilecek (hızlandırma, kaynak, inşaat süresi kısaltma)?
