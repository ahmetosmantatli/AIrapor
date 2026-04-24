using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaAdsAnalyzer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectiveDiagnosticFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "directives",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "directives",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symptom",
                table: "directives",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "directives");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "directives");

            migrationBuilder.DropColumn(
                name: "Symptom",
                table: "directives");
        }
    }
}
