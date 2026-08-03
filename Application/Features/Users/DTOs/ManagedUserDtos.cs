namespace GA.Application.Features.Users.DTOs
{
    public class CreateManagedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> RoleNames { get; set; } = new();
    }

    public class UpdateManagedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> RoleNames { get; set; } = new();
    }

    public class ManagedUserResultDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
