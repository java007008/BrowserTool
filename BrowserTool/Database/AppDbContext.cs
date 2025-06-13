using Microsoft.EntityFrameworkCore;
using System;

namespace BrowserTool.Database
{
    /// <summary>
    /// 应用程序数据库上下文
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// 站点组集合
        /// </summary>
        public DbSet<Entities.SiteGroup> SiteGroups { get; set; }

        /// <summary>
        /// 站点项集合
        /// </summary>
        public DbSet<Entities.SiteItem> SiteItems { get; set; }

        /// <summary>
        /// 配置数据库连接
        /// </summary>
        /// <param name="optionsBuilder">数据库上下文选项构建器</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            try
            {
                var dbPath = DatabaseInitializer.GetDbPath();
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"配置数据库连接时出错：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 配置实体模型
        /// </summary>
        /// <param name="modelBuilder">模型构建器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            try
            {
                // 配置 SiteGroup 实体
                modelBuilder.Entity<Entities.SiteGroup>(entity =>
                {
                    entity.HasKey(e => e.Id);
                    entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                    entity.Property(e => e.SortOrder).HasDefaultValue(0);
                    entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                    entity.Property(e => e.IsDefaultExpanded).HasDefaultValue(false);
                    entity.Property(e => e.CreateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                    entity.Property(e => e.UpdateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                });

                // 配置 SiteItem 实体
                modelBuilder.Entity<Entities.SiteItem>(entity =>
                {
                    entity.HasKey(e => e.Id);
                    entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
                    entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
                    entity.Property(e => e.SortOrder).HasDefaultValue(0);
                    entity.Property(e => e.Username).HasMaxLength(200);
                    entity.Property(e => e.Password).HasMaxLength(200);
                    entity.Property(e => e.CommonUsername).HasMaxLength(200);
                    entity.Property(e => e.CommonPassword).HasMaxLength(200);
                    entity.Property(e => e.UseCommonCredentials).HasDefaultValue(false);
                    entity.Property(e => e.AutoLogin).HasDefaultValue(false);
                    entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                    entity.Property(e => e.Icon).HasMaxLength(1000);
                    entity.Property(e => e.Description).HasMaxLength(1000);
                    entity.Property(e => e.Tags).HasMaxLength(500);
                    entity.Property(e => e.CreateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                    entity.Property(e => e.UpdateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
                    entity.Property(e => e.AccessCount).HasDefaultValue(0);
                    entity.Property(e => e.UsernameSelector).HasMaxLength(200);
                    entity.Property(e => e.PasswordSelector).HasMaxLength(200);
                    entity.Property(e => e.CaptchaSelector).HasMaxLength(200);
                    entity.Property(e => e.LoginButtonSelector).HasMaxLength(200);
                    entity.Property(e => e.LoginPageFeature).HasMaxLength(500);
                    entity.Property(e => e.CaptchaValue).HasMaxLength(200);
                    entity.Property(e => e.CaptchaMode).HasDefaultValue(0);
                    entity.Property(e => e.GoogleSecret).HasMaxLength(200);

                    // 配置外键关系
                    entity.HasOne(e => e.Group)
                        .WithMany(g => g.Sites)
                        .HasForeignKey(e => e.GroupId)
                        .OnDelete(DeleteBehavior.Cascade);
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"配置实体模型时出错：{ex.Message}", ex);
            }
        }
    }
} 