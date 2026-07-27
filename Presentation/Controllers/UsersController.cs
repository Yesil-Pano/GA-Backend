using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

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

        public UsersController(
            IGenericRepository<User> userRepository,
            IGenericRepository<Tenant> tenantRepository,
            ICurrentUserService currentUserService,
            ApplicationDbContext context)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _currentUserService = currentUserService;
            _context = context;
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

                return Ok(new
                {
                    fullName = user.FullName,
                    email = user.Email,
                    companyName,
                    tenantId = tenantId == Guid.Empty ? (Guid?)null : tenantId,
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
    }
}
