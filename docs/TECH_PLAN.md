1. Altyapı:
   1. Oyun mantığı Unity'den bağımsız C# kütüphanesi olur: Assets/Scripts/Simulation.
   2. Denge ve reçete verileri Assets/Data içindeki JSON dosyalarında durur.
   3. Testler ve ölçüm araçları Unity'yi açmadan .NET ile çalışır: Tests, Tools.
   4. Unity projesi repo kökünde, Unity 6000.3.11f1 ile kurulur.
2. Ekonomi çekirdeği:
   1. Reçeteler, stok, enerji, memnuniyet, nüfus, vergi ve pazar günlük hesaplanır.
   2. Veri düz dizilerde tutulur, hesaplar tam sayıyla yapılır, her gün için bellek ayrılmaz.
   3. Aynı seed aynı sonucu verir.
   4. Hesap ülke başına çağrılabilir, böylece ülkeler karelere dağıtılabilir.
3. Performans bütçesi:
   1. 1x hızda bir gün 5 saniyedir (economy.json, SecondsPerDay); 3x hızda saniyede 0,6 gün hesaplanır. Ölçümler daha zorlu olan saniyede 3 günle yapıldı.
   2. Orta seviye telefonda bir gün, ülkelere dağıtıldığında kareyi bozmamalı; hedef 60 FPS.
   3. Ölçüm önce bilgisayarda, sonra gerçek telefonda yapılır.
   4. İlk bilgisayar ölçümü (tek çekirdek, Unity dışında): 20.000 tesiste gün başına 0,87 ms, 160.000 tesiste 4,08 ms, gün içinde bellek ayırma yok.
   5. Bu ölçümde tesis kurma, şirket kasası, AI'ın gerçek kararları, ordu ve savaş henüz yok.
   6. Unity içinde ilk ölçüm (Windows, Mono, 20.000 tesis, 102.522 köşe): 60 FPS sınırında, en yavaş kare 16,9 ms, gün 1,2-1,3 ms, karede hesap en kötü 0,14 ms, başlatma kodu 38 ms. Bu telefon sonucu değil.
   7. Telefon ölçümü, arayüzsüz (POCO F5 Pro, IL2CPP, 200 ülke, 20.000 tesis, yaklaşık 20 saniye): 60 FPS, kaçan kare yok, gün 2,7-4,8 ms, karede hesap en kötü 0,79 ms.
   8. Telefon ölçümü, arayüzle (9 ülke, 301.330 köşe, 178.053 üçgen, yaklaşık 2 dakika): 60 FPS, dokunulmazken kaçan kare çok nadir (5 saniyelik 23 dilimin birinde 3 kare), kaydırırken 5 saniyede bir kare 25-34 ms, başlatma kodu yaklaşık 145 ms, bellek yaklaşık 332 MB, ısınma uyarısı yok.
   9. Bu telefon üst sınıf bir işlemciye sahip (Snapdragon 8+ Gen 1); orta seviye telefon ve uzun süreli kullanım henüz ölçülmedi.
   10. Panel ve Tesis Kur ile 3 dakikalık elle kullanım: 60 FPS, 0 hata; dokunurken 5 saniyede 1-5 kare 25-58 ms. Bellek 332 MB'tan 507 MB'a çıktı, 329 MB'ı sınıflandırılamayan bölümde. Sızıntı olup olmadığı henüz bilinmiyor, ölçülecek.
4. Harita:
   1. İller veri dosyasından üçgenlenip tek mesh olarak çizilir.
   2. Kaydırma ve yakınlaştırma sadece kamerayı hareket ettirir.
   3. Uzakta ülkeler, yakında iller görünür.
   4. İl seçimi noktanın hangi ilde olduğunu hesaplayarak yapılır.
   5. Her ülkenin hangi idari seviyeyi şehir sayacağı veri dosyasında tanımlanır.
   6. Türkiye ve komşularının dolgusu 102.522 köşe, 78.649 üçgen tutar; iller ham detayda, arka plan karası 0,04 derecelik ızgarada. Çizgilerle birlikte toplam 301.330 köşedir.
   7. Tüm dünya aynı detayda 907.317 köşe, 614.624 üçgen tutuyor; telefonda ayrıntı seviyesi şart, sınır çizgileri de sadeleştirilmeli.
   8. 18 ülke ve 376 şehirle dolgu 99.571 köşe, 90.838 üçgen; çizgilerle toplam 411.755 köşe, 246.930 üçgen. Rusya 133.158 ham köşeden 0,02 derecelik sadeleştirmeyle yaklaşık 21.600 köşeye indi; komşu iller ortak sınırda aynı köşeleri kullanır (`Tools/Shared/RingSimplifier.cs`). Bu harita telefonda henüz ölçülmedi.
   9. Sınır politikası: tek kaynak Natural Earth, fiili kontrol geçerlidir. Kırım Rusya'da, Golan'ın çoğu ve Kudüs'ün tamamı İsrail'de, Kuzey Kıbrıs ayrı ülkedir (KKTC). Akrotiri ve Dhekelia Kıbrıs'ın illerine katılır. İsrail'in başkenti Tel Aviv'dir.
   10. Toprağı tartışmalı ülke çiftleri savaşta başlar (`ui.json`, `StartingWars`): İsrail-Filistin, İsrail-Suriye, İsrail-Lübnan. Ukrayna oyunda olmadığı için Kırım'ın savaşı yoktur.
