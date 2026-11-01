using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1And2Implementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coworking_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<int>(type: "integer", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coworking_resources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    resource_type = table.Column<int>(type: "integer", nullable: false),
                    package_type = table.Column<int>(type: "integer", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    duration_hours = table.Column<int>(type: "integer", nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: true),
                    max_capacity = table.Column<int>(type: "integer", nullable: true),
                    is_student_eligible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    student_discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    downpayment_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 50m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    coworking_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_packages", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_packages_coworking_resources_coworking_resource_id",
                        column: x => x.coworking_resource_id,
                        principalTable: "coworking_resources",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    booking_type = table.Column<int>(type: "integer", nullable: false),
                    booking_status = table.Column<int>(type: "integer", nullable: false),
                    payment_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    front_desk_user_id = table.Column<string>(type: "text", nullable: true),
                    scheduled_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scheduled_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actual_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actual_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pax_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_student = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    snapshot_package_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    snapshot_price_per_unit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    snapshot_hourly_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    downpayment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    downpayment_paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    balance_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    overstay_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    reminder_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ending_reminder_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_bookings_coworking_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "coworking_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_service_packages_service_package_id",
                        column: x => x.service_package_id,
                        principalTable: "service_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_extensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    snapshot_hourly_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    processed_by_user_id = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_extensions", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_extensions_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_guests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    student_id_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_student_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_guests", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_guests_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    payment_type = table.Column<int>(type: "integer", nullable: false),
                    payment_validation_status = table.Column<int>(type: "integer", nullable: false),
                    proof_of_payment_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reference_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_payment_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    validated_by_user_id = table.Column<string>(type: "text", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_by_user_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_extensions_booking_id",
                table: "booking_extensions",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_guests_booking_id",
                table: "booking_guests",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_booking_payments_booking_id",
                table: "booking_payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_payments_external_payment_ref",
                table: "booking_payments",
                column: "external_payment_ref");

            migrationBuilder.CreateIndex(
                name: "ix_booking_payments_payment_validation_status",
                table: "booking_payments",
                column: "payment_validation_status");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_booking_status",
                table: "bookings",
                column: "booking_status");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_reference_number",
                table: "bookings",
                column: "reference_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_resource_id_scheduled_start_scheduled_end",
                table: "bookings",
                columns: new[] { "resource_id", "scheduled_start", "scheduled_end" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_scheduled_start",
                table: "bookings",
                column: "scheduled_start");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_service_package_id",
                table: "bookings",
                column: "service_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_coworking_resources_is_active",
                table: "coworking_resources",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_coworking_resources_resource_type",
                table: "coworking_resources",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "ix_service_packages_coworking_resource_id",
                table: "service_packages",
                column: "coworking_resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_packages_resource_type_is_active",
                table: "service_packages",
                columns: new[] { "resource_type", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_extensions");

            migrationBuilder.DropTable(
                name: "booking_guests");

            migrationBuilder.DropTable(
                name: "booking_payments");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "service_packages");

            migrationBuilder.DropTable(
                name: "coworking_resources");
        }
    }
}
