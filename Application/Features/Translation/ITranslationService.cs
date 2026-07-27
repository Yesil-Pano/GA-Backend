namespace GA.Application.Features.Translation
{
    public record WorkOrderTranslationResult(
        string TitleEn,
        string DescriptionEn,
        string MobileDescriptionEn,
        string? FieldNoteEn,
        string Provider);

    public interface ITranslationService
    {
        Task<WorkOrderTranslationResult> TranslateWorkOrderAsync(
            string title,
            string description,
            string mobileDescription,
            string? fieldNote,
            CancellationToken ct = default);
    }
}
