namespace GeoWar.Game
{
    // Ekrandaki sayılar en fazla 3 basamaktır. Üstü kısaltılır ve eldekinden fazla göstermemek için yuvarlanmaz, kesilir.
    public static class NumberFormat
    {
        private const string Minus = "−";
        private const string Plus = "+";
        private const char DecimalSeparator = ',';
        private const string Vowels = "aıeioöuü";
        private const string SuffixVowels = "ııiiuüuü";

        private static readonly long[] Units = { 1000000000000L, 1000000000L, 1000000L, 1000L };
        private static readonly string[] UnitNames = { "Tn", "Mr", "Mn", "B" };

        public static string Compact(long value)
        {
            bool negative = value < 0;
            long size = negative ? -value : value;
            string sign = negative ? Minus : "";

            for (int i = 0; i < Units.Length; i++)
            {
                if (size < Units[i])
                {
                    continue;
                }

                long whole = size / Units[i];
                int decimals = whole < 10 ? 2 : whole < 100 ? 1 : 0;
                long scale = decimals == 2 ? 100 : decimals == 1 ? 10 : 1;
                long cut = size / (Units[i] / scale);
                return sign + WithDecimals(cut, scale) + " " + UnitNames[i];
            }

            return sign + size;
        }

        public static string Signed(long value)
        {
            return value < 0 ? Compact(value) : Plus + Compact(value);
        }

        // Binde birim miktarı en fazla 3 basamakla yazar: küçük günlük miktarlar ondalıkla görünür.
        public static string Amount(long milli)
        {
            const long unit = 1000;
            const long oneDecimalFrom = 10;
            const long noDecimalFrom = 100;

            bool negative = milli < 0;
            long size = negative ? -milli : milli;
            long whole = size / unit;
            string sign = negative ? Minus : "";

            if (whole >= noDecimalFrom)
            {
                return sign + Compact(whole);
            }

            return whole >= oneDecimalFrom
                ? sign + WithDecimals(size / (unit / 10), 10)
                : sign + WithDecimals(size / (unit / 100), 100);
        }

        // Günlük değişim: işaretiyle yazılır, artı da görünür.
        public static string SignedAmount(long milli)
        {
            return milli > 0 ? Plus + Amount(milli) : Amount(milli);
        }

        // Binde birim miktarı yazar: 1250 "1,25", 3000 "3" olur.
        public static string Milli(long value)
        {
            const long unit = 1000;
            const long hundredths = 10;
            return WithDecimals(value / hundredths, unit / hundredths);
        }

        // Türkçede i ve ı harfleri ayrı olduğu için dilden bağımsız çeviri yeterli değildir.
        public static string Lower(string text)
        {
            return text.Replace('I', 'ı').Replace('İ', 'i').ToLowerInvariant();
        }

        public static string Upper(string text)
        {
            return text.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();
        }

        // Özel ada kesme işaretiyle gelen ilgi eki; ünlüsü addaki son ünlüye uyar: Türkiye'nin, Irak'ın.
        public static string Genitive(string name)
        {
            string lower = Lower(name);
            char suffixVowel = SuffixVowels[0];
            for (int i = lower.Length - 1; i >= 0; i--)
            {
                int vowel = Vowels.IndexOf(lower[i]);
                if (vowel >= 0)
                {
                    suffixVowel = SuffixVowels[vowel];
                    break;
                }
            }

            bool endsWithVowel = Vowels.IndexOf(lower[lower.Length - 1]) >= 0;
            return name + "'" + (endsWithVowel ? "n" : "") + suffixVowel + "n";
        }

        private static string WithDecimals(long cut, long scale)
        {
            long whole = cut / scale;
            long fraction = cut % scale;
            if (fraction == 0)
            {
                return whole.ToString();
            }

            // Sondaki sıfır yazılmaz: 1,20 değil 1,2.
            if (scale == 100 && fraction % 10 == 0)
            {
                return whole.ToString() + DecimalSeparator + (fraction / 10);
            }

            string digits = fraction.ToString();
            if (scale == 100 && fraction < 10)
            {
                digits = "0" + digits;
            }

            return whole.ToString() + DecimalSeparator + digits;
        }
    }
}
