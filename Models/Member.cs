using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fametra.Api.Models
{
    public class Member
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        
        [JsonIgnore]
        public User? User { get; set; }

        public string Name { get; set; } = string.Empty;
        public string AvatarEmoji { get; set; } = "👦";
        public string DeviceName { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = false;

        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public string LastLocationText { get; set; } = "Konum alınamadı";
        public DateTime LastLocationUpdate { get; set; } = DateTime.MinValue;

        public bool IsScreenMonitoringEnabled { get; set; } = false;
        public int DailyScreenTimeLimitMinutes { get; set; } = 120;
        public int UsedScreenTimeMinutesToday { get; set; } = 0;

        public ICollection<AppRestriction> AppRestrictions { get; set; } = new List<AppRestriction>();
    }
}
