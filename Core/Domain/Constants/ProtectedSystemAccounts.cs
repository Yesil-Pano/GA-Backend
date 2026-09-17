using GA.Core.Domain.Entities;

namespace GA.Core.Domain.Constants
{
    /// <summary>Sistem tarafından korunan, UI/API üzerinden değiştirilemeyen hesaplar.</summary>
    public static class ProtectedSystemAccounts
    {
        public const string PrimarySuperAdminEmail = "admin@theobuz.com";

        public static readonly string PrimarySuperAdminEmailNormalized =
            PrimarySuperAdminEmail.ToLowerInvariant();

        public static bool IsProtectedEmail(string? email) =>
            !string.IsNullOrWhiteSpace(email)
            && string.Equals(email.Trim(), PrimarySuperAdminEmail, StringComparison.OrdinalIgnoreCase);

        public static void EnsureCanModify(User user)
        {
            if (IsProtectedEmail(user.Email))
                throw new InvalidOperationException(
                    "Sistem yöneticisi hesabı (admin@theobuz.com) değiştirilemez veya silinemez.");
        }

        public static void EnsureEmailNotReserved(string? email)
        {
            if (IsProtectedEmail(email))
                throw new InvalidOperationException(
                    "Bu e-posta adresi sistem yöneticisi hesabına ayrılmıştır.");
        }
    }
}
