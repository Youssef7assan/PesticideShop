using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PesticideShop.Migrations
{
    /// <inheritdoc />
    public partial class _5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyInventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscounts = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayments = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDebts = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionsCount = table.Column<int>(type: "int", nullable: false),
                    CustomersCount = table.Column<int>(type: "int", nullable: false),
                    ProductsSoldCount = table.Column<int>(type: "int", nullable: false),
                    TotalQuantitySold = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsibleUser = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyInventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyCustomerSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyInventoryId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    TransactionsCount = table.Column<int>(type: "int", nullable: false),
                    TotalPurchases = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayments = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DebtAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastTransactionTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCustomerSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCustomerSummaries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCustomerSummaries_DailyInventories_DailyInventoryId",
                        column: x => x.DailyInventoryId,
                        principalTable: "DailyInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyProductSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyInventoryId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TotalQuantitySold = table.Column<int>(type: "int", nullable: false),
                    TotalSalesValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCostValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscounts = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetSalesValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionsCount = table.Column<int>(type: "int", nullable: false),
                    StartingQuantity = table.Column<int>(type: "int", nullable: false),
                    EndingQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProductSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyProductSummaries_DailyInventories_DailyInventoryId",
                        column: x => x.DailyInventoryId,
                        principalTable: "DailyInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyProductSummaries_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailySaleTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyInventoryId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalTransactionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySaleTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailySaleTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailySaleTransactions_DailyInventories_DailyInventoryId",
                        column: x => x.DailyInventoryId,
                        principalTable: "DailyInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailySaleTransactions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCustomerSummaries_CustomerId",
                table: "DailyCustomerSummaries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCustomerSummaries_DailyInventoryId_CustomerId",
                table: "DailyCustomerSummaries",
                columns: new[] { "DailyInventoryId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyInventories_InventoryDate",
                table: "DailyInventories",
                column: "InventoryDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductSummaries_DailyInventoryId_ProductId",
                table: "DailyProductSummaries",
                columns: new[] { "DailyInventoryId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductSummaries_ProductId",
                table: "DailyProductSummaries",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySaleTransactions_CustomerId",
                table: "DailySaleTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySaleTransactions_DailyInventoryId",
                table: "DailySaleTransactions",
                column: "DailyInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySaleTransactions_ProductId",
                table: "DailySaleTransactions",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCustomerSummaries");

            migrationBuilder.DropTable(
                name: "DailyProductSummaries");

            migrationBuilder.DropTable(
                name: "DailySaleTransactions");

            migrationBuilder.DropTable(
                name: "DailyInventories");
        }
    }
}
