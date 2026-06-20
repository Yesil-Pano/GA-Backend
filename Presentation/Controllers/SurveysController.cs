using GA.Application.Features.Surveys.DTOs;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace GA.Presentation.Controllers
{
    // DTO Sözleşmesini Controller içinde genişletiyoruz şefim
    public class CreateSurveyBackendRequest
    {
        public bool Q1_MeterSerialRead { get; set; }
        public bool Q2_PanelCleaning { get; set; }
        public bool Q3_FireExtinguisherPressure { get; set; }
        public bool Q4_MaintenanceFormSigned { get; set; }
        public bool Q5_FanWorkingAndSufficient { get; set; }
        public bool Q6_HasCoolingFan { get; set; }
        public bool Q7_SwitchgearScrewsChecked { get; set; }
        public bool Q8_PanelDoorLockable { get; set; }
        public bool Q9_HasPanelFrameDamage { get; set; }
        public bool Q10_GroundingSystemAppropriate { get; set; }
        public bool Q11_HasEquipotentialBusbar { get; set; }
        public bool Q12_HasInternalLightingAndSocket { get; set; }
        public bool Q13_HasWarningLabelsAndInsulatedMat { get; set; }
        public bool Q14_FirePreventionSolutionCleaned { get; set; }
        public bool Q15_IsOccupationalHealthCompliant { get; set; }
        public bool Q16_AreShuntReactorsActive { get; set; }
        public bool Q17_AreCapacitorCurrentsNominal { get; set; }
        public bool Q18_HasBlownCapacitor { get; set; }
        public bool Q19_HasDefectiveContactor { get; set; }
        public bool Q20_AreCableCrossSectionsAppropriate { get; set; }
        public bool Q21_IsThermalCameraImagingDone { get; set; }
        public bool Q22_IsModemGprsOnline { get; set; }
        public bool Q23_IsRelayPowerFactorOne { get; set; }
        public bool Q24_HasRelayScreenWarning { get; set; }
        public bool Q25_AreReactiveValuesBelowPenaltyLimit { get; set; }
        public bool Q26_AreOsosModemInfosRecorded { get; set; }
        public bool Q27_HasActiveOsosInMeter { get; set; }
        public bool Q28_AreKakrAndToroidValuesAppropriate { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class SurveysController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SurveysController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var surveys = await _context.Surveys.ToListAsync();
            return Ok(surveys);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSurveyBackendRequest request)
        {
            var survey = new Survey
            {
                Id = Guid.NewGuid(),
                Q1_MeterSerialRead = request.Q1_MeterSerialRead,
                Q2_PanelCleaning = request.Q2_PanelCleaning,
                Q3_FireExtinguisherPressure = request.Q3_FireExtinguisherPressure,
                Q4_MaintenanceFormSigned = request.Q4_MaintenanceFormSigned,
                Q5_FanWorkingAndSufficient = request.Q5_FanWorkingAndSufficient,
                Q6_HasCoolingFan = request.Q6_HasCoolingFan,
                Q7_SwitchgearScrewsChecked = request.Q7_SwitchgearScrewsChecked,
                Q8_PanelDoorLockable = request.Q8_PanelDoorLockable,
                Q9_HasPanelFrameDamage = request.Q9_HasPanelFrameDamage,
                Q10_GroundingSystemAppropriate = request.Q10_GroundingSystemAppropriate,
                Q11_HasEquipotentialBusbar = request.Q11_HasEquipotentialBusbar,
                Q12_HasInternalLightingAndSocket = request.Q12_HasInternalLightingAndSocket,
                Q13_HasWarningLabelsAndInsulatedMat = request.Q13_HasWarningLabelsAndInsulatedMat,
                Q14_FirePreventionSolutionCleaned = request.Q14_FirePreventionSolutionCleaned,
                Q15_IsOccupationalHealthCompliant = request.Q15_IsOccupationalHealthCompliant,
                Q16_AreShuntReactorsActive = request.Q16_AreShuntReactorsActive,
                Q17_AreCapacitorCurrentsNominal = request.Q17_AreCapacitorCurrentsNominal,
                Q18_HasBlownCapacitor = request.Q18_HasBlownCapacitor,
                Q19_HasDefectiveContactor = request.Q19_HasDefectiveContactor,
                Q20_AreCableCrossSectionsAppropriate = request.Q20_AreCableCrossSectionsAppropriate,
                Q21_IsThermalCameraImagingDone = request.Q21_IsThermalCameraImagingDone,
                Q22_IsModemGprsOnline = request.Q22_IsModemGprsOnline,
                Q23_IsRelayPowerFactorOne = request.Q23_IsRelayPowerFactorOne,
                Q24_HasRelayScreenWarning = request.Q24_HasRelayScreenWarning,
                Q25_AreReactiveValuesBelowPenaltyLimit = request.Q25_AreReactiveValuesBelowPenaltyLimit,
                Q26_AreOsosModemInfosRecorded = request.Q26_AreOsosModemInfosRecorded,
                Q27_HasActiveOsosInMeter = request.Q27_HasActiveOsosInMeter,
                Q28_AreKakrAndToroidValuesAppropriate = request.Q28_AreKakrAndToroidValuesAppropriate,
                Description = request.Description,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                CreatedAt = DateTime.UtcNow
            };

            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync();
            return Ok(survey);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var survey = await _context.Surveys.FindAsync(id);
            if (survey == null) return NotFound();

            _context.Surveys.Remove(survey);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Anket başarıyla silindi." });
        }
    }
}