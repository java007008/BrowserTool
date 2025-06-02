using Microsoft.EntityFrameworkCore.Migrations;

namespace BrowserTool.Database.Migrations
{
    public partial class AddIsDefaultExpandedToSiteGroup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultExpanded",
                table: "SiteGroups",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultExpanded",
                table: "SiteGroups");
        }
    }
}
