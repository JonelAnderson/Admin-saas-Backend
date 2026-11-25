using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10_ApiCredentialDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCredentialDeliveries",
                schema: "Ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstanceId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    IssuerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReadyOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPulledOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApiKeyProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApiSecretProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ApiCredentialDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiCredentialDeliveries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Core",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApiCredentialDeliveries_Instances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "Erp",
                        principalTable: "Instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApiCredentialDeliveries_Issuers_IssuerId",
                        column: x => x.IssuerId,
                        principalSchema: "ApiSunat",
                        principalTable: "Issuers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentialDeliveries_CustomerId",
                schema: "Ops",
                table: "ApiCredentialDeliveries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentialDeliveries_InstanceId_Status",
                schema: "Ops",
                table: "ApiCredentialDeliveries",
                columns: new[] { "InstanceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentialDeliveries_IssuerId",
                schema: "Ops",
                table: "ApiCredentialDeliveries",
                column: "IssuerId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (1, 2, 3, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentialDeliveries_Status_NextAttemptOnUtc",
                schema: "Ops",
                table: "ApiCredentialDeliveries",
                columns: new[] { "Status", "NextAttemptOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCredentialDeliveries",
                schema: "Ops");
        }
    }
}
