using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesticideShop.Migrations
{
    /// <inheritdoc />
    public partial class _17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SocialMediaName",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "SocialMediaName",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
