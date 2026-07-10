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

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        // Maksimum fotoğraf boyutu: 10 MB
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public PhotosController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Fotoğraf yükle (Base64 JSON body).
        /// POST /api/photos
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Upload([FromBody] UploadPhotoRequest request)
        {
            var tenantId = _currentUserService.TenantId;
            var userId   = _currentUserService.UserId;

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

            if (data.Length > MaxFileSizeBytes)
                return BadRequest(new { message = $"Dosya boyutu 10 MB'ı aşamaz. (Gönderilen: {data.Length / 1024 / 1024} MB)" });

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
                TenantId    = tenantId,
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

            var photos = await _context.Photos
                .IgnoreQueryFilters()
                .Where(p => p.EntityType == entityType
                         && p.EntityId   == entityId
                         && !p.IsDeleted
                         && (isSuperAdmin ||
                              p.TenantId == tenantId ||
                              (tenantId == _trugoTenantId && p.TenantId == _yesilPanoTenantId) ||
                              (tenantId == _yesilPanoTenantId && p.TenantId == _trugoTenantId)))
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

            var photo = await _context.Photos
                .IgnoreQueryFilters()
                .Where(p => p.Id == id
                         && !p.IsDeleted
                         && (isSuperAdmin ||
                              p.TenantId == tenantId ||
                              (tenantId == _trugoTenantId && p.TenantId == _yesilPanoTenantId) ||
                              (tenantId == _yesilPanoTenantId && p.TenantId == _trugoTenantId)))
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

            photo.IsDeleted = true;
            photo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
