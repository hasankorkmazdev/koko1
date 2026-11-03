# GeoWar

Android ve iOS için harita tabanlı strateji oyunu. Oyuncu bir ülke seçer ve o ülkeyi yönetir: şehirlere fabrikalar kurar, ekonomiyi büyütür, ordusunu geliştirir.

> Durum: Proje başlangıç aşamasında. Henüz kod yok. Aşağıdaki "Teknoloji" bölümü **varsayılan öneridir**; karar kesinleşince güncelle.

## Oyun Konsepti

- **Harita:** Dünya haritası; ülkeler ve ülkelere bağlı şehirler. Oyuncu haritada kaydırır/yakınlaştırır, ülke veya şehir seçer.
- **Ülke seçimi:** Oyun başında bir ülke seçilir. Her ülkenin başlangıç kaynakları, nüfusu ve şehirleri farklıdır.
- **Ekonomi:** Şehirlere fabrika kurulur (ör. silah, gıda, enerji, demir/çelik). Fabrikalar zamanla kaynak üretir; seviye atlatılabilir.
- **Ordu:** Birlik türleri (kara/hava/deniz), üretim, geliştirme (teknoloji/seviye) ve ileride savaş mekaniği.
- **Zaman:** Gerçek zamanlı veya tur tabanlı olacağı henüz belirlenmedi (bkz. Açık Kararlar).

## Teknoloji (varsayılan öneri)

- **Motor:** Unity (C#), 2D. Alternatif: Godot 4. Tek kod tabanı ile Android + iOS.
- **Harita:** Ülke/şehir verisi motordan bağımsız veri dosyalarında (JSON/ScriptableObject); harita çizimi bu veriden üretilir.
- **Veri odaklı tasarım:** Ülkeler, şehirler, fabrika türleri, birlikler ve maliyetler koda gömülmez; veri dosyalarından okunur. Denge ayarı kod değişikliği gerektirmemeli.
- **Kayıt:** Yerel kayıt (JSON) ile başla; bulut/hesap sistemi sonraya.

## Mimari İlkeler

- **Simülasyon ile görünümü ayır.** Oyun mantığı (ekonomi, üretim, savaş hesapları) saf C# sınıflarında olsun; UI/sahne nesnelerine bağımlı olmasın. Böylece test edilebilir ve ileride sunucuya taşınabilir.
- **Tek yönlü veri akışı:** Oyun durumu (GameState) -> UI. UI doğrudan state'i değiştirmez, komut/olay (command) gönderir.
- **Deterministik mantık:** Rastgelelik tek bir seed'li RNG üzerinden; aynı girdi aynı sonucu versin (test ve ileride çok oyunculu için).
- **Mobil performans:** Update döngüsünde ağır iş yapma, nesne havuzu (pooling) kullan, her karede allocation üretme. Hedef: orta seviye cihazlarda 60 FPS.
- **Dokunmatik öncelikli UI:** Tüm etkileşimler tek parmak/çift parmak hareketiyle çalışmalı; küçük hedeflerden kaçın (min ~48dp).

## Önerilen Klasör Yapısı

```
GeoWar/
  CLAUDE.md
  docs/            # Tasarım dokümanları (GDD, denge tabloları)
  Assets/
    Scripts/
      Core/        # Oyun durumu, komutlar, zaman
      Economy/     # Fabrikalar, kaynaklar, üretim
      Military/    # Birlikler, geliştirmeler, savaş
      Map/         # Ülke/şehir verisi, harita etkileşimi
      UI/
    Data/          # Ülke, şehir, fabrika, birlik tanımları
    Art/
  Tests/
```

## Kod Standartları

- Kod, değişken ve sınıf adları **İngilizce**; yorumlar ve dokümanlar Türkçe olabilir.
- C# adlandırma: sınıf/metot `PascalCase`, alan `_camelCase`, yerel değişken `camelCase`.
- Magic number kullanma; sabitleri veya veri dosyalarını kullan.
- Küçük, tek sorumluluklu sınıflar. Oyun mantığı sınıfları için birim test yaz.
- Yeni özellik eklerken önce ilgili veri yapısını ve mantığı, sonra UI'ı yaz.

## Çalışma Kuralları (Claude için)

- Büyük bir değişiklikten önce kısa bir plan sun; mimariyi değiştiren kararları sormadan verme.
- Mevcut yapıya uy; istenmeyen refactor yapma.
- Denge değerlerini (maliyet, üretim hızı, güç) koda değil veri dosyalarına koy.
- Her mantık değişikliğinde ilgili testi ekle/güncelle ve çalıştır.
- Platforma özel kod (Android/iOS) ayrı tutulur ve `#if UNITY_ANDROID` / `#if UNITY_IOS` ile sınırlandırılır.
- Gerçek ülke/sınır verisi kullanırken siyasi hassasiyetleri göz önünde bulundur (tartışmalı bölgeler için tek bir kaynak ve açık bir politika belirle).

## Komutlar

Motor kesinleşince doldurulacak:

```
# Build (Android):   TODO
# Build (iOS):       TODO
# Testler:           TODO
```

## Açık Kararlar

- [ ] Motor: Unity mi, Godot mu?
- [ ] Gerçek zamanlı mı, tur tabanlı mı?
- [ ] Tek oyunculu mu, çok oyunculu (online) mi? (Çok oyunculu ise sunucu mimarisi baştan planlanmalı.)
- [ ] Harita: gerçek dünya haritası mı, kurgusal mı? Veri kaynağı (ör. Natural Earth)?
- [ ] Monetizasyon: reklam, uygulama içi satın alma, ücretli?
- [ ] Savaş mekaniği: otomatik hesaplama mı, oyuncunun yönettiği mi?
- [ ] Sanat stili: minimalist/vektör mü, detaylı mı?
