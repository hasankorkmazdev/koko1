1. Para:
   1. Para vergiden ve pazarda yapılan satışlardan gelir.
   2. Vergi oranı şehir bazında veya tüm şehirler için toplu ayarlanabilir.
   3. Giderler:
      1. Ordu bakımı: Ordunun düzenli bir bakım gideri vardır; para ile ödenir.
2. Ticaret:
   1. Ticaret ortak pazarda yapılır.
   2. Her kaynağın bir piyasa fiyatı vardır.
   3. Ülkeler pazardan istedikleri zaman alıp satabilir.
   4. Fiyat arz ve talebe göre değişir:
      1. Her kaynağın veri dosyasında bir taban fiyatı vardır.
      2. Kaynak satın alındıkça fiyatı yükselir, satıldıkça düşer.
      3. Fiyat zamanla yavaşça taban fiyata geri döner.
   5. AI ülkeleri de aynı pazarda alıp satar.
   6. Yol ücreti:
      1. Pazardaki her satış teklifi bir ülkeye aittir.
      2. Yol ücretini alıcı öder; piyasa fiyatına eklenir.
      3. Yol ücreti alıcının başkenti ile satıcının başkenti arasındaki mesafeye göre hesaplanır.
   7. Başkenti alınan ülkenin ticareti askıya alınır; başkent geri alınınca ticaret tekrar açılır.
3. Sevkiyat:
   1. Şehirler arası sevkiyat yoktur; sevkiyat yalnızca ülkeler arası ticarette yapılır.
   2. Satın alınan mal alıcı ülkenin genel stoğuna gelir.
   3. Sevkiyat için ulaşım altyapısı gerekir: liman, havaalanı, karayolu, demiryolu.
   4. Tüm sevkiyat türlerinde (karayolu, demiryolu, deniz, hava) yol yalnızca kendi, tarafsız veya dost ülke bölgelerinden geçebilir; düşman bölgesinden geçilemez.
   5. Ulaşım türleri (karayolu temel alınır):
      1. Karayolu: hız 1x, kapasite 1x, maliyet 1x; komşu şehirler arasında her zaman vardır.
      2. Demiryolu: hız 1,5x, kapasite 4x, maliyet 0,5x; inşa edilmesi gerekir.
      3. Deniz: hız 0,5x, kapasite 10x, maliyet 0,25x; iki uçta da liman gerekir.
      4. Hava: hız 5x, kapasite 0,25x, maliyet 5x; iki uçta da havaalanı gerekir.
   6. Hız yolda geçen süreyi, kapasite bir seferde taşınan miktarı, maliyet taşıma ücreti çarpanını belirler.
   7. Liman ve havaalanı şehirde slot kaplayan binalardır.
4. Ekonomik olaylar:
   1. Olay tipleri:
      1. Doğa olayları:
         1. Kuraklık (ülke): Gıda üretimi düşer.
         2. Sel (şehir): Şehirdeki tesisler bir süre durur.
         3. Bereketli hasat (ülke): Gıda üretimi artar.
      2. İşçi olayları:
         1. Grev (ülke): Bir sektörde üretim durur.
         2. İş kazası (şehir): Bir tesis durur, memnuniyet düşer.
      3. Piyasa olayları:
         1. Talep patlaması (dünya): Bir kaynağın fiyatı yükselir.
         2. Arz fazlası (dünya): Bir kaynağın fiyatı düşer.
         3. Petrol krizi (dünya): Petrol ve yakıt pahalanır.
      4. Sanayi olayları:
         1. Tesis yangını (şehir): Bir tesis hasar görür, onarılana kadar çalışmaz.
   2. Piyasa dengesi:
      1. Fiyat taban fiyatın çok üstüne çıkarsa arz fazlası olayı gelir.
      2. Fiyat taban fiyatın çok altına inerse talep patlaması olayı gelir.
      3. Petrol krizi rastgele gelir.
   3. Olay sıklığı:
      1. Şehir ve ülke bazlı olayların sıklığı ülke başına hesaplanır, şehir sayısıyla artmaz.
      2. Sıklık ülkenin ekonomik index ve askeri index değerlerine bağlıdır; değerleri yüksek ülke daha çok olay yaşar.
   4. Olaylara tepki:
      1. Kötü ülke ve şehir olaylarında oyuncuya seçenek sunulur; cevap verilmezse "bekle" sayılır.
      2. Seçenekler:
         1. Kuraklık: Destek ödenir ve üretim düşüşü azalır veya beklenir.
         2. Sel: Para ödenir ve tesisler hemen açılır veya beklenir.
         3. Grev: Zam verilir (para ödenir, grev biter), bastırılır (grev biter, memnuniyet düşer) veya beklenir.
         4. İş kazası: Tazminat ödenir ve memnuniyet düşmez veya beklenir ve memnuniyet düşer.
         5. Tesis yangını: Para ve malzemeyle hemen onarılır veya beklenir; beklenirse tesis uzun süre durur.
      3. Bereketli hasat ve piyasa olayları otomatik işler.
      4. AI ülkeleri seçimini otomatik yapar.
