namespace GA.Application.Features.WorkOrders
{
    public static class WorkOrderRecurrence
    {
        public static DateTime ComputeNextExecution(DateTime fromDate, string? interval)
        {
            return (interval ?? "").Trim().ToLowerInvariant() switch
            {
                "haftalik" or "weekly" => fromDate.AddDays(7),
                "aylik" or "monthly" => fromDate.AddMonths(1),
                "yillik" or "yearly" => fromDate.AddYears(1),
                _ => fromDate.AddMonths(1),
            };
        }

        public static TimeSpan ResolveDuration(DateTime startDate, DateTime endDate)
        {
            var duration = endDate - startDate;
            return duration > TimeSpan.Zero ? duration : TimeSpan.FromDays(1);
        }
    }
}
