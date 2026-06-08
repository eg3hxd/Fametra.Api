using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fametra.Api.Data;
using Fametra.Api.Models;
using System.Security.Cryptography;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;

namespace Fametra.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class RegisterRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Bu e-posta zaten kullanımda.");

            string salt = Guid.NewGuid().ToString("N");
            string hash = HashPassword(request.Password, salt);

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                Salt = salt,
                PasswordHash = hash
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Email, user.FullName });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.Include(u => u.Members).FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                return Unauthorized("Kullanıcı bulunamadı.");

            if (user.PasswordHash != HashPassword(request.Password, user.Salt))
                return Unauthorized("Şifre hatalı.");

            return Ok(user);
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                return Ok(); // Güvenlik: E-posta bulunamasa bile bulundu gibi davran

            var code = new Random().Next(100000, 999999).ToString();
            
            var resetCode = new PasswordResetCode
            {
                UserId = user.Id,
                Code = code
            };
            _context.PasswordResetCodes.Add(resetCode);
            await _context.SaveChangesAsync();

            // Konsola da yazdır (debug için)
            Console.WriteLine($"\n===============================\n[SIFRE SIFIRLAMA]\nKullanıcı: {user.Email}\nKod: {code}\n===============================\n");

            // SMTP ile gerçek e-posta gönder
            try
            {
                await SendResetEmailAsync(user.Email, user.FullName, code);
                Console.WriteLine($"[E-POSTA] {user.Email} adresine kod gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[E-POSTA HATASI] {ex.Message}");
                // E-posta gönderilemese bile kod veritabanına kaydedildi, konsoldaki kodu kullanabilir
            }

            return Ok(new { message = "Eğer sistemimizde bu e-posta adresi kayıtlıysa, sıfırlama kodu gönderilmiştir." });
        }

        private async Task SendResetEmailAsync(string toEmail, string toName, string code)
        {
            var smtp = _config.GetSection("SmtpSettings");
            string host = smtp["Host"] ?? "smtp.gmail.com";
            int port = int.Parse(smtp["Port"] ?? "587");
            string username = smtp["Username"] ?? "";
            string password = smtp["Password"] ?? "";
            string fromEmail = smtp["FromEmail"] ?? username;
            string fromName = smtp["FromName"] ?? "Fametra";

            // Eğer SMTP ayarlanmadıysa atla
            if (string.IsNullOrEmpty(username) || username.Contains("BURAYA"))
            {
                Console.WriteLine("[E-POSTA] SMTP ayarlanmamış, e-posta gönderilmedi. Konsoldaki kodu kullanın.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = "Fametra - Şifre Sıfırlama Kodu";

            message.Body = new TextPart("html")
            {
                Text = $@"
                <div style='font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px; background: #f8f9fa; border-radius: 16px;'>
                    <h2 style='color: #2d3436; text-align: center;'>Şifre Sıfırlama</h2>
                    <p style='color: #636e72; text-align: center;'>Merhaba <strong>{toName}</strong>,</p>
                    <p style='color: #636e72; text-align: center;'>Şifrenizi sıfırlamak için aşağıdaki 6 haneli kodu kullanın:</p>
                    <div style='background: #6c5ce7; color: white; font-size: 32px; font-weight: bold; text-align: center; padding: 16px; border-radius: 12px; letter-spacing: 8px; margin: 24px 0;'>
                        {code}
                    </div>
                    <p style='color: #b2bec3; font-size: 12px; text-align: center;'>Bu kod 15 dakika içinde geçerliliğini yitirecektir.</p>
                    <p style='color: #b2bec3; font-size: 12px; text-align: center;'>Bu işlemi siz yapmadıysanız bu e-postayı görmezden gelin.</p>
                </div>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public class ResetPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return BadRequest("Kullanıcı bulunamadı.");

            var resetCode = await _context.PasswordResetCodes
                .Where(r => r.UserId == user.Id && r.Code == request.Code && !r.IsUsed && r.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetCode == null) return BadRequest("Geçersiz veya süresi dolmuş kod.");

            resetCode.IsUsed = true;
            
            string newSalt = Guid.NewGuid().ToString("N");
            user.Salt = newSalt;
            user.PasswordHash = HashPassword(request.NewPassword, newSalt);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Şifreniz başarıyla güncellendi." });
        }

        public class ChangePasswordRequest
        {
            public string Email { get; set; } = string.Empty;
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return BadRequest("Kullanıcı bulunamadı.");

            // Mevcut şifreyi doğrula
            if (user.PasswordHash != HashPassword(request.CurrentPassword, user.Salt))
                return Unauthorized("Mevcut şifre hatalı.");

            // Yeni şifreyi kaydet
            string newSalt = Guid.NewGuid().ToString("N");
            user.Salt = newSalt;
            user.PasswordHash = HashPassword(request.NewPassword, newSalt);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Şifreniz başarıyla güncellendi." });
        }

        private string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
