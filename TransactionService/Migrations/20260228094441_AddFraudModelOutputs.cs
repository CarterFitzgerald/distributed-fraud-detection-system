using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionService.Migrations
{
    /// <inheritdoc />
    public partial class AddFraudModelOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FraudModelVersion",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FraudPrediction",
                table: "Transactions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "FraudProbability",
                table: "Transactions",
                type: "real",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FraudModelVersion",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FraudPrediction",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FraudProbability",
                table: "Transactions");
        }
    }
}
