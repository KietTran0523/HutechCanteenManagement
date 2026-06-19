using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyCanTeenHutech.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260620000100_AddSepayPayment")]
    public partial class AddSepayPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentCode",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Sepay");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepayTransactionId",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SepayTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SepayId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubAccount = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferAmount = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                    Accumulated = table.Column<decimal>(type: "decimal(18, 2)", nullable: true),
                    ReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SepayTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentCode",
                table: "Orders",
                column: "PaymentCode",
                unique: true,
                filter: "[PaymentCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SepayTransactions_SepayId",
                table: "SepayTransactions",
                column: "SepayId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SepayTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SepayTransactionId",
                table: "Orders");
        }
    }
}
