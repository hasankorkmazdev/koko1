# GeoWar

Android ve iOS için harita tabanlı strateji oyunu. Oyuncu bir ülke seçer ve o ülkeyi yönetir: şehirlere fabrikalar kurar, ekonomiyi büyütür, ordusunu geliştirir.

> Durum: Unity projesi kuruldu ve telefonda çalışıyor. Ekonomi çekirdeği, tesis kurma ve seviye mantığı, bölge haritası, üst bar, il paneli, Tesis Kur, kaynak penceresi, Stok, Pazar ve isyan hazır. Enerji ve Diplomasi ekranları, şirketler, olaylar, diplomasi, AI, ordu ve savaş henüz koda geçmedi.

## Oyun Konsepti

- **Harita:** Dünya haritası; ülkeler ve ülkelere bağlı şehirler. Oyuncu haritada kaydırır/yakınlaştırır, ülke veya şehir seçer.
- **Ülke seçimi:** Oyun başında bir ülke seçilir. Her ülkenin başlangıç kaynakları, nüfusu ve şehirleri farklıdır.
- **Ekonomi:** Şehirlere fabrika kurulur (ör. silah, gıda, enerji, demir/çelik). Fabrikalar zamanla kaynak üretir; seviye atlatılabilir.
- **Ordu:** Birlik türleri (kara/hava/deniz), üretim, geliştirme (teknoloji/seviye) ve ileride savaş mekaniği.
- **Zaman:** Gerçek zamanlı veya tur tabanlı olacağı henüz belirlenmedi (bkz. Açık Kararlar).

## Teknoloji

