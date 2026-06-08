using System;
using System.Text.Json.Serialization;

namespace Fametra.Api.Models
{
    public class AppRestriction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MemberId { get; set; } = string.Empty;
        
        [JsonIgnore]
        public Member? Member { get; set; }

        public string AppName { get; set; } = string.Empty;
        public string AppIcon { get; set; } = "📱";
        public bool IsBlocked { get; set; } = false;
        public int DailyLimitMinutes { get; set; } = 60;
        public int UsedMinutesToday { get; set; } = 0;
    }
}