5. Arayüz sırası (UI Toolkit, ölçüler onaylanan ekran tasarımından):
   1. Üst bar, hız kutusu, uyarı şeridi, alt şerit, renk açıklaması ve harita görünümü: hazır; düğmelerin açacağı pencereler sonraki adımlarda.
   2. İl paneli, ülke paneli ve Tesis Kur: hazır; Kaynak ara ve diplomasi düğmeleri henüz bir şey yapmaz.
      1. Vergi ayarı: il panelindeki kutudan açılan pencere; seçilen oran 1 Ocak'ta yürürlüğe girer, tek düğme aynı kararı tüm şehirlere yazar. Takvim simülasyondadır (`WorldState.StartDate`), başlangıç tarihi `ui.json`'dan gelir.
      2. Onay bekleyen varsayımlar: oran %0-100 arasında 5'er 5'er değişir (economy.json), başlangıç %20; karar 1 Ocak'a kadar değiştirilebilir; şehir el değiştirince yürürlükteki oran şehirle gider, eski sahibin kararı düşer; tavizdeki şehirde vergi alınmaz, süre bitince yürürlükteki oran geri gelir; AI vergisini değiştirmez.
   3. Kaynak penceresi, raptiyeyle üst bara taşıma ve kaynak kartından açılan bilgi penceresi (fiyat, reçete, kullanım yerleri, şehir bazında üretim): hazır.
      1. Stok ekranı (kaç gün yeteceği, azalan kaynaklar) ve Pazar ekranı (fiyat, elle alım satım): hazır.
      2. Pazar ülkelerin sınırlı teklifleriyle çalışır; yol ücreti başkentler arası mesafeden hesaplanır.
      3. Sevkiyat: karayolu, deniz ve hava çalışır; rota, süre, kapasite ve yoldaki mallar koda geçti. Demiryolu kararla şimdilik yoktur.
      4. Hava rotasının geçtiği ülkeler harita ile oyun başında hesaplanır: ülke sayısının küpü kadar yer tutar, tüm dünya haritasında yöntem değişmelidir.
      5. Tesis Kur'daki "Eksiği al" düğmesi yeni pazar kuralına uymadığı için şimdilik kaldırıldı.
   4. Şirket teklifi ve olay pencereleri. İsyan penceresi hazır; askeri bastırma düğmesi ordu gelene kadar kapalıdır.
   5. Enerji ve Diplomasi.
   6. Ana menü, ülke seçimi, kayıt ve ayarlar.
6. Mekaniklerin koda geçiş sırası:
   1. Tesis kurma, inşaat süresi, kapatma, seviye, deneyim ve kombina: mantığı hazır, testli. Ekranda kapatma ve ürün seçimi henüz yok.
      1. Slot sayısı oyun başında bir kez hesaplanır: 3 + nüfus / 1,5 milyon, en çok 14. Ülkenin zenginliği henüz katılmadı.
      2. Tesisler slot dizisinde sabit durur; boş slot bellek ve hesap maliyeti getirmez.
      3. Onay bekleyen varsayımlar: AR-GE sırasında tesis çalışır, en yüksek seviye 5, yükseltme bedeli kurulum bedelinin seviye başına %50'si, devlet şirket tesisini kapatamaz, liman kıyı ister.
   2. Şirketler ve kasaları.
   3. Olaylar ve isyan: isyan mantığı ve şehir sahipliği hazır, testli; olaylar henüz yok.
      1. Şehirler dizide sabit durur, sahibi CityOwner'dan okunur; ülkenin şehir ve slot listeleri yalnızca şehir el değiştirince yeniden kurulur. Fetih de bunu kullanacak.
      2. İsyancı devletler için dizilerin sonunda boş ülke yerleri ayrılır (economy.json, RebelStateSlots). Yer kalmazsa isyan söner, şehir ülkede kalır.
      3. Onay bekleyen varsayımlar: isyancı devlete şehrin nüfus payı kadar stok ve şebeke kapasitesi geçer, para geçmez; isyancı devlet ana ülkeyle savaşta başlar ve mesafeleri ana ülkeden kopyalanır; hazine vergilerden dolar.
      4. Ordu olmadığı için askerlerin geçmesi ve şehri geri alma yoktur. AI yalnızca pazarda alım satım yapar ve isyana cevap verir.
      5. Onay bekleyen varsayım: isyan penceresi kendiliğinden açıldıktan sonra 30 gün yeniden açılmaz (economy.json, RebellionAlertGapDays). Bu süre olmazsa kıtlıkta cevap verilen isyanın yerine hemen yenisi çıkar ve pencere arka arkaya açılır.
      6. Kırmızı ton 16 kademelidir ve memnuniyet %40'ın altına inince başlar; renkler yalnızca kademe değişince gönderilir.
   4. Diplomasi.
   5. AI kararları.
   6. Ordu ve savaş, tasarımı bitince.
7. Telefon:
   1. APK üretiliyor (IL2CPP, ARM64, 26,4 MB, motor kodu kırpması kapalı); üst sınıf telefonda çalıştı, orta seviye telefonda ölçülecek.
   2. Sonuçlar hedefi tutmazsa önce Burst ve Job System devreye alınır.
   3. iOS, Android sürümü oturduktan sonra Unity Build Automation ile eklenir.
