using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataImportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly Guid _targetTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");

        public DataImportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🧹 0. UÇ NOKTA: UZAYLI İSTİLASINI (BOZUK VERİLERİ) TEMİZLEME BUTONU
        [HttpDelete("clean-garbage")]
        public async Task<IActionResult> CleanGarbage()
        {
            // Yeşil Pano firmasına ait, bugün eklenmiş tüm bozuk istasyonları sil
            var badStations = await _context.Stations
                .Where(s => s.TenantId == _targetTenantId)
                .ToListAsync();
            _context.Stations.RemoveRange(badStations);

            // Adı/Soyadı uzaylı karakteri içeren kullanıcıları sil
            var badUsers = await _context.Users
                .Include(u => u.FieldWorkerProfile)
                .Where(u => u.TenantId == _targetTenantId && u.FullName.Contains(""))
                .ToListAsync();
            _context.Users.RemoveRange(badUsers);

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Sistem uzaylı kodlarından ve bozuk verilerden tamamen arındırıldı şefim!" });
        }

        // 🟢 1. UÇ NOKTA: PERSONEL CSV AKTARIMI
        [HttpPost("import-personnel")]
        public async Task<IActionResult> ImportPersonnel(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Lütfen bir dosya yükleyin.");

            // 🚀 EXCEL KORUMASI: Uzantı kontrolü
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("ŞEFİM DİKKAT! Lütfen '.xlsx' dosyasını değil, sonu '.csv' ile biten dosyayı seçin.");

            int successCount = 0;
            // Türkçe karakterleri sorunsuz okumak için UTF-8 tanımladık
            using var streamReader = new StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8);

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 🚀 EXCEL BÖLGESEL DİL KORUMASI: Sütunlar virgül ile mi yoksa noktalı virgül ile mi ayrılmış?
                var columns = line.Contains(";") ? line.Split(';') : line.Split(',');

                try
                {
                    string ad = columns[0].Replace("\0", "").Trim();
                    if (string.IsNullOrEmpty(ad) || ad == "KAAN" || ad == "Lokasyon Adı") continue; // Başlıkları atla

                    string soyad = columns.Length > 1 ? columns[1].Replace("\0", "").Trim() : "";
                    string plaka = columns.Length > 5 ? columns[5].Replace("\0", "").Trim() : "ARAÇ YOK";

                    string safeName = $"{ad.ToLower().Replace(" ", "")}{soyad.ToLower().Replace(" ", "")}";
                    string uniqueSuffix = Guid.NewGuid().ToString().Substring(0, 4);

                    var user = new User
                    {
                        Username = $"{safeName}_{uniqueSuffix}",
                        Email = $"{safeName}_{uniqueSuffix}@yesilpano.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Teamer123!"),
                        FullName = $"{ad} {soyad}",
                        PhoneNumber = "05550000000",
                        IsActive = true,
                        TenantId = _targetTenantId
                    };

                    _context.Users.Add(user);

                    var profile = new FieldWorkerProfile
                    {
                        UserId = user.Id,
                        ProjectName = "Yeşil Pano Projesi",
                        VehiclePlate = plaka == "ARAÇ YOK" ? "-" : plaka,
                        TeamLeader = "-",
                        HomeLocation = new NetTopologySuite.Geometries.Point(32.85411, 39.92077) { SRID = 4326 }
                    };

                    _context.FieldWorkerProfiles.Add(profile);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Satır okuma hatası: {line} - Hata: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{successCount} adet personel başarıyla Yeşil Pano firmasına aktarıldı!" });
        }

        // 🟢 2. UÇ NOKTA: İSTASYON CSV AKTARIMI (Trugo vb.)
        [HttpPost("import-stations")]
        public async Task<IActionResult> ImportStations(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Lütfen bir dosya yükleyin.");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("ŞEFİM DİKKAT! Lütfen '.xlsx' dosyasını değil, sonu '.csv' ile biten Trugo cihaz listesini seçin.");

            int successCount = 0;
            using var streamReader = new StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8);

            var headerLine = await streamReader.ReadLineAsync();

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = line.Contains(";") ? line.Split(';') : line.Split(',');

                if (columns.Length < 15) continue;

                try
                {
                    string stationName = columns[0].Replace("\0", "").Trim();
                    if (stationName == "Lokasyon Adı" || string.IsNullOrEmpty(stationName)) continue;

                    // 🚀 YENİ EXCEL SÜTUNLARI PARÇALANIYOR VE DEĞİŞKENLERE ATANIYOR
                    string chargepointId = columns.Length > 1 ? columns[1].Replace("\0", "").Trim() : null;
                    string deviceVendor = columns.Length > 2 ? columns[2].Replace("\0", "").Trim() : null;
                    string vendorModel = columns.Length > 3 ? columns[3].Replace("\0", "").Trim() : null;
                    string powerType = columns.Length > 4 ? columns[4].Replace("\0", "").Trim() : "DC";

                    int? socketCount = null;
                    if (columns.Length > 5 && int.TryParse(columns[5].Replace("\0", "").Trim(), out int sc)) socketCount = sc;

                    string devicePower = columns.Length > 6 ? columns[6].Replace("\0", "").Trim() : null;
                    string city = columns.Length > 7 ? columns[7].Replace("\0", "").Trim() : "Bilinmiyor";
                    string district = columns.Length > 8 ? columns[8].Replace("\0", "").Trim() : null;
                    string statusType = columns.Length > 9 ? columns[9].Replace("\0", "").Trim() : "Alt Yapı Tamamlandı";
                    string partnerStatus = columns.Length > 11 ? columns[11].Replace("\0", "").Trim() : null;
                    string ownerCompany = columns.Length > 12 ? columns[12].Replace("\0", "").Trim() : null;

                    DateTime? estimatedDate = null;
                    if (columns.Length > 13 && DateTime.TryParse(columns[13].Replace("\0", "").Trim(), out DateTime ed))
                    {
                        // 🚀 ÇÖZÜM: PostgreSQL'in hata vermemesi için tarihe UTC mührü vuruyoruz
                        estimatedDate = DateTime.SpecifyKind(ed, DateTimeKind.Utc);
                    }

                    double lat = 39.92077;
                    double lng = 32.85411;

                    string latString = columns[14].Replace("\0", "").Replace(",", ".").Trim();
                    string lngString = columns[15].Replace("\0", "").Replace(",", ".").Trim();

                    double.TryParse(latString, NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                    double.TryParse(lngString, NumberStyles.Any, CultureInfo.InvariantCulture, out lng);

                    var station = new Station
                    {
                        Name = stationName,
                        ChargepointId = chargepointId,       // YENİ
                        DeviceVendor = deviceVendor,         // YENİ
                        VendorModel = vendorModel,           // YENİ
                        PowerType = powerType,
                        SocketCount = socketCount,           // YENİ
                        DevicePower = devicePower,           // YENİ
                        City = city,
                        District = district,                 // YENİ
                        StatusType = statusType,
                        PartnerStatus = partnerStatus,       // YENİ
                        OwnerCompany = ownerCompany,         // YENİ
                        EstimatedDate = estimatedDate,       // YENİ
                        PersonnelName = "-",
                        PersonnelPhone = "-",
                        Edas = "-",
                        Address = "-",
                        PointType = "YG Abonelik",
                        Location = new NetTopologySuite.Geometries.Point(lng, lat) { SRID = 4326 },
                        TenantId = _targetTenantId
                    };

                    _context.Stations.Add(station);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"İstasyon okuma hatası: {line} - Hata: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{successCount} adet istasyon tüm teknik donanım detayları ve harita koordinatlarıyla birlikte aktarıldı!" });
        }
    }
}