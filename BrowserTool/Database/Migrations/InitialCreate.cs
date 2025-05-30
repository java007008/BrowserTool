using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BrowserTool.Database.Migrations
{
    public partial class InitialCreate : Migration
    {
        /// <summary>
        /// 创建数据库表结构
        /// </summary>
        /// <param name="migrationBuilder">迁移构建器</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdateTime = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CommonUsername = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CommonPassword = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UseCommonCredentials = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AutoLogin = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdateTime = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastAccessTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UsernameSelector = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PasswordSelector = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CaptchaSelector = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LoginButtonSelector = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LoginPageFeature = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CaptchaValue = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CaptchaMode = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    GoogleSecret = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteItems_SiteGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SiteGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteItems_GroupId",
                table: "SiteItems",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteItems_SortOrder",
                table: "SiteItems",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SiteGroups_SortOrder",
                table: "SiteGroups",
                column: "SortOrder");
        }

        /// <summary>
        /// 删除数据库表结构
        /// </summary>
        /// <param name="migrationBuilder">迁移构建器</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteItems");

            migrationBuilder.DropTable(
                name: "SiteGroups");
        }
    }
} 