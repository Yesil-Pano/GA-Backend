namespace GA.Application.Features.Auth.DTOs
{
    public class UpdateWorkOrderStatusDto
    {
        public required string Status { get; set; }

        /// <summary>Sahadan girilen not — Tamamla/İptal işleminde zorunlu tutulur (mobil tarafı kontrol eder)</summary>
        public string? FieldNote { get; set; }
    }
}
