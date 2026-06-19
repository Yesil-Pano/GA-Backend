using GA.Application.Features.Auth.DTOs;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WorkOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. LİSTELEME UÇ NOKTASI
        [HttpGet]
        public IActionResult GetWorkOrders()
        {
            var orders = _context.WorkOrders
                .Where(w => !w.IsDeleted)
                .Select(w => new {
                    id = w.Id,
                    title = w.Title,
                    customerName = w.CustomerName,
                    priority = w.Priority,
                    status = w.Status,
                    type = w.WorkType,
                    category = w.WorkCategory,
                    description = w.Description,
                    mobileDescription = w.MobileDescription,
                    address = w.Address,
                    startDate = w.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    endDate = w.EndDate.ToString("yyyy-MM-dd HH:mm"),
                    plannedDate = w.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    position = new[] { w.Location.Y, w.Location.X },

                    // 💡 FRONEND'DE FİLTRELEME YAPABİLMEK İÇİN BU ID ALANINI EKLİYORUZ:
                    assignedToUserId = w.AssignedToUserId,

                    operationUserName = _context.Users.Where(u => u.Id == w.OperationUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış",
                    openedByUserName = _context.Users.Where(u => u.Id == w.OpenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış",
                    assignedToUserName = _context.Users.Where(u => u.Id == w.AssignedToUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış"
                }).ToList();

            return Ok(orders);
        }

        // 2. YENİ: DİNAMİK SEÇİM KUTULARINI (COMBOBOX) VERİTABANINDAN DOLDURAN UÇ NOKTA
        [HttpGet("lookups")]
        public IActionResult GetFormLookups()
        {
            // Veritabanındaki gerçek kayıtlı personelleri çekiyoruz (Seçim kutuları için)
            var systemPersonnel = _context.Users
                .Where(u => u.IsActive && !u.IsDeleted)
                .Select(u => new { id = u.Id, fullName = u.FullName })
                .ToList();

            // Videoda yer alan Teamer listeleri
            var workTypes = new[] { "Arıza", "Devreye Alma", "Kontrol" };
            var workCategories = new[] { "Arıza Bildirimi", "YG İşletme Sorumluluğu Talebi", "YG Bakım", "AG Bakım", "Kapasitif Ceza", "QR, Etiket ve Görsel Kontrol" };

            return Ok(new
            {
                personnel = systemPersonnel,
                types = workTypes,
                categories = workCategories
            });
        }

        // 3. KAYDETME UÇ NOKTASI
        [HttpPost]
        public IActionResult CreateWorkOrder([FromBody] CreateWorkOrderDto dto)
        {
            var workOrder = new WorkOrder
            {
                Title = dto.Title,
                CustomerName = dto.CustomerName,
                Description = dto.Description,
                MobileDescription = dto.MobileDescription,
                Address = dto.Address,
                Priority = dto.Priority,
                WorkType = dto.WorkType,
                WorkCategory = dto.WorkCategory,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 },
                OperationUserId = dto.OperationUserId,
                OpenedByUserId = dto.OpenedByUserId,
                AssignedToUserId = dto.AssignedToUserId
            };

            _context.WorkOrders.Add(workOrder);
            _context.SaveChanges();

            return Ok(new { message = "İş emri başarıyla oluşturuldu!" });
        }
    }
}
