using GA.Application.Features.Users.DTOs;

namespace GA.Application.Features.Users
{
    public interface IUserManagementService
    {
        Task<ManagedUserResultDto> CreateUserAsync(CreateManagedUserDto dto, CancellationToken ct = default);
        Task<ManagedUserResultDto> UpdateUserAsync(Guid userId, UpdateManagedUserDto dto, CancellationToken ct = default);
    }
}
