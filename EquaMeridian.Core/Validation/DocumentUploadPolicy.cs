using Microsoft.AspNetCore.Http;

namespace EquaMeridian.Core.Validation
{
    public static class DocumentUploadPolicy
    {
        public static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
        public const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public const string MaxFileSizeLabel = "10MB";
        public static string? Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return "One of the selected files is empty or could not be read.";

            if (file.Length > MaxFileSizeBytes)
                return $"'{file.FileName}' is too large. Maximum file size is {MaxFileSizeLabel}.";

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return $"'{file.FileName}' has an unsupported format. Please upload PDF, JPG, or PNG files only.";

            return null;
        }
    }
}
