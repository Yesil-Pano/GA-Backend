using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyFormFieldsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Surveys",
                newName: "Q9_HasPanelFrameDamage");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Surveys",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Surveys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Q10_GroundingSystemAppropriate",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q11_HasEquipotentialBusbar",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q12_HasInternalLightingAndSocket",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q13_HasWarningLabelsAndInsulatedMat",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q14_FirePreventionSolutionCleaned",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q15_IsOccupationalHealthCompliant",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q16_AreShuntReactorsActive",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q17_AreCapacitorCurrentsNominal",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q18_HasBlownCapacitor",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q19_HasDefectiveContactor",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q1_MeterSerialRead",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q20_AreCableCrossSectionsAppropriate",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q21_IsThermalCameraImagingDone",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q22_IsModemGprsOnline",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q23_IsRelayPowerFactorOne",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q24_HasRelayScreenWarning",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q25_AreReactiveValuesBelowPenaltyLimit",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q26_AreOsosModemInfosRecorded",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q27_HasActiveOsosInMeter",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q28_AreKakrAndToroidValuesAppropriate",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q2_PanelCleaning",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q3_FireExtinguisherPressure",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q4_MaintenanceFormSigned",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q5_FanWorkingAndSufficient",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q6_HasCoolingFan",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q7_SwitchgearScrewsChecked",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Q8_PanelDoorLockable",
                table: "Surveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Surveys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubTitle",
                table: "Surveys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Surveys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q10_GroundingSystemAppropriate",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q11_HasEquipotentialBusbar",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q12_HasInternalLightingAndSocket",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q13_HasWarningLabelsAndInsulatedMat",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q14_FirePreventionSolutionCleaned",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q15_IsOccupationalHealthCompliant",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q16_AreShuntReactorsActive",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q17_AreCapacitorCurrentsNominal",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q18_HasBlownCapacitor",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q19_HasDefectiveContactor",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q1_MeterSerialRead",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q20_AreCableCrossSectionsAppropriate",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q21_IsThermalCameraImagingDone",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q22_IsModemGprsOnline",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q23_IsRelayPowerFactorOne",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q24_HasRelayScreenWarning",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q25_AreReactiveValuesBelowPenaltyLimit",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q26_AreOsosModemInfosRecorded",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q27_HasActiveOsosInMeter",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q28_AreKakrAndToroidValuesAppropriate",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q2_PanelCleaning",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q3_FireExtinguisherPressure",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q4_MaintenanceFormSigned",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q5_FanWorkingAndSufficient",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q6_HasCoolingFan",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q7_SwitchgearScrewsChecked",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Q8_PanelDoorLockable",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "SubTitle",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Surveys");

            migrationBuilder.RenameColumn(
                name: "Q9_HasPanelFrameDamage",
                table: "Surveys",
                newName: "IsActive");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Surveys",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
