using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allows_daily",
                table: "service_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_postpaid",
                table: "service_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_upfront",
                table: "service_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "snapshot_payment_scheme",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allows_daily",
                table: "service_packages");

            migrationBuilder.DropColumn(
                name: "allows_postpaid",
                table: "service_packages");

            migrationBuilder.DropColumn(
                name: "allows_upfront",
                table: "service_packages");

            migrationBuilder.DropColumn(
                name: "snapshot_payment_scheme",
                table: "bookings");
        }
    }
}
