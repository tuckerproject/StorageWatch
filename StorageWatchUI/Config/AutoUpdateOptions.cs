using System.ComponentModel.DataAnnotations;

namespace StorageWatchUI.Config
{
    public class AutoUpdateOptions
    {
        public const string SectionKey = "AutoUpdate";

        public bool Enabled { get; set; } = true;

        [StringLength(500, ErrorMessage = "ManifestUrl cannot exceed 500 characters")]
        public string ManifestUrl { get; set; } = string.Empty;

        [Range(1, 10080, ErrorMessage = "CheckIntervalMinutes must be between 1 and 10080")]
        public int CheckIntervalMinutes { get; set; } = 60;
    }
}
