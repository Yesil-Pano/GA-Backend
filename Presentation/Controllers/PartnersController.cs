using GA.Application.Features.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PartnersController : ControllerBase
    {
        private readonly IPartnerTenantService _partnerTenantService;

        public PartnersController(IPartnerTenantService partnerTenantService)
        {
            _partnerTenantService = partnerTenantService;
        }

        /// <summary>Firma seçici + tenant eşlemesi. GET /api/partners</summary>
        [HttpGet]
        public async Task<IActionResult> GetPartners(CancellationToken ct)
        {
            var partners = await _partnerTenantService.ListForUiAsync(ct);
            return Ok(partners);
        }
    }
}
