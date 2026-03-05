using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionService.Migrations
{
    /// <inheritdoc />
    public partial class AddFraudModelFeatureColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountAgeDays",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerAge",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DistanceFromHomeKm",
                table: "Transactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInternational",
                table: "Transactions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNewDevice",
                table: "Transactions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNewPaymentToken",
                table: "Transactions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MccRisk",
                table: "Transactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantCategory",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethodAgeDays",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmountLast24h",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TxnCountLast1h",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TxnCountLast24h",
                table: "Transactions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountAgeDays",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CustomerAge",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DistanceFromHomeKm",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsInternational",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsNewDevice",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsNewPaymentToken",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MccRisk",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MerchantCategory",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentMethodAgeDays",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TotalAmountLast24h",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TxnCountLast1h",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TxnCountLast24h",
                table: "Transactions");
        }
    }
}
