using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.DatabaseBackup
{
    public class BackupFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class RestoreBackupRequest
    {
        [Required]
        public string FileName { get; set; } = string.Empty;

        public bool ConfirmOverride { get; set; } = false;
    }
}
