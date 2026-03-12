using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FraudDetectionWorker.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerStateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerDeviceState",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDeviceState", x => new { x.CustomerId, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentTokenState",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaymentMethodToken = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentTokenState", x => new { x.CustomerId, x.PaymentMethodToken });
                });

            migrationBuilder.CreateTable(
                name: "CustomerProfileState",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HomeCountry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CustomerAgeYears = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfileState", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "MerchantCategoryRisk",
                columns: table => new
                {
                    MerchantCategory = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Risk = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantCategoryRisk", x => x.MerchantCategory);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDeviceState");

            migrationBuilder.DropTable(
                name: "CustomerPaymentTokenState");

            migrationBuilder.DropTable(
                name: "CustomerProfileState");

            migrationBuilder.DropTable(
                name: "MerchantCategoryRisk");
        }
    }
}
