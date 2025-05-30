using BrowserTool.Database.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace BrowserTool.Database
{
    public class ApplicationDbContext : DbContext
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sites.db");

        public DbSet<SiteGroup> SiteGroups { get; set; }
        public DbSet<SiteItem> SiteItems { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure SiteGroup
            modelBuilder.Entity<SiteGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            // Configure SiteItem
            modelBuilder.Entity<SiteItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CreateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.UseCommonCredentials).HasDefaultValue(false);
                entity.Property(e => e.AutoLogin).HasDefaultValue(false);
                entity.Property(e => e.AccessCount).HasDefaultValue(0);
                entity.Property(e => e.CaptchaMode).HasDefaultValue(0);

                // Configure relationship
                entity.HasOne(e => e.Group)
                    .WithMany(g => g.Sites)
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
} 