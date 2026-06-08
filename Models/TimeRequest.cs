using System;

namespace Fametra.Api.Models
{
    public class TimeRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MemberId { get; set; } = string.Empty;
        public int RequestedMinutes { get; set; } = 15;
        public string Status { get; set; } = "pending"; // pending, approved, denied
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
