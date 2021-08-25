using Microsoft.EntityFrameworkCore.Migrations;

namespace WalletWasabi.Backend.Migrations
{
    public partial class AddTokenStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tokens");
        }
    }
}
