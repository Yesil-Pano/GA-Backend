using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 🔒 ÇOK KRİTİK: Sadece geçerli bir Token'ı olan (giriş yapmış) kullanıcılar buraya girebilir!
    public class UsersController : ControllerBase
    {
        private readonly IGenericRepository<User> _userRepository;
        private readonly IGenericRepository<Tenant> _tenantRepository;
        private readonly ICurrentUserService _currentUserService;

        public UsersController(
            IGenericRepository<User> userRepository,
            IGenericRepository<Tenant> tenantRepository,
            ICurrentUserService currentUserService)
        {
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _currentUserService = currentUserService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                // 1. Güvenli Servisten (Token'dan) kullanıcının ID'sini ve Şirket ID'sini alıyoruz
                var userId = _currentUserService.UserId;
                var tenantId = _currentUserService.TenantId;

                if (userId == Guid.Empty)
                    return Unauthorized(new { Message = "Geçersiz oturum." });

                // 2. Kullanıcıyı veritabanından buluyoruz
                var users = await _userRepository.FindAsync(u => u.Id == userId);
                var user = users.FirstOrDefault();

                if (user == null)
                    return NotFound(new { Message = "Kullanıcı bulunamadı." });

                // 3. Kullanıcının bağlı olduğu şirketi (Tenant) buluyoruz
                var tenants = await _tenantRepository.FindAsync(t => t.Id == tenantId);
                var tenant = tenants.FirstOrDefault();

                // 4. Mobildeki "UserProfile" sınıfının beklediği JSON formatında veriyi dönüyoruz
                // (.NET Core bu isimlendirmeleri otomatik olarak küçük harfle başlayan camelCase'e dönüştürür)
                return Ok(new
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    // Eğer Tenant (Şirket) tablonuzdaki kolon adı Name değil de Title ise burayı ona göre değiştirin.
                    CompanyName = tenant != null ? tenant.Name : "Bilinmeyen Şirket"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Profil bilgileri alınırken bir hata oluştu: " + ex.Message });
            }
        }
    }
}