1. Oyunun amacı tüm ülkeleri ele geçirmek ve görevleri tamamlamak.
   1. Bir ülke, şehirlerinin %85'i alınınca düşer.
   2. Başkenti alınmışsa, şehirlerinin %75'i alınınca düşer.
   3. Ülke düşünce kalan şehirleri, ülkeyi düşüren şehri alan ülkeye geçer.
   4. Düşen ülkenin kalan hazinesi ve stoğu da bu ülkeye geçer.
2. Zaman:
   1. Oyun durdurulabilir.
   2. Oyun hızı: 1x, 2x, 3x.
   3. 1x hızda bir oyun günü 5 saniye sürer.
   4. Oyun kapalıyken zaman akmaz.
3. Şehir ele geçirme:
   1. Ordu düşman şehrine gönderilir.
   2. Ordu varınca savaş otomatik hesaplanır: ordunun gücü, şehrin savunmasıyla (garnizon + savunma binaları) karşılaştırılır.
   3. Saldıran kazanırsa şehri alır, kaybederse birlikleri kayıp verir.
4. Bina slotu:
   1. Her şehrin sınırlı sayıda bina slotu vardır.
   2. Slot sayısı şehrin nüfusu ve ülkenin zenginliğiyle birlikte belirlenir; sayılar denge aşamasında ayarlanır.
5. İnşaat maliyeti:
   1. Bina kurmak para ve malzeme (ör. çelik, çimento) gerektirir.
   2. Malzeme üretilebilir veya diğer ülkelerden satın alınabilir.
   3. İnşaat belli bir süre alır; süre oyun zamanıyla akar.
6. Bina türleri:
   1. Kaynak çıkarma: maden, petrol kuyusu, doğalgaz kuyusu, tarla
   2. Sanayi: çelik fabrikası, rafineri, çimento fabrikası, enerji tesisleri, ilaç fabrikası, kimya tesisi, makine fabrikası, elektronik fabrikası
   3. Savunma sanayi: her askeri ürün grubu için ayrı bina (ör. silah fabrikası, uçak fabrikası)
   4. Askeri: kışla, hava savunma, radar
   5. Ulaşım: liman, havaalanı
7. Kaynaklar: [RESOURCES.md](RESOURCES.md)
8. Diplomasi: [DIPLOMACY.md](DIPLOMACY.md)
9. Ekonomi: [ECONOMY.md](ECONOMY.md)
10. Memnuniyet:
    1. Her şehrin bir memnuniyet değeri vardır.
    2. Yüksek vergi memnuniyeti düşürür.
    3. Memnuniyet düşükse şehirde üretim yavaşlar.
    4. Memnuniyeti eşiğin altına düşen şehirde her gün isyan çıkma ihtimali vardır; memnuniyet düştükçe ihtimal artar.
    5. Şehir el değiştirince memnuniyeti düşük başlar.
    6. Oyuncunun şehirlerinin rengi memnuniyet düştükçe yavaş yavaş kırmızıya döner.
11. İsyan:
    1. İsyan çıkınca şehirde üretim durur.
    2. Bir ülkede aynı anda çıkan isyan sayısı sınırlıdır.
    3. İsyan komşu şehirleri de etkiler; komşu şehirlerin memnuniyeti düşer.
    4. Oyuncuya isyanı bitirmek için seçenekler sunulur:
       1. Taviz: Yalnızca isyan eden şehre uygulanır; xx gün vergi %0 ve üretim %50 düşük.
       2. Askeri bastırma: Şehre asker gönderilir; isyan hemen biter ama asker kaybedilir ve memnuniyet daha da düşer.
       3. Yardım paketi: Para ödenir, memnuniyet hemen yükselir.
    5. Aynı anda süren isyanlar tek pencerede toplanır; hepsi tek seçimle çözülebilir.
    6. Pencere yalnızca ilk isyanda kendiliğinden açılır ve oyunu durdurur; sonraki isyanlar ve şehir kayıpları oyunu durdurmaz.
    7. İsyan çözülünce şehir belli bir süre yeniden isyan etmez.
    8. AI ülkesi isyana hemen cevap verir: parası yetiyorsa yardım paketi, yetmiyorsa taviz.
    9. Oyuncu belli bir süre içinde seçenek seçmezse şehir elden çıkar ve isyancı bir devlet oluşur.
    10. Bir ülkeden en fazla bir isyancı devlet çıkar; o ülkede isyanla kaybedilen her şehir, komşu olmasa da bu devlete katılır.
    11. İsyancı devleti AI yönetir.
    12. İsyancı devletin adı isyanın çıktığı ülkeye göre verilir: "<Ülke> İşçi Emekçi Devleti" (ör. Türkiye İşçi Emekçi Devleti).
    13. İsyancı devlet kurulunca ona geçenler:
       1. Ülkenin kaynaklarının belli bir kısmı
       2. Askerlerin belli bir kısmı
       3. Şehrin nüfusunun tamamı
    14. Başkent isyancı devlete geçerse ülkenin kalan en kalabalık şehri yeni başkent olur.
