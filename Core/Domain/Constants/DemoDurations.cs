using System;

namespace GA.Core.Domain.Constants
{
    public static class DemoDurations
    {
        public const string OneWeek = "OneWeek";
        public const string FifteenDays = "FifteenDays";
        public const string OneMonth = "OneMonth";

        public static int? ToDays(string? key) => key?.Trim() switch
        {
            OneWeek or "7" or "BirHafta" => 7,
            FifteenDays or "15" or "OnBesGun" => 15,
            OneMonth or "30" or "BirAy" => 30,
            _ => null,
        };

        public static string Label(int days) => days switch
        {
            7 => "Bir Hafta",
            15 => "15 Gün",
            30 => "Bir Ay",
            _ => $"{days} Gün",
        };
    }
}
