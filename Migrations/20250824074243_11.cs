using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesticideShop.Migrations
{
	/// <inheritdoc />
	public partial class _11 : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "ReturnTrackings",
				columns: table => new
				{
					Id = table.Column<int>(type: "int", nullable: false)
						.Annotation("SqlServer:Identity", "1, 1"),
					OriginalInvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
					ProductId = table.Column<int>(type: "int", nullable: false),
					ReturnedQuantity = table.Column<int>(type: "int", nullable: false),
					ReturnInvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
					ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
					ReturnReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
					CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
					CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ReturnTrackings", x => x.Id);
					table.ForeignKey(
						name: "FK_ReturnTrackings_Products_ProductId",
						column: x => x.ProductId,
						principalTable: "Products",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_ReturnTrackings_ProductId",
				table: "ReturnTrackings",
				column: "ProductId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "ReturnTrackings");
		}
	}
}
