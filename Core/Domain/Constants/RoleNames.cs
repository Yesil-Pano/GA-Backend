namespace GA.Core.Domain.Constants
{
    public static class RoleNames
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string TenantAdmin = "TenantAdmin";
        public const string OfficeUser = "OfficeUser";
        public const string FieldWorker = "FieldWorker";
        /// <summary>Yalnızca arıza açabilir; ekibe atayamaz.</summary>
        public const string OperationReporter = "OperationReporter";
        /// <summary>ISG fotoğraflarını görür; operasyon fotoğraflarını görmez.</summary>
        public const string IsgInspector = "IsgInspector";

        /// <summary>Ofis sohbet gelen kutusuna erişebilen roller.</summary>
        public static readonly HashSet<string> OfficeChatRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            SuperAdmin,
            TenantAdmin,
            OfficeUser,
            OperationReporter,
            IsgInspector,
        };

        /// <summary>Ofis ↔ ofis doğrudan mesajlaşmaya erişebilen roller.</summary>
        public static readonly HashSet<string> OfficeDirectChatRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            SuperAdmin,
            TenantAdmin,
            OfficeUser,
        };

        public static readonly Guid SuperAdminRoleId =
            Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");
        public static readonly Guid TenantAdminRoleId =
            Guid.Parse("b2c3d4e5-f6a7-4890-b123-456789abcdef");
        public static readonly Guid OfficeUserRoleId =
            Guid.Parse("c3d4e5f6-a7b8-4901-c234-56789abcdef0");
        public static readonly Guid FieldWorkerRoleId =
            Guid.Parse("d4e5f6a7-b8c9-4012-d345-6789abcdef01");
        public static readonly Guid OperationReporterRoleId =
            Guid.Parse("e5f6a7b8-c9d0-4123-e456-789abcdef012");
        public static readonly Guid IsgInspectorRoleId =
            Guid.Parse("f6a7b8c9-d0e1-4234-f567-89abcdef0123");
    }
}