5. Özel sektör:
   1. Şirketler tesisi kendiliğinden açmaz, şehirlerdeki boş slotlar için teklif getirir.
   2. Teklif şartları:
      1. Şehirde boş slot olmalı, vergi düşük ve memnuniyet yüksek olmalı.
      2. Çıkarma tesisi için şehirde o kaynak keşfedilmiş olmalı.
   3. Şirket, satış fiyatından girdi maliyetini çıkarıp en kârlı ürünü seçer.
   4. Birden fazla şehir uygunsa şirket kombina bonusu alacağı şehri seçer.
   5. Teklifte şehir, tesis, ürün ve toplam maliyet yazar.
   6. Oyuncu devlet payını seçer veya teklifi reddeder:
      1. %0: Yatırımı şirket öder, devlet yalnızca vergi alır.
      2. %25: Devlet yatırımın %25'ini öder, çıktının %25'i genel stoğa gider.
      3. %50: Devlet yatırımın %50'sini öder, çıktının %50'si genel stoğa gider.
   7. Şirket kâr edemeyeceği seçeneği sunmaz.
   8. Hazinenin veya şirketin parasının yetmediği seçenek kapalı olur; açık seçenek kalmazsa teklif gelmez.
   9. Teklif birkaç gün bekler; cevap verilmezse düşer.
   10. Teklif onaylansa, reddedilse veya düşse de belli bir süre (ör. 30 gün) ülkeye yeni teklif gelmez.
   11. AI ülkeleri teklifleri otomatik cevaplar.
   12. Şirket girdisini pazardan alır ve parasını kendisi öder; ülke stoğundan almaz.
   13. Şirket, çıktının devlet payı dışında kalan kısmını pazara satar.
   14. Şirketin pazardaki teklifi bulunduğu ülkeye ait sayılır.
   15. Ülke şirketlerden vergi alır.
   16. Şirketler sivil tesis açar; savunma sanayi ve nükleer santral devlete aittir.
   17. Şehir el değiştirince (fetih veya isyan):
       1. Şirket tesisini işletmeye devam eder, o tesisin vergisini şehrin yeni sahibine öder.
       2. Fetihte şirket tesislerinin bir kısmı kapanır (ör. %25) ve slotları boşalır; isyanda kapanmaz.
       3. Kalan tesisler birkaç gün üretim yapmaz, sonra normale döner.
       4. Şehrin memnuniyeti toparlanana kadar o şehir için yeni teklif gelmez.
       5. Şirket tesisindeki devlet payı şehrin yeni sahibine geçer.
   18. Şirketler:
       1. Her ülkede 4-5 şirket bulunur.
       2. Şirket adları kurgusaldır ve gerçek adları çağrıştırır (ör. Ford Endüstri yerine Kord Endüstri).
       3. Oyun başında şirketlerin tesisi olmaz, yalnızca parası olur.
       4. Başlangıç parası ülkenin zenginliğine göre belirlenir.
       5. Şirket satıştan kazanır; girdi, vergi ve yatırımı kendi kasasından öder.
       6. Parası biten şirket girdi alamaz ve tesisleri durur.
       7. Tesisleri duran şirket batar; tesisleri kapanır ve slotları boşalır.
       8. Batan şirketin yerine bir süre sonra yeni şirket kurulur.
