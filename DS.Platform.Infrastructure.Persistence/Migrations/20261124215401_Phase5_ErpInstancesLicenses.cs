using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_ErpInstancesLicenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Erp");

            migrationBuilder.CreateTable(
                name: "Instances",
                schema: "Erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Server = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LicenseKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LicenseKeyHint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    KeyRotatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastContactOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastMigration = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Companies = table.Column<int>(type: "integer", nullable: true),
                    Users = table.Column<int>(type: "integer", nullable: true),
                    Branches = table.Column<int>(type: "integer", nullable: true),
                    DocumentsThisMonth = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Instances_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Core",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LicenseIssues",
                schema: "Erp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstanceId = table.Column<int>(type: "integer", nullable: false),
                    IssuedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CoveredUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    SuspendOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseIssues_Instances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "Erp",
                        principalTable: "Instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instances_CustomerId_Name",
                schema: "Erp",
                table: "Instances",
                columns: new[] { "CustomerId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Instances_LicenseKeyHash",
                schema: "Erp",
                table: "Instances",
                column: "LicenseKeyHash",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseIssues_InstanceId_IssuedOnUtc",
                schema: "Erp",
                table: "LicenseIssues",
                columns: new[] { "InstanceId", "IssuedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseIssues",
                schema: "Erp");

            migrationBuilder.DropTable(
                name: "Instances",
                schema: "Erp");
        }
    }
}
