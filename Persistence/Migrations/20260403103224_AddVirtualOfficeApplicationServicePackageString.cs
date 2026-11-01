using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualOfficeApplicationServicePackageString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "service_package",
                table: "virtual_office_applications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_virtual_office_applications_service_package",
                table: "virtual_office_applications",
                column: "service_package");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_virtual_office_applications_service_package",
                table: "virtual_office_applications");

            migrationBuilder.DropColumn(
                name: "service_package",
                table: "virtual_office_applications");
        }
    }
}
