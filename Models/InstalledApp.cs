using System.ComponentModel.DataAnnotations;

namespace Fametra.Api.Models
{
    public class InstalledApp
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MemberId { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string IconBase64 { get; set; } = string.Empty;
    }
}
