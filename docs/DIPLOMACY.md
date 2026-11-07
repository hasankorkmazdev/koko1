1. Ülkeler arası üç ilişki durumu vardır: barış, savaş, ittifak.
2. Oyun başında ülkeler barış durumundadır. Tartışmalı toprağı olan ülke çiftleri savaşta başlar; liste veri dosyasındadır (`ui.json`, `StartingWars`).
3. Bir ülkeye saldırmak için önce savaş ilan edilir.
4. Savaştaki ülkeye barış teklif edilebilir; kabul ederse savaş biter.
   1. Barış yapıldıktan sonra belli bir süre (ör. 30 oyun günü) tekrar savaş ilan edilemez.
5. İttifak:
   1. Müttefikler birbirine saldıramaz.
   2. Müttefikler ortak düşmana karşı birlikte savaşır.
   3. İttifak bozulabilir; bozan ülkeyle diğer ülkelerin ilişki puanı düşer.
6. İlişki puanı:
   1. Her ülkenin diğer her ülkeye karşı -100 ile +100 arasında bir ilişki puanı vardır.
   2. Bir teklifin kabul edilip edilmemesi ilişki puanına bağlıdır.
   3. Olaylar (savaş, ticaret, ihanet, yardım) ilişki puanını değiştirir.
   4. İlişki puanı zamanla yavaşça doğal ilişki değerine geri döner.
7. Doğal ilişki:
   1. Her ülke çiftinin olumlu veya olumsuz bir doğal ilişki değeri vardır; veri dosyasında tanımlıdır (ör. Türkiye–Azerbaycan yüksek olumlu).
   2. Oyun başında ilişki puanı doğal ilişki değerinden başlar.
   3. Doğal ilişki değerleri tek bir ölçüte dayanır: tarihi ve kültürel bağlar, mevcut ittifaklar.
8. İlişki puanı ve doğal ilişki AI ülkeleri arasında da geçerlidir.
9. Çok güçlenen ülkeye karşı diğer ülkeler ittifak kurabilir.
