using System.Collections.Generic;

namespace LoadTester
{
    /// <summary>
    /// Statik veri sağlayıcı sınıfı
    /// </summary>
    public static class StaticData
    {
        /// <summary>
        /// 1'den 10'a kadar olan sayıları yazı olarak döndürür
        /// </summary>
        public static readonly Dictionary<int, string> NumbersAsText = new Dictionary<int, string>
        {
            { 1, "bir" },
            { 2, "iki" },
            { 3, "üç" },
            { 4, "dört" },
            { 5, "beş" },
            { 6, "altı" },
            { 7, "yedi" },
            { 8, "sekiz" },
            { 9, "dokuz" },
            { 10, "on" }
        };

        /// <summary>
        /// Verilen sayıyı yazı olarak döndürür
        /// </summary>
        /// <param name="number">1-10 arası sayı</param>
        /// <returns>Sayının yazı karşılığı</returns>
        public static string GetNumberAsText(int number)
        {
            return NumbersAsText.TryGetValue(number, out var text) ? text : "bilinmeyen";
        }

        /// <summary>
        /// Tüm sayıları yazı olarak liste halinde döndürür
        /// </summary>
        /// <returns>1'den 10'a kadar sayıların yazı listesi</returns>
        public static List<string> GetAllNumbersAsText()
        {
            return new List<string>(NumbersAsText.Values);
        }

        /// <summary>
        /// Rastgele bir sayıyı yazı olarak döndürür
        /// </summary>
        /// <returns>1-10 arası rastgele sayının yazı karşılığı</returns>
        public static string GetRandomNumberAsText()
        {
            var randomNumber = Random.Shared.Next(1, 11);
            return GetNumberAsText(randomNumber);
        }
    }
}