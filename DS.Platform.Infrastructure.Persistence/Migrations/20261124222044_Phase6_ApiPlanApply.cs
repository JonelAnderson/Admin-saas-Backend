using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_ApiPlanApply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanMaxDocuments",
                schema: "Ops",
                table: "RemoteActions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanSlug",
                schema: "Ops",
                table: "RemoteActions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiPlanSlug",
                schema: "Billing",
                table: "Plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanDesiredMaxDocuments",
                schema: "ApiSunat",
                table: "Issuers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanDesiredSlug",
                schema: "ApiSunat",
                table: "Issuers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanMaxDocuments",
                schema: "Ops",
                table: "RemoteActions");

            migrationBuilder.DropColumn(
                name: "PlanSlug",
                schema: "Ops",
                table: "RemoteActions");

            migrationBuilder.DropColumn(
                name: "ApiPlanSlug",
                schema: "Billing",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "PlanDesiredMaxDocuments",
                schema: "ApiSunat",
                table: "Issuers");

            migrationBuilder.DropColumn(
                name: "PlanDesiredSlug",
                schema: "ApiSunat",
                table: "Issuers");
        }
    }
}