12. Nüfus ve gıda:
    1. Şehirler nüfuslarına göre gıda tüketir.
    2. Gıda yetmezse memnuniyet düşer.
    3. Nüfus artış oranı:
       1. Her ülkenin taban artış oranı gerçek veriden gelir; ülkeler arasındaki fark korunur.
       2. Taban oran bir oyun çarpanıyla hızlandırılır; çarpan denge aşamasında belirlenir.
       3. Gıda fazlası artışı hızlandırır, gıda açığı nüfusu azaltır.
       4. Savaş yaşanan şehirde nüfus azalır.
    4. Nüfus arttıkça vergi geliri artar.
    5. Halk tüketimi:
       1. Nüfus gıda dışında ilaç, yakıt ve elektronik de tüketir.
       2. Bu ihtiyaçlar karşılanırsa memnuniyet ve vergi geliri artar, karşılanmazsa düşer.
13. Üretim:
    1. Girdisi eksik olan bina üretim yapmaz ve alarm durumuna geçer.
    2. Fabrikalar makineye ihtiyaç duyar.
    3. Makine üretilebilir veya pazardan satın alınabilir.
       1. Makinenin bir ömrü vardır.
       2. Ömrü dolan makine çöp olur.
       3. Makinesi olmayan fabrika yeni makine gelene kadar üretim yapmaz ve alarm durumuna geçer.
    4. Fabrikalar makine dışında yan malzemelere de ihtiyaç duyar.
       1. Her fabrika, ne üretirse üretsin, çalıştığı her gün sarf malzemesi harcar.
    5. Birden fazla ürün üretebilen binada oyuncu ne üretileceğini seçer.
    6. Kombina bonusu:
       1. Biri diğerine girdi sağlayan iki fabrika aynı şehirde olursa bağlantı başına +%2,5 verim kazanır.
       2. Bağlantılar reçetelerden otomatik çıkar.
14. Stok:
    1. Her ülkenin tek bir genel stoğu vardır; devlet tesislerinin üretimi ve şirket tesislerindeki devlet payı buraya gider.
    2. Binalar girdilerini genel stoktan alır.
    3. Stoğun kapasite sınırı yoktur.
    4. Şehir ele geçirilince, şehrin ülke üretimindeki payı kadar stok yeni sahibine geçer (ör. şehir üretimin %10'unu yapıyorsa stoğun %10'u).
15. Enerji: [ENERGY.md](ENERGY.md)
16. Şehir ve tesis adları düzenlenebilir.
17. Her ülkenin bir ekonomik index ve bir askeri index değeri vardır.
18. Tesis seviyesi:
    1. Her tesisin kendi deneyimi ve seviyesi vardır.
    2. Yeni tesis seviye 1'de ve sıfır deneyimle başlar.
    3. Tesis ne kadar çalışırsa o kadar deneyim kazanır (ör. %60 hızla çalışıyorsa deneyimi de %60 hızla artar); duran tesis kazanmaz.
    4. Deneyim dolunca sonraki seviye açılır.
    5. Yükseltme (AR-GE) para, malzeme ve süre ister; maliyet seviye yükseldikçe artar.
    6. Yükseltme tek tek veya toplu yapılır (ör. hazır olan tüm çelik fabrikaları).
    7. Seviye başına üretim hızı artar (ör. +%25); girdi ve enerji tüketimi de aynı oranda artar.
    8. Şirket kendi tesisini kendi parasıyla otomatik yükseltir; devlet payı değişmez.
    9. Ele geçirilen tesis seviyesini ve deneyimini korur.
    10. Tesis kapanırsa seviyesi ve deneyimi de gider.
19. Şehir sayısı:
    1. Her ülkenin şehir sayısı büyüklüğüne ve nüfusuna göre belirlenir.
    2. Her ülkenin hangi idari seviyeyi şehir sayacağı veri dosyasında tanımlanır.
    3. Birimleri çok küçük olan ülkelerde küçük birimler birleştirilir (ör. Azerbaycan'ın rayonları).
    4. Birimleri çok büyük olan ülkelerde birimler daha ayrıntılı bir veri kaynağıyla bölünür.
