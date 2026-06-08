using System.ComponentModel.DataAnnotations;

namespace Fametra.Api.Models
{
    public class PasswordResetCode
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
        public bool IsUsed { get; set; } = false;
    }
}
