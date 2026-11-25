using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_ApiSunatIssuersRemoteActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ApiSunat");

            migrationBuilder.EnsureSchema(
                name: "Ops");

            migrationBuilder.CreateTable(
                name: "Issuers",
                schema: "ApiSunat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ApiPlan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DocumentsThisMonth = table.Column<int>(type: "integer", nullable: true),
                    MaxDocumentsMonth = table.Column<int>(type: "integer", nullable: true),
                    SuspendDesired = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendApplied = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_Issuers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issuers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Core",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemoteActions",
                schema: "Ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IssuerId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DoneOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_RemoteActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteActions_Issuers_IssuerId",
                        column: x => x.IssuerId,
                        principalSchema: "ApiSunat",
                        principalTable: "Issuers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issuers_CustomerId",
                schema: "ApiSunat",
                table: "Issuers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Issuers_Ruc",
                schema: "ApiSunat",
                table: "Issuers",
                column: "Ruc",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Issuers_TenantId",
                schema: "ApiSunat",
                table: "Issuers",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteActions_IdempotencyKey",
                schema: "Ops",
                table: "RemoteActions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemoteActions_IssuerId",
                schema: "Ops",
                table: "RemoteActions",
                column: "IssuerId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteActions_Status_NextAttemptOnUtc",
                schema: "Ops",
                table: "RemoteActions",
                columns: new[] { "Status", "NextAttemptOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteActions",
                schema: "Ops");

            migrationBuilder.DropTable(
                name: "Issuers",
                schema: "ApiSunat");
        }
    }
}
