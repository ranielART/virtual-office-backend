using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentSchemeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing packages: all pre-existing packages had no scheme data,
            // so default them to Postpaid (the standard collect-at-checkout behaviour).
            migrationBuilder.Sql(
                "UPDATE service_packages SET allows_postpaid = true WHERE NOT allows_upfront AND NOT allows_postpaid AND NOT allows_daily;");

            // Backfill existing bookings: snapshot_payment_scheme was added with defaultValue 0
            // which is not a valid PaymentScheme enum value. Postpaid = 2.
            migrationBuilder.Sql(
                "UPDATE bookings SET snapshot_payment_scheme = 2 WHERE snapshot_payment_scheme = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-reversible data fix — no meaningful rollback.
        }
    }
}
