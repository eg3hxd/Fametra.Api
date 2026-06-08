using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fametra.Api.Data;
using Fametra.Api.Models;
using System.Security.Cryptography;

namespace Fametra.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MembersController(AppDbContext context)
        {
            _context = context;
        }

        // ==================== MEMBER CRUD ====================

        public class CreateMemberRequest
        {
            public string UserId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string AvatarEmoji { get; set; } = "👦";
        }

        [HttpPost]
        public async Task<IActionResult> CreateMember([FromBody] CreateMemberRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("Kullanıcı bulunamadı");

            var member = new Member
            {
                UserId = user.Id,
                Name = request.Name,
                AvatarEmoji = string.IsNullOrEmpty(request.AvatarEmoji) ? "👦" : request.AvatarEmoji,
                DeviceName = "Yeni Cihaz",
                IsOnline = false,
                DailyScreenTimeLimitMinutes = 120,
                AppRestrictions = new List<AppRestriction>()
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync();
            return Ok(member);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMember(string id)
        {
            var member = await _context.Members
                .Include(m => m.AppRestrictions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (member == null) return NotFound();
            return Ok(member);
        }

        [HttpGet("by-user/{userId}")]
        public async Task<IActionResult> GetMembersByUser(string userId)
        {
            var members = await _context.Members
                .Include(m => m.AppRestrictions)
                .Where(m => m.UserId == userId)
                .ToListAsync();
            return Ok(members);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMember(string id, [FromBody] Member updatedMember)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            member.IsScreenMonitoringEnabled = updatedMember.IsScreenMonitoringEnabled;
            member.DailyScreenTimeLimitMinutes = updatedMember.DailyScreenTimeLimitMinutes;
            member.Name = updatedMember.Name;
            member.DeviceName = updatedMember.DeviceName;

            await _context.SaveChangesAsync();
            return Ok(member);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMember(string id)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            _context.Members.Remove(member);
            await _context.SaveChangesAsync();
            return Ok();
        }

        public class SyncInstalledAppsRequest
        {
            public List<InstalledAppDto> Apps { get; set; } = new();
        }

        public class InstalledAppDto
        {
            public string PackageName { get; set; } = string.Empty;
            public string AppName { get; set; } = string.Empty;
            public string IconBase64 { get; set; } = string.Empty;
        }

        [HttpPost("{id}/installed-apps")]
        public async Task<IActionResult> SyncInstalledApps(string id, [FromBody] SyncInstalledAppsRequest request)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            // Sadece loglamak için değil, tabloya da kaydediyoruz.
            var existingApps = await _context.InstalledApps.Where(a => a.MemberId == id).ToListAsync();
            _context.InstalledApps.RemoveRange(existingApps);

            var newApps = request.Apps.Select(a => new InstalledApp
            {
                MemberId = id,
                PackageName = a.PackageName,
                AppName = a.AppName,
                IconBase64 = a.IconBase64
            }).ToList();

            _context.InstalledApps.AddRange(newApps);
            await _context.SaveChangesAsync();

            // Gelen uygulamaları AppRestrictions'a da otomatik ekle (Eğer yoksa)
            var currentRestrictions = await _context.AppRestrictions.Where(r => r.MemberId == id).ToListAsync();
            foreach (var app in request.Apps)
            {
                if (!currentRestrictions.Any(r => r.AppName == app.AppName || r.AppName == app.PackageName))
                {
                    _context.AppRestrictions.Add(new AppRestriction
                    {
                        MemberId = id,
                        AppName = app.AppName,
                        AppIcon = app.IconBase64, // Real icon as base64 or emoji
                        DailyLimitMinutes = 0, // Sınırsız
                        IsBlocked = false
                    });
                }
            }
            await _context.SaveChangesAsync();

            return Ok();
        }

        // ==================== PAIRING ====================

        [HttpPost("pairing/generate")]
        public async Task<IActionResult> GeneratePairingCode([FromBody] CreateMemberRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("Kullanıcı bulunamadı");

            // Eski kullanılmamış kodları sil
            var oldCodes = await _context.PairingCodes
                .Where(p => p.UserId == request.UserId && !p.IsUsed)
                .ToListAsync();
            _context.PairingCodes.RemoveRange(oldCodes);

            // Yeni 6 haneli kod üret
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            int codeNum = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;

            var pairingCode = new PairingCode
            {
                UserId = request.UserId,
                Code = codeNum.ToString("D6"),
                MemberName = request.Name,
                AvatarEmoji = string.IsNullOrEmpty(request.AvatarEmoji) ? "👦" : request.AvatarEmoji,
                ExpiresAt = DateTime.UtcNow.AddSeconds(30)
            };

            _context.PairingCodes.Add(pairingCode);
            await _context.SaveChangesAsync();

            return Ok(new { pairingCode.Code, pairingCode.ExpiresAt, pairingCode.Id });
        }

        public class VerifyPairingRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string DeviceName { get; set; } = "Android Telefon";
        }

        [HttpPost("pairing/verify")]
        public async Task<IActionResult> VerifyPairingCode([FromBody] VerifyPairingRequest request)
        {
            var pairingCode = await _context.PairingCodes
                .FirstOrDefaultAsync(p => p.Code == request.Code && !p.IsUsed);

            if (pairingCode == null)
                return NotFound("Geçersiz eşleştirme kodu.");

            if (pairingCode.ExpiresAt < DateTime.UtcNow)
                return BadRequest("Bu kodun süresi dolmuş. Yeni bir kod alın.");

            var user = await _context.Users.FindAsync(pairingCode.UserId);
            if (user == null || !user.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("E-posta adresi ile kod eşleşmiyor.");
            }

            // Üye oluştur
            var member = new Member
            {
                UserId = pairingCode.UserId,
                Name = pairingCode.MemberName,
                AvatarEmoji = pairingCode.AvatarEmoji,
                DeviceName = request.DeviceName,
                IsOnline = true,
                DailyScreenTimeLimitMinutes = 120,
                AppRestrictions = new List<AppRestriction>
                {
                    new() { AppName = "YouTube", AppIcon = "▶️", DailyLimitMinutes = 60, UsedMinutesToday = 0 },
                    new() { AppName = "Instagram", AppIcon = "📷", DailyLimitMinutes = 30, UsedMinutesToday = 0 },
                    new() { AppName = "TikTok", AppIcon = "🎵", DailyLimitMinutes = 30, UsedMinutesToday = 0, IsBlocked = true },
                    new() { AppName = "Oyunlar", AppIcon = "🎮", DailyLimitMinutes = 45, UsedMinutesToday = 0 }
                }
            };

            _context.Members.Add(member);
            pairingCode.IsUsed = true;
            pairingCode.PairedMemberId = member.Id;

            await _context.SaveChangesAsync();

            return Ok(member);
        }

        // ==================== LOCATION ====================

        public class LocationUpdateRequest
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string LastLocationText { get; set; } = string.Empty;
        }

        [HttpPost("{id}/location")]
        public async Task<IActionResult> UpdateLocation(string id, [FromBody] LocationUpdateRequest request)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            member.Latitude = request.Latitude;
            member.Longitude = request.Longitude;
            member.LastLocationText = request.LastLocationText;
            member.LastLocationUpdate = DateTime.UtcNow;
            member.IsOnline = true;

            await _context.SaveChangesAsync();
            return Ok(new { member.Latitude, member.Longitude, member.LastLocationText });
        }

        // ==================== SCREEN TIME ====================

        public class ScreenTimeUpdateRequest
        {
            public int UsedMinutes { get; set; }
        }

        [HttpPost("{id}/screentime")]
        public async Task<IActionResult> UpdateScreenTime(string id, [FromBody] ScreenTimeUpdateRequest request)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            member.UsedScreenTimeMinutesToday = request.UsedMinutes;
            member.IsOnline = true;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                member.DailyScreenTimeLimitMinutes,
                member.UsedScreenTimeMinutesToday,
                RemainingMinutes = Math.Max(0, member.DailyScreenTimeLimitMinutes - member.UsedScreenTimeMinutesToday),
                IsLocked = member.UsedScreenTimeMinutesToday >= member.DailyScreenTimeLimitMinutes
            });
        }

        // ==================== APP RESTRICTIONS ====================

        [HttpGet("{id}/restrictions")]
        public async Task<IActionResult> GetRestrictions(string id)
        {
            var restrictions = await _context.AppRestrictions
                .Where(a => a.MemberId == id)
                .ToListAsync();
            return Ok(restrictions);
        }

        [HttpPost("{id}/restrictions")]
        public async Task<IActionResult> AddRestriction(string id, [FromBody] AppRestriction restriction)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            restriction.MemberId = id;
            restriction.Id = Guid.NewGuid().ToString();
            _context.AppRestrictions.Add(restriction);
            await _context.SaveChangesAsync();
            return Ok(restriction);
        }

        [HttpPut("restrictions/{restrictionId}")]
        public async Task<IActionResult> UpdateRestriction(string restrictionId, [FromBody] AppRestriction updated)
        {
            var restriction = await _context.AppRestrictions.FindAsync(restrictionId);
            if (restriction == null) return NotFound();

            restriction.IsBlocked = updated.IsBlocked;
            restriction.DailyLimitMinutes = updated.DailyLimitMinutes;
            restriction.UsedMinutesToday = updated.UsedMinutesToday;
            restriction.AppName = updated.AppName;
            restriction.AppIcon = updated.AppIcon;

            await _context.SaveChangesAsync();
            return Ok(restriction);
        }

        [HttpDelete("restrictions/{restrictionId}")]
        public async Task<IActionResult> DeleteRestriction(string restrictionId)
        {
            var restriction = await _context.AppRestrictions.FindAsync(restrictionId);
            if (restriction == null) return NotFound();

            _context.AppRestrictions.Remove(restriction);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== TIME REQUESTS ====================

        [HttpPost("{id}/time-request")]
        public async Task<IActionResult> RequestExtraTime(string id)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            var request = new TimeRequest
            {
                MemberId = id,
                RequestedMinutes = 15,
                Status = "pending"
            };

            _context.TimeRequests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(request);
        }

        [HttpGet("{id}/time-requests")]
        public async Task<IActionResult> GetTimeRequests(string id)
        {
            var requests = await _context.TimeRequests
                .Where(t => t.MemberId == id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return Ok(requests);
        }

        [HttpPost("time-request/{requestId}/approve")]
        public async Task<IActionResult> ApproveTimeRequest(string requestId)
        {
            var request = await _context.TimeRequests.FindAsync(requestId);
            if (request == null) return NotFound();

            request.Status = "approved";

            var member = await _context.Members.FindAsync(request.MemberId);
            if (member != null)
            {
                member.DailyScreenTimeLimitMinutes += request.RequestedMinutes;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                request.Status,
                member?.DailyScreenTimeLimitMinutes,
                member?.UsedScreenTimeMinutesToday,
                RemainingMinutes = Math.Max(0, (member?.DailyScreenTimeLimitMinutes ?? 0) - (member?.UsedScreenTimeMinutesToday ?? 0))
            });
        }

        [HttpPost("time-request/{requestId}/deny")]
        public async Task<IActionResult> DenyTimeRequest(string requestId)
        {
            var request = await _context.TimeRequests.FindAsync(requestId);
            if (request == null) return NotFound();

            request.Status = "denied";
            await _context.SaveChangesAsync();

            return Ok(request);
        }

        // ==================== ONLINE STATUS ====================

        [HttpPost("{id}/heartbeat")]
        public async Task<IActionResult> Heartbeat(string id)
        {
            var member = await _context.Members
                .Include(m => m.AppRestrictions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (member == null) return NotFound();

            member.IsOnline = true;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                member.DailyScreenTimeLimitMinutes,
                member.UsedScreenTimeMinutesToday,
                RemainingMinutes = Math.Max(0, member.DailyScreenTimeLimitMinutes - member.UsedScreenTimeMinutesToday),
                IsLocked = member.UsedScreenTimeMinutesToday >= member.DailyScreenTimeLimitMinutes,
                member.IsScreenMonitoringEnabled,
                Restrictions = member.AppRestrictions
            });
        }
    }
}
