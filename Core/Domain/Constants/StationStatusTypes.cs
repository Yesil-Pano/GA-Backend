using System;

namespace GA.Core.Domain.Constants
{
    public static class StationStatusTypes
    {
        public const string BakimaDahil = "Bakıma Dahil";
        public const string BakimDisi = "Bakım Dışı";

        public static readonly string[] All = [BakimaDahil, BakimDisi];

        public static bool IsValid(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            (string.Equals(value.Trim(), BakimaDahil, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(value.Trim(), BakimDisi, StringComparison.OrdinalIgnoreCase));

        public static string NormalizeOrDefault(string? value, string fallback = BakimaDahil)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var v = value.Trim();
            if (string.Equals(v, BakimaDahil, StringComparison.OrdinalIgnoreCase)) return BakimaDahil;
            if (string.Equals(v, BakimDisi, StringComparison.OrdinalIgnoreCase)) return BakimDisi;
            return fallback;
        }
    }
}
