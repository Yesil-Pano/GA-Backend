using Microsoft.AspNetCore.Mvc;

namespace GA.Application.Features.Common
{
    /// <summary>
    /// Türkçe / Unicode dosya adlarında Content-Disposition header hatasını önler.
    /// ASP.NET <see cref="ControllerBase.File(byte[], string, string)"/> overload'unu kullanır.
    /// </summary>
    public static class FileDownloadResults
    {
        public static IActionResult FromBytes(
            byte[] data,
            string? contentType,
            string? fileName,
            string defaultFileName = "download")
        {
            var type = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            var name = string.IsNullOrWhiteSpace(fileName) ? defaultFileName : fileName.Trim();
            return new FileContentResult(data, type) { FileDownloadName = name };
        }
    }

}
