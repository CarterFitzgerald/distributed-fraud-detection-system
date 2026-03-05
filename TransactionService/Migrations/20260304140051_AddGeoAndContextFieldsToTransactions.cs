using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionService.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoAndContextFieldsToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerHomeCountry",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Transactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Transactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantRiskTier",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerHomeCountry",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MerchantRiskTier",
                table: "Transactions");
        }
    }
}
