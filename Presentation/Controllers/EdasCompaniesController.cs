using System.Globalization;
using GA.Application.Features.Auth;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    /// <summary>
    /// Dağıtım şirketi (EDAŞ) referans listesi.
    /// </summary>
    [Route("api/edas-companies")]
    [ApiController]
    [Authorize]
    public class EdasCompaniesController : ControllerBase
    {
        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        private readonly ApplicationDbContext _context;
        private readonly IUserAccessService _userAccessService;

        public EdasCompaniesController(ApplicationDbContext context, IUserAccessService userAccessService)
        {
            _context = context;
            _userAccessService = userAccessService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var companies = await _context.EdasCompanies
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.Name)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                })
                .ToListAsync();

            return Ok(companies);
        }

        [HttpGet("capabilities")]
        public async Task<IActionResult> GetCapabilities()
        {
            var canManage = await _userAccessService.CanManageEdasCompaniesAsync();
            return Ok(new { canManageEdasCompanies = canManage });
        }

        public record CreateEdasCompanyDto(string Name);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEdasCompanyDto dto)
        {
            if (!await _userAccessService.CanManageEdasCompaniesAsync())
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "EDAŞ adı zorunludur." });

            var normalized = dto.Name.Trim().ToUpper(Turkish);
            if (normalized.Length > 100)
                return BadRequest(new { message = "EDAŞ adı en fazla 100 karakter olabilir." });

            var exists = await _context.EdasCompanies
                .AsNoTracking()
                .AnyAsync(e => !e.IsDeleted && e.Name == normalized);

            if (exists)
                return Conflict(new { message = "Bu EDAŞ adı zaten kayıtlı." });

            var entity = new EdasCompany { Name = normalized };
            _context.EdasCompanies.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { id = entity.Id, name = entity.Name });
        }
    }
}
