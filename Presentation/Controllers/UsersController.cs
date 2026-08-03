using GA.Application.Features.Auth;
using GA.Application.Features.Users;
using GA.Application.Features.Users.DTOs;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IGenericRepository<User> _userRepository;
        private readonly IGenericRepository<Tenant> _tenantRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _context;
        private readonly IUserAccessService _userAccessService;
        private readonly IUserManagementService _userManagementService;

        public UsersController(
            IGenericRepository<User> userRepository,
            IGenericRepository<Tenant> tenantRepository,
            ICurrentUserService currentUserService,
            ApplicationDbContext context,
            IUserAccessService userAccessService,
            IUserManagementService userManagementService)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _currentUserService = currentUserService;
            _context = context;
            _userAccessService = userAccessService;
            _userManagementService = userManagementService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                var userId = _currentUserService.UserId;
                var tenantId = _currentUserService.TenantId;

                if (userId == Guid.Empty)
                    return Unauthorized(new { Message = "Geçersiz oturum." });

                var users = await _userRepository.FindAsync(u => u.Id == userId);
                var user = users.FirstOrDefault();

                if (user == null)
                    return NotFound(new { Message = "Kullanıcı bulunamadı." });

                var tenants = await _tenantRepository.FindAsync(t => t.Id == tenantId);
                var tenant = tenants.FirstOrDefault();

                var profile = await _context.FieldWorkerProfiles
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

                var hasAuthDoc = false;
                string? authFileName = null;
                long? authFileSize = null;
                if (profile != null)
                {
                    var auth = await _context.FieldWorkerDocuments
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .Where(d => d.FieldWorkerProfileId == profile.Id
                                    && !d.IsDeleted
                                    && d.DocumentType == FieldWorkerDocumentTypes.Authorization)
                        .Select(d => new { d.FileName, d.FileSize })
                        .FirstOrDefaultAsync();
                    if (auth != null)
                    {
                        hasAuthDoc = true;
                        authFileName = auth.FileName;
                        authFileSize = auth.FileSize;
                    }
                }

                var companyName = tenant != null ? tenant.Name : "Bilinmeyen Şirket";
                var roles = await _userAccessService.GetRoleNamesAsync(userId);

                return Ok(new
                {
                    id = user.Id,
                    fullName = user.FullName,
                    email = user.Email,
                    companyName,
                    tenantId = tenantId == Guid.Empty ? (Guid?)null : tenantId,
                    roles,
                    canViewIsgPhotos = await _userAccessService.CanViewIsgPhotosAsync(),
                    canViewOperationPhotos = await _userAccessService.CanViewOperationPhotosAsync(),
                    hasAuthorizationDocument = hasAuthDoc,
                    authorizationDocumentFileName = authFileName,
                    authorizationDocumentFileSize = authFileSize,
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Profil bilgileri alınırken bir hata oluştu: " + ex.Message });
            }
        }

        /// <summary>
        /// Oturum açmış kullanıcı kendi şifresini değiştirir.
        /// POST /api/users/me/change-password
        /// </summary>
        [HttpPost("me/change-password")]
        public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeMyPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(new { message = "Mevcut ve yeni şifre zorunludur." });

            if (dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Yeni şifre en az 6 karakter olmalıdır." });

            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty)
                return Unauthorized(new { message = "Geçersiz oturum." });

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                return NotFound(new { message = "Kullanıcı bulunamadı." });

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                return BadRequest(new { message = "Mevcut şifre hatalı." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Şifreniz güncellendi." });
        }

        /// <summary>
        /// Sahacı kendi Yetki Belgesi PDF'ini görüntüler (Admin gerekmez).
        /// GET /api/users/me/authorization-document
        /// </summary>
        [HttpGet("me/authorization-document")]
        public async Task<IActionResult> GetMyAuthorizationDocument()
        {
            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty)
                return Unauthorized(new { message = "Geçersiz oturum." });

            var profile = await _context.FieldWorkerProfiles
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

            if (profile == null)
                return NotFound(new { message = "Saha profili bulunamadı." });

            var doc = await _context.FieldWorkerDocuments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.FieldWorkerProfileId == profile.Id
                                          && !d.IsDeleted
                                          && d.DocumentType == FieldWorkerDocumentTypes.Authorization);

            if (doc == null || doc.Data == null || doc.Data.Length == 0)
                return NotFound(new { message = "Yetki belgesi bulunamadı." });

            var fileName = string.IsNullOrWhiteSpace(doc.FileName) ? "yetki-belgesi.pdf" : doc.FileName;
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(doc.Data, doc.ContentType ?? "application/pdf");
        }

        /// <summary>Super Admin: kullanıcı listesi. GET /api/users</summary>
        [HttpGet]
        public async Task<IActionResult> ListUsers()
        {
            if (!await _userAccessService.IsSuperAdminAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yalnızca Super Admin erişebilir." });

            var tenantLookup = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .ToDictionaryAsync(t => t.Id, t => t.Name);

            var users = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    fullName = u.FullName,
                    email = u.Email,
                    phoneNumber = u.PhoneNumber,
                    tenantId = u.TenantId,
                    isActive = u.IsActive,
                    roles = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(_context.Roles.Where(r => !r.IsDeleted), ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                        .ToList(),
                })
                .ToListAsync();

            var result = users.Select(u => new
            {
                u.id,
                u.username,
                u.fullName,
                u.email,
                u.phoneNumber,
                u.tenantId,
                tenantName = u.tenantId == Guid.Empty
                    ? "Sistem (Super Admin)"
                    : (tenantLookup.TryGetValue(u.tenantId, out var name) ? name : "—"),
                u.isActive,
                u.roles,
            });

            return Ok(result);
        }

        /// <summary>Super Admin: firma listesi (dropdown). GET /api/users/tenants</summary>
        [HttpGet("tenants")]
        public async Task<IActionResult> ListTenants()
        {
            if (!await _userAccessService.IsSuperAdminAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yalnızca Super Admin erişebilir." });

            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new { id = t.Id, name = t.Name })
                .ToListAsync();

            return Ok(tenants);
        }

        /// <summary>Super Admin: yeni kullanıcı. POST /api/users</summary>
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateManagedUserDto dto)
        {
            if (!await _userAccessService.IsSuperAdminAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yalnızca Super Admin erişebilir." });

            try
            {
                var created = await _userManagementService.CreateUserAsync(dto);
                return Ok(new { message = "Kullanıcı oluşturuldu.", user = created });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Super Admin: kullanıcı güncelle. PUT /api/users/{id}</summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateManagedUserDto dto)
        {
            if (!await _userAccessService.IsSuperAdminAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yalnızca Super Admin erişebilir." });

            try
            {
                var updated = await _userManagementService.UpdateUserAsync(id, dto);
                return Ok(new { message = "Kullanıcı güncellendi.", user = updated });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Super Admin: kullanıcı rollerini güncelle. PUT /api/users/{id}/roles</summary>
        [HttpPut("{id:guid}/roles")]
        public async Task<IActionResult> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesDto dto)
        {
            if (!await _userAccessService.IsSuperAdminAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yalnızca Super Admin erişebilir." });

            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (user == null)
                return NotFound(new { message = "Kullanıcı bulunamadı." });

            try
            {
                var updated = await _userManagementService.UpdateUserAsync(id, new UpdateManagedUserDto
                {
                    Username = user.Username,
                    Email = user.Email,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    TenantId = user.TenantId,
                    IsActive = user.IsActive,
                    RoleNames = dto.RoleNames ?? new List<string>(),
                });
                return Ok(new { message = "Roller güncellendi.", roles = updated.Roles });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Super Admin: atanabilir roller. GET /api/users/roles</summary>
        [HttpGet("roles")]
        public async Task<IActionResult> ListRoles()
        {
            if (!await _userAccessService.IsSuperAdminAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yalnızca Super Admin erişebilir." });

            var roles = await _context.Roles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.Name != RoleNames.SuperAdmin)
                .OrderBy(r => r.Name)
                .Select(r => new { id = r.Id, name = r.Name, description = r.Description })
                .ToListAsync();

            return Ok(roles);
        }
    }

    public class UpdateUserRolesDto
    {
        public List<string> RoleNames { get; set; } = new();
    }

    public class ChangeMyPasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
