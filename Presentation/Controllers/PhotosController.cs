using GA.Application.Features.Auth;
using GA.Application.Features.Photos;
using GA.Application.Features.Photos.DTOs;
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
    public class PhotosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserAccessService _userAccessService;

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        // Maksimum boyut: görsel 10 MB, video 30 MB (OpeningAttachmentRules)
        private const long LegacyMaxFileSizeBytes = 10 * 1024 * 1024;

        public PhotosController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IUserAccessService userAccessService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _userAccessService = userAccessService;
        }

        /// <summary>
        /// Fotoğraf yükle (Base64 JSON body).
        /// POST /api/photos
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(64L * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromBody] UploadPhotoRequest request)
        {
            var tenantId = _currentUserService.TenantId;
            var userId   = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;
            var isOpeningAttachment = OpeningAttachmentRules.IsOpeningCategory(request.Description);

            if (isOpeningAttachment && !OpeningAttachmentRules.IsAllowedContentType(request.ContentType))
                return BadRequest(new { message = "Açılış ekleri için yalnızca JPEG, PNG, WebP, MP4, MOV veya WebM yüklenebilir." });

            byte[] data;
            try
            {
                // data:image/jpeg;base64,... öneki varsa temizle
                var raw = request.Base64Data.Contains(',')
                    ? request.Base64Data.Split(',')[1]
                    : request.Base64Data;

                data = Convert.FromBase64String(raw);
            }
            catch
            {
                return BadRequest(new { message = "Geçersiz Base64 verisi." });
            }

            var maxBytes = isOpeningAttachment
                ? OpeningAttachmentRules.MaxBytesForContentType(request.ContentType)
                : LegacyMaxFileSizeBytes;

            if (data.Length > maxBytes)
            {
                var maxMb = maxBytes / 1024 / 1024;
                return BadRequest(new { message = $"Dosya boyutu {maxMb} MB'ı aşamaz. (Gönderilen: {data.Length / 1024 / 1024} MB)" });
            }

            Guid photoTenantId = tenantId;
            if (string.Equals(request.EntityType, "WorkOrder", StringComparison.OrdinalIgnoreCase))
            {
                var workOrder = await _context.WorkOrders
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == request.EntityId && !w.IsDeleted);

                if (workOrder == null)
                    return BadRequest(new { message = "İş emri bulunamadı." });

                if (!isSuperAdmin && !CanUploadToWorkOrder(workOrder, tenantId, userId))
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bu iş emrine dosya yükleme yetkiniz yok." });

                photoTenantId = workOrder.TenantId;

                if (isOpeningAttachment)
                {
                    var existingCount = await _context.Photos
                        .IgnoreQueryFilters()
                        .CountAsync(p => !p.IsDeleted
                                         && p.EntityType == "WorkOrder"
                                         && p.EntityId == request.EntityId
                                         && p.Description != null
                                         && p.Description.ToUpper() == OpeningAttachmentRules.Category);

                    if (existingCount >= OpeningAttachmentRules.MaxPerWorkOrder)
                        return BadRequest(new { message = $"En fazla {OpeningAttachmentRules.MaxPerWorkOrder} açılış eki yüklenebilir." });
                }
            }

            var photo = new Photo
            {
                FileName    = request.FileName,
                ContentType = request.ContentType,
                Data        = data,
                FileSize    = data.Length,
                Description = request.Description,
                EntityType  = request.EntityType,
                EntityId    = request.EntityId,
                UserId      = userId,
                TenantId    = photoTenantId != Guid.Empty ? photoTenantId : tenantId,
                CustomerId  = _currentUserService.CustomerId,
            };

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            return Ok(new PhotoDto
            {
                Id          = photo.Id,
                FileName    = photo.FileName,
                ContentType = photo.ContentType,
                FileSize    = photo.FileSize,
                Description = photo.Description,
                EntityType  = photo.EntityType,
                EntityId    = photo.EntityId,
                UserId      = photo.UserId,
                CreatedAt   = photo.CreatedAt,
            });
        }

        /// <summary>
        /// Bir kayda ait fotoğraf listesi (binary DATA döndürülmez, sadece metadata).
        /// GET /api/photos/{entityType}/{entityId}
        /// </summary>
        [HttpGet("{entityType}/{entityId:guid}")]
        public async Task<IActionResult> ListForEntity(string entityType, Guid entityId)
        {
            var tenantId     = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var filtered = await ApplyPhotoVisibilityFilter(
                _context.Photos
                    .IgnoreQueryFilters()
                    .Where(p => p.EntityType == entityType
                             && p.EntityId == entityId
                             && !p.IsDeleted
                             && (isSuperAdmin ||
                                  p.TenantId == tenantId ||
                                  (tenantId == _yesilPanoTenantId && p.TenantId == _trugoTenantId))));

            var photos = await filtered
                .OrderBy(p => p.CreatedAt)
                .Select(p => new PhotoDto
                {
                    Id          = p.Id,
                    FileName    = p.FileName,
                    ContentType = p.ContentType,
                    FileSize    = p.FileSize,
                    Description = p.Description,
                    EntityType  = p.EntityType,
                    EntityId    = p.EntityId,
                    UserId      = p.UserId,
                    CreatedAt   = p.CreatedAt,
                })
                .ToListAsync();

            return Ok(photos);
        }

        /// <summary>
        /// Tek fotoğrafın binary verisini döndürür (tarayıcı veya mobil için).
        /// GET /api/photos/{id}/image
        /// </summary>
        [HttpGet("{id:guid}/image")]
        public async Task<IActionResult> GetImage(Guid id)
        {
            var tenantId     = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var filtered = await ApplyPhotoVisibilityFilter(
                _context.Photos
                    .IgnoreQueryFilters()
                    .Where(p => p.Id == id
                             && !p.IsDeleted
                             && (isSuperAdmin ||
                                  p.TenantId == tenantId ||
                                  (tenantId == _yesilPanoTenantId && p.TenantId == _trugoTenantId))));

            var photo = await filtered
                .Select(p => new { p.Data, p.ContentType, p.FileName })
                .FirstOrDefaultAsync();

            if (photo == null) return NotFound();

            return File(photo.Data, photo.ContentType, photo.FileName);
        }

        /// <summary>
        /// Fotoğrafı soft-delete ile sil.
        /// DELETE /api/photos/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var tenantId     = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var photo = await _context.Photos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id
                                       && !p.IsDeleted
                                       && (isSuperAdmin || p.TenantId == tenantId));

            if (photo == null) return NotFound();

            if (OpeningAttachmentRules.IsOpeningCategory(photo.Description))
                return BadRequest(new { message = "Açılış ekleri silinemez." });

            photo.IsDeleted = true;
            photo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<IQueryable<Photo>> ApplyPhotoVisibilityFilter(IQueryable<Photo> query)
        {
            if (_currentUserService.TenantId == Guid.Empty)
                return query;

            var canViewIsg = await _userAccessService.CanViewIsgPhotosAsync();
            var canViewOperasyon = await _userAccessService.CanViewOperationPhotosAsync();

            if (canViewIsg && canViewOperasyon)
                return query;

            var acilis = OpeningAttachmentRules.Category;

            if (canViewIsg && !canViewOperasyon)
            {
                return query.Where(p =>
                    (p.Description != null && p.Description.ToUpper() == acilis) ||
                    p.Description == null ||
                    p.Description.ToUpper() == "ISG" ||
                    p.Description.ToUpper() == "DIGER");
            }

            if (!canViewIsg && canViewOperasyon)
            {
                return query.Where(p =>
                    (p.Description != null && p.Description.ToUpper() == acilis) ||
                    p.Description == null ||
                    p.Description.ToUpper() != "ISG");
            }

            return query.Where(p => p.Description != null && p.Description.ToUpper() == acilis);
        }

        /// <summary>
        /// İş emri listesi/görüntüleme ile uyumlu yükleme yetkisi.
        /// Atanan saha personeli ve YP→TRUGO erişimi dahil.
        /// </summary>
        private bool CanUploadToWorkOrder(WorkOrder workOrder, Guid tenantId, Guid userId)
        {
            if (workOrder.TenantId == tenantId)
                return true;

            if (workOrder.AssignedToUserId == userId)
                return true;

            if (tenantId == _yesilPanoTenantId && workOrder.TenantId == _trugoTenantId)
                return true;

            return false;
        }
    }
}
