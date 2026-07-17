using System.Globalization;

namespace GA.Application.Features.Common
{
    /// <summary>
    /// UTC DateTime → Europe/Istanbul (UTC+3) gösterim.
    /// </summary>
    public static class TurkeyTime
    {
        private static readonly TimeZoneInfo Istanbul =
            ResolveIstanbulTimeZone();

        private static TimeZoneInfo ResolveIstanbulTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            catch (TimeZoneNotFoundException) { /* Windows */ }

            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "UTC+03",
                    TimeSpan.FromHours(3),
                    "UTC+03",
                    "UTC+03");
            }
        }

        public static string Format(DateTime? utc, string format = "yyyy-MM-dd HH:mm")
        {
            if (!utc.HasValue) return string.Empty;
            var dto = utc.Value.Kind switch
            {
                DateTimeKind.Utc => utc.Value,
                DateTimeKind.Local => utc.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc),
            };
            var local = TimeZoneInfo.ConvertTimeFromUtc(dto, Istanbul);
            return local.ToString(format, CultureInfo.InvariantCulture);
        }

        public static int? DurationMinutes(DateTime? startedAtUtc, DateTime? completedAtUtc)
        {
            if (!startedAtUtc.HasValue || !completedAtUtc.HasValue) return null;
            var mins = (int)Math.Round((completedAtUtc.Value - startedAtUtc.Value).TotalMinutes);
            return mins < 0 ? 0 : mins;
        }
    }
}
