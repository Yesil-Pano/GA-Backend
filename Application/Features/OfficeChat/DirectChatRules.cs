using GA.Application.Features.Partners;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;

namespace GA.Application.Features.OfficeChat
{
    public static class DirectChatRules
    {
        public const string GaManagementBadge = "GA Yönetim";

        public static bool IsGaManagementUser(User user, IReadOnlyCollection<string> roleNames)
        {
            if (user.TenantId == Guid.Empty)
                return true;
            if (string.Equals(user.Email, "admin@theobuz.com", StringComparison.OrdinalIgnoreCase))
                return true;
            return roleNames.Any(r =>
                string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase));
        }

        public static bool CanUsersChat(
            Guid meId,
            Guid targetId,
            Guid meTenantId,
            Guid targetTenantId,
            bool meIsGaManagement,
            bool targetIsGaManagement)
        {
            if (meId == targetId) return false;
            if (meIsGaManagement || targetIsGaManagement) return true;
            if (meTenantId == Guid.Empty || targetTenantId == Guid.Empty) return false;
            return meTenantId == targetTenantId;
        }

        public static bool UserMatchesPartnerFilter(
            User user,
            PartnerDefinition? partnerFilter,
            IReadOnlyCollection<string> projectNames)
        {
            if (partnerFilter == null) return true;
            return PartnerCatalog.MatchesTeam(partnerFilter, user.TenantId, projectNames);
        }
    }
}
