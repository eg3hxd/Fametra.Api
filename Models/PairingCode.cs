using System;

namespace Fametra.Api.Models
{
    public class PairingCode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string AvatarEmoji { get; set; } = "👦";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddSeconds(30);
        public bool IsUsed { get; set; } = false;
        public string? PairedMemberId { get; set; } = null;
    }
}
