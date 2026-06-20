using System;

namespace GA.Application.Features.Surveys.DTOs
{
    public class CreateSurveyRequest
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
    }
}