using GA.Application.Features.Auth;
using GA.Application.Features.WorkOrders;
using GA.Application.Features.Partners;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private const long MaxDocumentBytes = 27L * 1024 * 1024;
        private const int MaxPersonnelDocuments = 10;

        private static readonly HashSet<string> PersonnelAllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".webp",
            ".doc", ".docx", ".xls", ".xlsx",
        };

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserAccessService _userAccessService;

        public TeamsController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IUserAccessService userAccessService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _userAccessService = userAccessService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams([FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var teams = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .Where(u => !u.IsDeleted && u.FieldWorkerProfile != null &&
                            (isSuperAdmin ||
                             u.TenantId == tenantId))
                .Select(u => new {
                    id = u.Id,
                    name = u.FullName,
                    username = u.Username,
                    email = u.Email,
                    phone = u.PhoneNumber,
                    tenantId = u.TenantId,
                    project = u.FieldWorkerProfile!.Projects.Any()
                        ? string.Join(", ", u.FieldWorkerProfile.Projects.Select(p => p.Name))
                        : (u.FieldWorkerProfile.ProjectName ?? "-"),
                    projectNames = u.FieldWorkerProfile.Projects.Any()
                        ? u.FieldWorkerProfile.Projects.Select(p => p.Name).ToList()
                        : (string.IsNullOrWhiteSpace(u.FieldWorkerProfile.ProjectName)
                            ? new List<string>()
                            : new List<string> { u.FieldWorkerProfile.ProjectName }),
                    projectIds = u.FieldWorkerProfile.Projects.Select(p => p.Id).ToList(),
                    assignedProjects = u.FieldWorkerProfile.Projects
                        .Select(p => new { id = p.Id, name = p.Name })
                        .ToList(),
                    plate = u.FieldWorkerProfile!.VehiclePlate ?? "-",
                    teamLeader = u.FieldWorkerProfile!.TeamLeader ?? "-",
                    address = u.FieldWorkerProfile!.Address ?? "-",
                    city = u.FieldWorkerProfile!.City ?? "-",
                    district = u.FieldWorkerProfile!.District ?? "-",
                    hasAuthorizationDocument = u.FieldWorkerProfile!.Documents.Any(d =>
                        !d.IsDeleted && d.DocumentType == FieldWorkerDocumentTypes.Authorization && d.FileSize > 0),
                    authorizationDocumentFileName = u.FieldWorkerProfile.Documents
                        .Where(d => !d.IsDeleted && d.DocumentType == FieldWorkerDocumentTypes.Authorization)
                        .Select(d => d.FileName)
                        .FirstOrDefault(),
                    authorizationDocumentFileSize = u.FieldWorkerProfile.Documents
                        .Where(d => !d.IsDeleted && d.DocumentType == FieldWorkerDocumentTypes.Authorization)
                        .Select(d => (long?)d.FileSize)
                        .FirstOrDefault(),
                    personnelDocumentCount = u.FieldWorkerProfile.Documents.Count(d =>
                        !d.IsDeleted && d.DocumentType == FieldWorkerDocumentTypes.Personnel),
                    hasLiveLocation = u.Location != null,
                    locationUpdatedAt = u.LocationUpdatedAt,
                    position = u.Location != null
                        ? new[] { u.Location.Y, u.Location.X }
                        : (u.FieldWorkerProfile!.HomeLocation != null
                            ? new[] { u.FieldWorkerProfile.HomeLocation.Y, u.FieldWorkerProfile.HomeLocation.X }
                            : new[] { 39.92077, 32.85411 })
                }).ToListAsync();

            if (isSuperAdmin)
            {
                var partner = PartnerCatalog.ResolveFilter(partnerKey);
                if (partner != null)
                {
                    teams = teams
                        .Where(t => PartnerCatalog.MatchesTeam(partner, t.tenantId, t.projectNames))
                        .ToList();
                }
            }

            // FE'ye projectNames alanını sızdırmadan aynısını dön (tenantId harita renkleri için)
            var payload = teams.Select(t => new {
                t.id,
                t.name,
                t.username,
                t.email,
                t.phone,
                t.tenantId,
                t.project,
                t.projectIds,
                t.plate,
                t.teamLeader,
                t.address,
                t.city,
                t.district,
                t.hasAuthorizationDocument,
                t.authorizationDocumentFileName,
                t.authorizationDocumentFileSize,
                t.hasLiveLocation,
                t.locationUpdatedAt,
                t.position,
            });

            return Ok(payload);
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetTeamsLookups(
            [FromQuery] string? partnerKey,
            [FromQuery] Guid? tenantIdFilter)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var projectsQuery = _context.Projects
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted &&
                            (isSuperAdmin ||
                             p.TenantId == tenantId));

            if (isSuperAdmin && tenantIdFilter.HasValue && tenantIdFilter.Value != Guid.Empty)
            {
                projectsQuery = projectsQuery.Where(p => p.TenantId == tenantIdFilter.Value);
            }

            var projects = await projectsQuery
                .Select(p => new { id = p.Id, name = p.Name, tenantId = p.TenantId })
                .OrderBy(p => p.name)
                .ToListAsync();

            if (isSuperAdmin && (!tenantIdFilter.HasValue || tenantIdFilter.Value == Guid.Empty))
            {
                var partner = PartnerCatalog.ResolveFilter(partnerKey);
                if (partner != null)
                {
                    projects = projects
                        .Where(p => PartnerCatalog.Matches(partner, p.tenantId, null, p.name))
                        .ToList();
                }
            }

            return Ok(projects);
        }

        [HttpGet("capabilities")]
        public async Task<IActionResult> GetCapabilities()
        {
            var canManage = await _userAccessService.IsTenantAdminOrAboveAsync();
            return Ok(new
            {
                canManageAuthorizationDocuments = canManage,
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTeamDto dto, [FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            Guid targetTenantId = tenantId;
            if (isSuperAdmin)
            {
                if (!dto.TenantId.HasValue || dto.TenantId == Guid.Empty)
                    return BadRequest(new { Message = "Super Admin olarak bir hedef firma seçmek zorundasınız!" });

                targetTenantId = dto.TenantId.Value;
            }

            var exists = await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == dto.Email || u.Username == dto.Username);
            if (exists) return BadRequest(new { Message = "Bu e-posta adresi veya kullanıcı adı zaten sistemde kayıtlı!" });

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.Name,
                PhoneNumber = dto.Phone,
                IsActive = true,
                TenantId = targetTenantId
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var profile = new FieldWorkerProfile
            {
                UserId = user.Id,
                ProjectName = dto.Project,
                VehiclePlate = dto.Plate,
                TeamLeader = dto.TeamLeader,
                // 🚀 YENİ ALANLAR KAYDEDİLİYOR
                Address = dto.Address,
                City = dto.City,
                District = dto.District,
                HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 }
            };

            if (dto.ProjectIds != null && dto.ProjectIds.Any())
            {
                var (selectedProjects, allValid) = await ResolveAssignableProjectsAsync(
                    dto.ProjectIds,
                    isSuperAdmin,
                    isSuperAdmin ? dto.TenantId : null,
                    partnerKey);

                if (!allValid)
                    return BadRequest(new { Message = "Seçilen projelerden biri veya birkaçı bulunamadı veya atama yetkiniz dışında." });

                foreach (var project in selectedProjects)
                    profile.Projects.Add(project);

                profile.ProjectName = string.Join(", ", selectedProjects.Select(p => p.Name));
            }

            _context.FieldWorkerProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ekip başarıyla oluşturuldu!" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(Guid id, [FromBody] UpdateTeamDto dto, [FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
                                          (isSuperAdmin ||
                                           u.TenantId == tenantId));

            if (user == null)
                return NotFound(new { Message = "Güncellenmek istenen ekip üyesi bulunamadı veya yetkiniz yetersiz." });

            var exists = await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != id && (u.Email == dto.Email || u.Username == dto.Username));
            if (exists) return BadRequest(new { Message = "Bu e-posta veya kullanıcı adı başka bir personele aittir!" });

            user.FullName = dto.Name;
            user.PhoneNumber = dto.Phone;
            user.Username = dto.Username;
            user.Email = dto.Email;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            user.UpdatedAt = DateTime.UtcNow;

            if (user.FieldWorkerProfile != null)
            {
                user.FieldWorkerProfile.VehiclePlate = dto.Plate;
                user.FieldWorkerProfile.TeamLeader = dto.TeamLeader;
                // 🚀 YENİ ALANLAR GÜNCELLENİYOR
                user.FieldWorkerProfile.Address = dto.Address;
                user.FieldWorkerProfile.City = dto.City;
                user.FieldWorkerProfile.District = dto.District;

                user.FieldWorkerProfile.HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
                user.FieldWorkerProfile.UpdatedAt = DateTime.UtcNow;

                user.FieldWorkerProfile.Projects.Clear();
                if (dto.ProjectIds != null && dto.ProjectIds.Any())
                {
                    var (selectedProjects, allValid) = await ResolveAssignableProjectsAsync(
                        dto.ProjectIds,
                        isSuperAdmin,
                        tenantIdFilter: null,
                        partnerKey);

                    if (!allValid)
                        return BadRequest(new { Message = "Seçilen projelerden biri veya birkaçı bulunamadı veya atama yetkiniz dışında." });

                    foreach (var project in selectedProjects)
                        user.FieldWorkerProfile.Projects.Add(project);

                    user.FieldWorkerProfile.ProjectName = string.Join(", ", selectedProjects.Select(p => p.Name));
                }
                else
                {
                    user.FieldWorkerProfile.ProjectName = "-";
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Ekip bilgileri kurumsal standartlarda başarıyla güncellendi!" });
        }

        /// <summary>
        /// Ekibi soft-delete eder; açık iş emirlerini Atanmamış'a çeker.
        /// DELETE /api/teams/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
                                          (isSuperAdmin ||
                                           u.TenantId == tenantId));

            if (user == null)
                return NotFound(new { Message = "Silinecek ekip bulunamadı veya yetkiniz yetersiz." });

            user.IsDeleted = true;
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.FieldWorkerProfile != null)
            {
                user.FieldWorkerProfile.IsDeleted = true;
                user.FieldWorkerProfile.UpdatedAt = DateTime.UtcNow;
            }

            var openStatuses = new[]
            {
                WorkOrderStatus.Unassigned,
                WorkOrderStatus.Waiting,
                WorkOrderStatus.InProgress,
            };
            var openOrders = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted
                            && w.AssignedToUserId == id
                            && openStatuses.Contains(w.Status))
                .ToListAsync();

            foreach (var order in openOrders)
            {
                order.AssignedToUserId = null;
                WorkOrderStatus.ApplyOnUnassign(order);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Ekip silindi. Açık iş emirleri Atanmamış durumuna alındı.",
                unassignedWorkOrderCount = openOrders.Count,
            });
        }

        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateTeamLocationDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty) return Unauthorized();

            var profile = await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == dto.TeamUserId &&
                                          p.User.TenantId == tenantId);

            if (profile == null) return NotFound(new { message = "Saha personeli profili bulunamadı." });

            profile.HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
            await _context.SaveChangesAsync();
            return Ok(new { message = "Saha konumu merkeze başarıyla raporlandı." });
        }

        /// <summary>
        /// Personel evrak listesi (metadata; binary yok).
        /// GET /api/teams/{id}/documents
        /// </summary>
        [HttpGet("{id:guid}/documents")]
        public async Task<IActionResult> ListDocuments(Guid id, [FromQuery] string? type = null)
        {
            if (!await _userAccessService.IsTenantAdminOrAboveAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Evrakları görüntülemek için Super Admin veya Firma Admin olmalısınız." });

            var profile = await FindAccessibleProfileAsync(id);
            if (profile == null)
                return NotFound(new { message = "Ekip profili bulunamadı veya yetkiniz yetersiz." });

            var query = _context.FieldWorkerDocuments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.FieldWorkerProfileId == profile.Id && !d.IsDeleted);

            if (!string.IsNullOrWhiteSpace(type))
            {
                var normalized = NormalizeDocumentType(type);
                if (normalized == null)
                    return BadRequest(new { message = "Geçersiz belge tipi. Authorization veya Personnel kullanın." });
                query = query.Where(d => d.DocumentType == normalized);
            }

            var items = await query
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    id = d.Id,
                    documentType = d.DocumentType,
                    fileName = d.FileName,
                    contentType = d.ContentType,
                    fileSize = d.FileSize,
                    uploadedAt = d.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToListAsync();

            return Ok(new
            {
                authorizationCount = items.Count(i => i.documentType == FieldWorkerDocumentTypes.Authorization),
                personnelCount = items.Count(i => i.documentType == FieldWorkerDocumentTypes.Personnel),
                items,
            });
        }

        /// <summary>
        /// Evrak yükle.
        /// POST /api/teams/{id}/documents?type=Authorization|Personnel
        /// Yetki Belgesi: tek PDF (varsa değiştirilir). Personel: max 10, PDF/görsel/Office.
        /// </summary>
        [HttpPost("{id:guid}/documents")]
        [RequestSizeLimit(30L * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 30L * 1024 * 1024)]
        public async Task<IActionResult> UploadDocument(Guid id, [FromQuery] string type, IFormFile file)
        {
            if (!await _userAccessService.IsTenantAdminOrAboveAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Evrak yüklemek için Super Admin veya Firma Admin olmalısınız." });

            var documentType = NormalizeDocumentType(type);
            if (documentType == null)
                return BadRequest(new { message = "type zorunlu: Authorization veya Personnel." });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Lütfen bir dosya seçin." });

            if (file.Length > MaxDocumentBytes)
                return BadRequest(new { message = "Dosya boyutu en fazla 27 MB olabilir." });

            var profile = await FindAccessibleProfileAsync(id);
            if (profile == null)
                return NotFound(new { message = "Ekip profili bulunamadı veya yetkiniz yetersiz." });

            var fileName = Path.GetFileName(file.FileName ?? "document");
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = (file.ContentType ?? "").ToLowerInvariant();

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            if (documentType == FieldWorkerDocumentTypes.Authorization)
            {
                var isPdf = contentType.Contains("pdf") || ext == ".pdf";
                if (!isPdf)
                    return BadRequest(new { message = "Yetki Belgesi yalnızca PDF olabilir." });
                if (bytes.Length < 5 || bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46)
                    return BadRequest(new { message = "Geçersiz PDF dosyası." });
                contentType = "application/pdf";
            }
            else
            {
                if (!PersonnelAllowedExtensions.Contains(ext))
                    return BadRequest(new { message = "Personel evrakı için izin verilen türler: PDF, JPG, PNG, WEBP, DOC, DOCX, XLS, XLSX." });
                if (string.IsNullOrWhiteSpace(contentType))
                    contentType = GuessContentType(ext);

                var personnelCount = await _context.FieldWorkerDocuments
                    .IgnoreQueryFilters()
                    .CountAsync(d => d.FieldWorkerProfileId == profile.Id
                                     && !d.IsDeleted
                                     && d.DocumentType == FieldWorkerDocumentTypes.Personnel);
                if (personnelCount >= MaxPersonnelDocuments)
                    return BadRequest(new { message = $"Personel Evrak Bilgisi en fazla {MaxPersonnelDocuments} dosya olabilir." });
            }

            FieldWorkerDocument? existingAuth = null;
            if (documentType == FieldWorkerDocumentTypes.Authorization)
            {
                existingAuth = await _context.FieldWorkerDocuments
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.FieldWorkerProfileId == profile.Id
                                              && !d.IsDeleted
                                              && d.DocumentType == FieldWorkerDocumentTypes.Authorization);
            }

            if (existingAuth != null)
            {
                existingAuth.FileName = fileName;
                existingAuth.ContentType = contentType;
                existingAuth.Data = bytes;
                existingAuth.FileSize = bytes.LongLength;
                existingAuth.UploadedAt = DateTime.UtcNow;
                existingAuth.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Yetki Belgesi güncellendi.",
                    id = existingAuth.Id,
                    documentType = existingAuth.DocumentType,
                    fileName = existingAuth.FileName,
                    contentType = existingAuth.ContentType,
                    fileSize = existingAuth.FileSize,
                    uploadedAt = existingAuth.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                });
            }

            var doc = new FieldWorkerDocument
            {
                FieldWorkerProfileId = profile.Id,
                DocumentType = documentType,
                FileName = fileName,
                ContentType = contentType,
                Data = bytes,
                FileSize = bytes.LongLength,
                UploadedAt = DateTime.UtcNow,
                TenantId = profile.TenantId,
                CustomerId = profile.CustomerId,
            };
            _context.FieldWorkerDocuments.Add(doc);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = documentType == FieldWorkerDocumentTypes.Authorization
                    ? "Yetki Belgesi kaydedildi."
                    : "Personel evrakı kaydedildi.",
                id = doc.Id,
                documentType = doc.DocumentType,
                fileName = doc.FileName,
                contentType = doc.ContentType,
                fileSize = doc.FileSize,
                uploadedAt = doc.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
            });
        }

        /// <summary>
        /// Evrak indir / görüntüle.
        /// GET /api/teams/{id}/documents/{docId}
        /// </summary>
        [HttpGet("{id:guid}/documents/{docId:guid}")]
        public async Task<IActionResult> GetDocument(Guid id, Guid docId)
        {
            if (!await _userAccessService.IsTenantAdminOrAboveAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Evrak görüntülemek için Super Admin veya Firma Admin olmalısınız." });

            var profile = await FindAccessibleProfileAsync(id);
            if (profile == null)
                return NotFound(new { message = "Ekip profili bulunamadı veya yetkiniz yetersiz." });

            var doc = await _context.FieldWorkerDocuments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == docId && d.FieldWorkerProfileId == profile.Id && !d.IsDeleted);

            if (doc == null || doc.Data == null || doc.Data.Length == 0)
                return NotFound(new { message = "Evrak bulunamadı." });

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{doc.FileName}\"";
            return File(doc.Data, doc.ContentType);
        }

        /// <summary>
        /// Evrak sil.
        /// DELETE /api/teams/{id}/documents/{docId}
        /// </summary>
        [HttpDelete("{id:guid}/documents/{docId:guid}")]
        public async Task<IActionResult> DeleteDocument(Guid id, Guid docId)
        {
            if (!await _userAccessService.IsTenantAdminOrAboveAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Evrak silmek için Super Admin veya Firma Admin olmalısınız." });

            var profile = await FindAccessibleProfileAsync(id);
            if (profile == null)
                return NotFound(new { message = "Ekip profili bulunamadı veya yetkiniz yetersiz." });

            var doc = await _context.FieldWorkerDocuments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == docId && d.FieldWorkerProfileId == profile.Id && !d.IsDeleted);

            if (doc == null)
                return NotFound(new { message = "Evrak bulunamadı." });

            doc.IsDeleted = true;
            doc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = doc.DocumentType == FieldWorkerDocumentTypes.Authorization
                    ? "Yetki Belgesi silindi."
                    : "Personel evrakı silindi.",
                id = doc.Id,
                documentType = doc.DocumentType,
            });
        }

        /// <summary>Geriye uyumluluk: Yetki Belgesi yükle → documents?type=Authorization</summary>
        [HttpPost("{id:guid}/authorization-document")]
        [RequestSizeLimit(30L * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 30L * 1024 * 1024)]
        public Task<IActionResult> UploadAuthorizationDocument(Guid id, IFormFile file)
            => UploadDocument(id, FieldWorkerDocumentTypes.Authorization, file);

        /// <summary>Geriye uyumluluk: Yetki Belgesi görüntüle</summary>
        [HttpGet("{id:guid}/authorization-document")]
        public async Task<IActionResult> GetAuthorizationDocument(Guid id)
        {
            if (!await _userAccessService.IsTenantAdminOrAboveAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yetki belgesini görüntülemek için Super Admin veya Firma Admin olmalısınız." });

            var profile = await FindAccessibleProfileAsync(id);
            if (profile == null)
                return NotFound(new { message = "Ekip profili bulunamadı veya yetkiniz yetersiz." });

            var doc = await _context.FieldWorkerDocuments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.FieldWorkerProfileId == profile.Id
                                          && !d.IsDeleted
                                          && d.DocumentType == FieldWorkerDocumentTypes.Authorization);

            if (doc == null || doc.Data == null || doc.Data.Length == 0)
                return NotFound(new { message = "Bu ekibe ait yetki belgesi yok." });

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{doc.FileName}\"";
            return File(doc.Data, doc.ContentType);
        }

        /// <summary>Geriye uyumluluk: Yetki Belgesi sil</summary>
        [HttpDelete("{id:guid}/authorization-document")]
        public async Task<IActionResult> DeleteAuthorizationDocument(Guid id)
        {
            if (!await _userAccessService.IsTenantAdminOrAboveAsync())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Yetki belgesi silmek için Super Admin veya Firma Admin olmalısınız." });

            var profile = await FindAccessibleProfileAsync(id);
            if (profile == null)
                return NotFound(new { message = "Ekip profili bulunamadı veya yetkiniz yetersiz." });

            var doc = await _context.FieldWorkerDocuments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.FieldWorkerProfileId == profile.Id
                                          && !d.IsDeleted
                                          && d.DocumentType == FieldWorkerDocumentTypes.Authorization);

            if (doc == null)
                return NotFound(new { message = "Bu ekibe ait yetki belgesi yok." });

            doc.IsDeleted = true;
            doc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Yetki belgesi silindi.", hasAuthorizationDocument = false });
        }

        private static string? NormalizeDocumentType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;
            var t = type.Trim();
            if (t.Equals(FieldWorkerDocumentTypes.Authorization, StringComparison.OrdinalIgnoreCase)
                || t.Equals("YetkiBelgesi", StringComparison.OrdinalIgnoreCase)
                || t.Equals("yetki", StringComparison.OrdinalIgnoreCase))
                return FieldWorkerDocumentTypes.Authorization;
            if (t.Equals(FieldWorkerDocumentTypes.Personnel, StringComparison.OrdinalIgnoreCase)
                || t.Equals("Personel", StringComparison.OrdinalIgnoreCase)
                || t.Equals("PersonelEvrak", StringComparison.OrdinalIgnoreCase))
                return FieldWorkerDocumentTypes.Personnel;
            return null;
        }

        private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };

        /// <summary>
        /// Adminin lookups ekranında gördüğü projelerle aynı kapsam — çoklu proje ataması.
        /// </summary>
        private async Task<(List<Project> projects, bool allValid)> ResolveAssignableProjectsAsync(
            IEnumerable<Guid> projectIds,
            bool isSuperAdmin,
            Guid? tenantIdFilter,
            string? partnerKey)
        {
            var distinctIds = projectIds.Distinct().ToList();
            if (distinctIds.Count == 0)
                return (new List<Project>(), true);

            var adminTenantId = _currentUserService.TenantId;

            var query = _context.Projects
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && distinctIds.Contains(p.Id));

            if (isSuperAdmin)
            {
                if (tenantIdFilter.HasValue && tenantIdFilter.Value != Guid.Empty)
                    query = query.Where(p => p.TenantId == tenantIdFilter.Value);
            }
            else
            {
                query = query.Where(p => p.TenantId == adminTenantId);
            }

            var projects = await query.ToListAsync();

            if (isSuperAdmin && (!tenantIdFilter.HasValue || tenantIdFilter.Value == Guid.Empty))
            {
                var partner = PartnerCatalog.ResolveFilter(partnerKey);
                if (partner != null)
                {
                    projects = projects
                        .Where(p => PartnerCatalog.Matches(partner, p.TenantId, null, p.Name))
                        .ToList();
                }
            }

            return (projects, projects.Count == distinctIds.Count);
        }

        private async Task<FieldWorkerProfile?> FindAccessibleProfileAsync(Guid userId)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            return await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId &&
                    !p.IsDeleted &&
                    p.User != null &&
                    !p.User.IsDeleted &&
                    (isSuperAdmin ||
                     p.User.TenantId == tenantId));
        }
    }

    public class CreateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;

        // 🚀 DTO'YA YENİ ALANLAR EKLENDİ
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;

        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        public Guid? TenantId { get; set; }
        public double Latitude { get; set; } = 39.92077;
        public double Longitude { get; set; } = 32.85411;
    }

    public class UpdateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;

        // 🚀 DTO'YA YENİ ALANLAR EKLENDİ
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;

        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class UpdateTeamLocationDto
    {
        public Guid TeamUserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}