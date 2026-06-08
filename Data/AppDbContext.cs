using Microsoft.EntityFrameworkCore;
using Fametra.Api.Models;

namespace Fametra.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<AppRestriction> AppRestrictions { get; set; }
        public DbSet<InstalledApp> InstalledApps { get; set; }
        public DbSet<PairingCode> PairingCodes { get; set; }
        public DbSet<TimeRequest> TimeRequests { get; set; }
        public DbSet<PasswordResetCode> PasswordResetCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Member>()
                .HasOne(m => m.User)
                .WithMany(u => u.Members)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppRestriction>()
                .HasOne(a => a.Member)
                .WithMany(m => m.AppRestrictions)
                .HasForeignKey(a => a.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
