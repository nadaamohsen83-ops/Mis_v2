using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalImportSourceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalIdentifier",
                table: "LegalClientAssignments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedFieldsJson",
                table: "LegalClientAssignments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalClientAssignments_ExternalIdentifier",
                table: "LegalClientAssignments",
                column: "ExternalIdentifier",
                filter: "\"ExternalIdentifier\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LegalClientAssignments_ExternalIdentifier",
                table: "LegalClientAssignments");

            migrationBuilder.DropColumn(
                name: "ExternalIdentifier",
                table: "LegalClientAssignments");

            migrationBuilder.DropColumn(
                name: "ImportedFieldsJson",
                table: "LegalClientAssignments");
        }
    }
}
