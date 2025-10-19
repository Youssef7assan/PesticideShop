using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesticideShop.Migrations
{
    /// <inheritdoc />
    public partial class _12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExchangeTrackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalInvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeInvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldProductId = table.Column<int>(type: "int", nullable: false),
                    NewProductId = table.Column<int>(type: "int", nullable: false),
                    ExchangedQuantity = table.Column<int>(type: "int", nullable: false),
                    PriceDifference = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExchangeReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExchangeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldProductNavigationId = table.Column<int>(type: "int", nullable: true),
                    NewProductNavigationId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeTrackings_Products_NewProductId",
                        column: x => x.NewProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeTrackings_Products_NewProductNavigationId",
                        column: x => x.NewProductNavigationId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExchangeTrackings_Products_OldProductId",
                        column: x => x.OldProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeTrackings_Products_OldProductNavigationId",
                        column: x => x.OldProductNavigationId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeTrackings_NewProductId",
                table: "ExchangeTrackings",
                column: "NewProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeTrackings_NewProductNavigationId",
                table: "ExchangeTrackings",
                column: "NewProductNavigationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeTrackings_OldProductId",
                table: "ExchangeTrackings",
                column: "OldProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeTrackings_OldProductNavigationId",
                table: "ExchangeTrackings",
                column: "OldProductNavigationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeTrackings");
        }
    }
}
