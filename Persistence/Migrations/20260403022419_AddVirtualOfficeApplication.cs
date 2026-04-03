using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualOfficeApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "virtual_office_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    country = table.Column<string>(type: "text", nullable: false),
                    business_type = table.Column<string>(type: "text", nullable: false),
                    needs_business_address = table.Column<bool>(type: "boolean", nullable: false),
                    needs_occasional_space = table.Column<bool>(type: "boolean", nullable: false),
                    duration_months = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    business_document_urls = table.Column<string>(type: "jsonb", nullable: false),
                    down_payment_proof_urls = table.Column<string>(type: "jsonb", nullable: false),
                    admin_notes = table.Column<string>(type: "text", nullable: true),
                    reviewed_by_user_id = table.Column<string>(type: "text", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_virtual_office_applications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_virtual_office_applications_email",
                table: "virtual_office_applications",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_virtual_office_applications_status",
                table: "virtual_office_applications",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "virtual_office_applications");
        }
    }
}
