using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fametra.Api.Data;
using Fametra.Api.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fametra.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoogleAuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private static readonly HttpClient _httpClient = new();

        // Session store: sessionId -> user info (in-memory)
        private static readonly ConcurrentDictionary<string, GoogleSessionResult> _sessions = new();

        public GoogleAuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        /// <summary>
        /// Masaustu uygulamasi bu endpoint'i cagirir. Session ID ile birlikte Google'a yonlendirir.
        /// </summary>
        [HttpGet("login")]
        public IActionResult Login([FromQuery] string? session)
        {
            string clientId = _config["GoogleAuth:ClientId"] ?? "GIRILECEK_CLIENT_ID.apps.googleusercontent.com";
            string redirectUri = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/googleauth/callback";

            // Session bilgisini state parametresinde tasiyoruz
            string state = session ?? Guid.NewGuid().ToString("N");

            string url = $"https://accounts.google.com/o/oauth2/v2/auth" +
                         $"?client_id={clientId}" +
                         $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                         $"&response_type=code" +
                         $"&scope=email%20profile" +
                         $"&state={state}" +
                         $"&access_type=offline";

            return Redirect(url);
        }

        /// <summary>
        /// Google buraya yonlendirir. Code'u token'a cevirip kullanici bilgisini alir.
        /// </summary>
        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? error, [FromQuery] string? state)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return Content(ErrorPage($"Google girisi basarisiz: {error}"), "text/html; charset=utf-8");
            }

            if (string.IsNullOrEmpty(code))
            {
                return Content(ErrorPage("Google'dan yetkilendirme kodu alinamadi."), "text/html; charset=utf-8");
            }

            try
            {
                string clientId = _config["GoogleAuth:ClientId"] ?? "";
                string clientSecret = _config["GoogleAuth:ClientSecret"] ?? "";
                string redirectUri = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/googleauth/callback";

                var tokenRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
                });

                var tokenResponse = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    string errText = await tokenResponse.Content.ReadAsStringAsync();
                    return Content(ErrorPage($"Google Token Error: {errText}"), "text/html; charset=utf-8");
                }

                var tokenData = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
                string accessToken = tokenData.RootElement.GetProperty("access_token").GetString() ?? "";

                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var userInfoResponse = await _httpClient.SendAsync(request);
                var userInfoStr = await userInfoResponse.Content.ReadAsStringAsync();
                var userInfo = JsonDocument.Parse(userInfoStr);

                string email = userInfo.RootElement.GetProperty("email").GetString() ?? "";
                string name = userInfo.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? email : email;

                var user = await _context.Users.Include(u => u.Members).FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    string salt = Guid.NewGuid().ToString("N");
                    string hash = HashPassword(Guid.NewGuid().ToString(), salt);
                    user = new User
                    {
                        FullName = name,
                        Email = email,
                        Salt = salt,
                        PasswordHash = hash
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                if (!string.IsNullOrEmpty(state))
                {
                    _sessions[state] = new GoogleSessionResult
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        CompletedAt = DateTime.UtcNow
                    };
                }

                return Content(SuccessPage(name, email), "text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                return Content(ErrorPage($"Sistem hatası: {ex.Message}"), "text/html; charset=utf-8");
            }
        }

        /// <summary>
        /// Masaustu uygulamasi bu endpoint'i pollar. Google girisi tamamlaninca kullanici bilgisini doner.
        /// </summary>
        [HttpGet("check-session")]
        public IActionResult CheckSession([FromQuery] string session)
        {
            if (string.IsNullOrEmpty(session))
                return BadRequest("Session ID gerekli.");

            if (_sessions.TryRemove(session, out var result))
            {
                return Ok(new
                {
                    success = true,
                    userId = result.UserId,
                    email = result.Email,
                    fullName = result.FullName
                });
            }

            return Ok(new { success = false });
        }

        public class GoogleLoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string GoogleId { get; set; } = string.Empty;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Gecersiz Google bilgisi.");

            var user = await _context.Users.Include(u => u.Members).FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                string salt = Guid.NewGuid().ToString("N");
                string hash = HashPassword(Guid.NewGuid().ToString(), salt);
                user = new User
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    Salt = salt,
                    PasswordHash = hash
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            return Ok(user);
        }

        private string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private class GoogleSessionResult
        {
            public string UserId { get; set; } = "";
            public string Email { get; set; } = "";
            public string FullName { get; set; } = "";
            public DateTime CompletedAt { get; set; }
        }

        private string SuccessPage(string name, string email) => $@"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <title>Giriş Başarılı</title>
    <style>
        body {{
            margin: 0;
            padding: 0;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        .card {{
            background: #ffffff;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
            width: 400px;
            max-width: 90%;
        }}
        .icon {{
            width: 60px;
            height: 60px;
            background: #4caf50;
            border-radius: 50%;
            display: inline-flex;
            justify-content: center;
            align-items: center;
            margin-bottom: 20px;
            margin-left: auto;
            margin-right: auto;
        }}
        .icon svg {{
            fill: white;
            width: 30px;
            height: 30px;
        }}
        h2 {{ margin: 0 0 10px 0; color: #333; font-size: 24px; font-weight: bold; }}
        .name {{ color: #666; font-size: 16px; margin: 0 0 5px 0; }}
        .email {{ color: #999; font-size: 13px; margin: 0 0 30px 0; }}
        .info {{ color: #00b894; font-size: 14px; margin: 5px 0; font-weight: 600; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>
            <svg viewBox='0 0 24 24'><path d='M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z'/></svg>
        </div>
        <h2>Google ile Giris Basarili!</h2>
        <p class='name'>Hosgeldiniz, {name}</p>
        <p class='email'>{email}</p>
        <p class='info'>Bu pencereyi kapatabilirsiniz.</p>
        <p class='info'>Uygulama otomatik giris yapacak.</p>
        <script>
            setTimeout(function() {{ window.close(); }}, 3000);
        </script>
    </div>
</body>
</html>";

        private string ErrorPage(string message) => $@"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <title>Giriş Başarısız</title>
    <style>
        body {{
            margin: 0;
            padding: 0;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #ff758c 0%, #ff7eb3 100%);
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        .card {{
            background: #ffffff;
            border-radius: 20px;
            padding: 40px;
            text-align: center;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
            width: 400px;
            max-width: 90%;
        }}
        .icon {{
            width: 60px;
            height: 60px;
            background: #ff4757;
            border-radius: 50%;
            display: inline-flex;
            justify-content: center;
            align-items: center;
            margin-bottom: 20px;
            margin-left: auto;
            margin-right: auto;
        }}
        .icon svg {{
            fill: white;
            width: 30px;
            height: 30px;
        }}
        h2 {{ margin: 0 0 10px 0; color: #333; font-size: 24px; font-weight: bold; }}
        .error {{ color: #ff4757; font-size: 16px; margin: 0 0 30px 0; }}
        .info {{ color: #666; font-size: 14px; margin: 5px 0; font-weight: 600; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>
            <svg viewBox='0 0 24 24'><path d='M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z'/></svg>
        </div>
        <h2>Google ile Giris Basarisiz!</h2>
        <p class='error'>{message}</p>
        <p class='info'>Lutfen bu pencereyi kapatip tekrar deneyin.</p>
    </div>
</body>
</html>";
    }
}