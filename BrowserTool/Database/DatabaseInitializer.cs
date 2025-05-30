using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;

namespace BrowserTool.Database
{
    /// <summary>
    /// 数据库初始化器
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// 初始化数据库
        /// </summary>
        public static void Initialize()
        {
            // 确保数据库目录存在
            var dbPath = GetDbPath();
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            using (var context = new AppDbContext())
            {
                try
                {
                    // 确保数据库存在
                    context.Database.EnsureCreated();

                    // 检查是否需要迁移
                    var pendingMigrations = context.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        context.Database.Migrate();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"初始化数据库时出错：{ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 获取数据库文件路径
        /// </summary>
        /// <returns>数据库文件的完整路径</returns>
        public static string GetDbPath()
        {
            try
            {
                // 获取应用程序目录
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var dbPath = Path.Combine(appPath, "sites.db");
                return dbPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取数据库路径时出错：{ex.Message}", ex);
            }
        }
    }
} 