- **Motor:** Unity (C#), 2D. Tek kod tabanı ile Android + iOS. Seçilme nedeni: yüksek hesaplama performansı (IL2CPP, gerektiğinde Burst ve Job System) ve Windows'tan iOS'a bulut derleme.
- **Harita:** Ülke/şehir verisi motordan bağımsız veri dosyalarında (JSON/ScriptableObject); harita çizimi bu veriden üretilir.
- **Veri odaklı tasarım:** Ülkeler, şehirler, fabrika türleri, birlikler ve maliyetler koda gömülmez; veri dosyalarından okunur. Denge ayarı kod değişikliği gerektirmemeli.
- **Kayıt:** Yerel kayıt (JSON) ile başla; bulut/hesap sistemi sonraya.

## Mimari İlkeler

- **Simülasyon ile görünümü ayır.** Oyun mantığı (ekonomi, üretim, savaş hesapları) saf C# sınıflarında olsun; UI/sahne nesnelerine bağımlı olmasın. Böylece test edilebilir ve ileride sunucuya taşınabilir.
- **Tek yönlü veri akışı:** Oyun durumu (GameState) -> UI. UI doğrudan state'i değiştirmez, komut/olay (command) gönderir.
- **Deterministik mantık:** Rastgelelik tek bir seed'li RNG üzerinden; aynı girdi aynı sonucu versin (test ve ileride çok oyunculu için).
- **Mobil performans:** Update döngüsünde ağır iş yapma, nesne havuzu (pooling) kullan, her karede allocation üretme. Hedef: orta seviye cihazlarda 60 FPS.
- **Hesaplama performansı öncelikli:** Ekonomi her karede değil oyun gününde bir hesaplanır. Veri düz dizilerde tutulur, hesaplar tam sayıyla yapılır, ülkeler karelere dağıtılır, AI seyrek karar verir. Performans iddiaları gerçek telefonda ölçülür.
- **Dokunmatik öncelikli UI:** Tüm etkileşimler tek parmak/çift parmak hareketiyle çalışmalı; küçük hedeflerden kaçın (min ~48dp).

## Önerilen Klasör Yapısı

```
GeoWar/
  CLAUDE.md
  docs/            # Tasarım dokümanları (GDD, denge tabloları)
  Assets/
    Scripts/
      Simulation/  # Oyun mantığı; Unity'ye bağlı değil
      Map/         # Harita verisi, üçgenleme, il bulma; Unity'ye bağlı değil
      Game/        # Unity tarafı: çizim, kamera, oyun döngüsü
      Game/UI/     # Arayüz (UI Toolkit); ekran kodla kurulur, stiller GeoWar.uss dosyasındadır
      Editor/      # Proje ayarları ve komut satırından derleme
    Scenes/        # Boş başlangıç sahnesi; gerisini GameBootstrap kurar
    Data/Resources/ # Ekonomi, arayüz (ui.json) ve harita verisi
    Art/Resources/ # Gölgelendiriciler, yazı tipleri, arayüz stilleri
  Tests/           # Unity'siz çalışan oyun mantığı testleri
  Tools/           # Ölçüm, harita üretimi, derleme kontrolü
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
- İstenen işin dışında fark ettiğin sorunları o an düzeltme; `docs/ISSUES.md` dosyasına yeni madde olarak yaz: sorun, nerede olduğu, nasıl görüldüğü. Kullanıcı listenin üstünden en sonda geçer, o istemeden maddeleri işleme. Düzelen veya geçersiz kalan maddeyi listeden sil. Dosya düz numaralı listedir, başlık ve bölüm içermez.

## Komutlar

```
# Testler:              dotnet run --project Tests/SimTests -c Release
# Hesap ölçümü:         dotnet run --project Tools/SimBenchmark -c Release
# Unity derleme kontrolü: dotnet build Tools/UnityCompileCheck -c Release
# Harita üretimi:       dotnet run --project Tools/MapBuilder -c Release -- <admin1.geojson> Assets/Data/Resources/map_region.bytes region
# Oyunu aç (Windows):   powershell -ExecutionPolicy Bypass -File Tools/Run.ps1
# Oyunu aç (telefon):   powershell -ExecutionPolicy Bypass -File Tools/Run.ps1 -Phone
# Unity:                "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
# Build (Android):      <Unity> -batchmode -nographics -projectPath <repo> -buildTarget Android -executeMethod GeoWar.EditorTools.GeoWarBuild.BuildAndroid -logFile <log>
# Build (Windows testi): <Unity> -batchmode -nographics -projectPath <repo> -buildTarget Win64 -executeMethod GeoWar.EditorTools.GeoWarBuild.BuildWindows -logFile <log>
# Otomatik Windows testi: Build/Windows/GeoWar.exe -geowarTestSeconds 22 -geowarScreenshot <png> -screen-width 390 -screen-height 844 -screen-fullscreen 0 -logFile <log>
# Dünya ölçeği testi:   aynı komuta -geowarPerfTest eklenir (200 ülke, 20.000 tesis, arayüzsüz)
# Telefon bağlantısı:   powershell -ExecutionPolicy Bypass -File Tools\AdbWifi.ps1 [-Pair]
# adb:                  <Unity>\..\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe
# Telefona kurma:       adb install -r Build/Android/GeoWar.apk
# Oyunu açma:           adb shell am start -n com.geowar.game/com.unity3d.player.UnityPlayerGameActivity
# Ölçüm satırları:      günlükte GeoWarStartup, GeoWarPerf, GeoWarUi (telefonda: adb logcat -s Unity:I, hatalar: adb logcat -s Unity:E)
# Build (iOS):          TODO, Android sürümü oturunca
```

## Açık Kararlar

- [x] Motor: Unity (2026-11-05).
- [ ] Gerçek zamanlı mı, tur tabanlı mı?
- [ ] Tek oyunculu mu, çok oyunculu (online) mi? (Çok oyunculu ise sunucu mimarisi baştan planlanmalı.)
- [ ] Harita: gerçek dünya haritası mı, kurgusal mı? Veri kaynağı (ör. Natural Earth)?
- [ ] Monetizasyon: reklam, uygulama içi satın alma, ücretli?
- [ ] Savaş mekaniği: otomatik hesaplama mı, oyuncunun yönettiği mi?
- [ ] Sanat stili: minimalist/vektör mü, detaylı mı?
