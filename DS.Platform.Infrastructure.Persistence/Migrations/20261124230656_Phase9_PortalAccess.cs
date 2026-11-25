using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9_PortalAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalAccesses",
                schema: "Ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ResetMfa = table.Column<bool>(type: "boolean", nullable: false),
                    ApiUserId = table.Column<long>(type: "bigint", nullable: true),
                    InvitedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitationExpiresOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitationCount = table.Column<int>(type: "integer", nullable: false),
                    WorkType = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PortalAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalAccesses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Core",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalAccessIssuers",
                schema: "Ops",
                columns: table => new
                {
                    PortalAccessId = table.Column<int>(type: "integer", nullable: false),
                    IssuerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAccessIssuers", x => new { x.PortalAccessId, x.IssuerId });
                    table.ForeignKey(
                        name: "FK_PortalAccessIssuers_Issuers_IssuerId",
                        column: x => x.IssuerId,
                        principalSchema: "ApiSunat",
                        principalTable: "Issuers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalAccessIssuers_PortalAccesses_PortalAccessId",
                        column: x => x.PortalAccessId,
                        principalSchema: "Ops",
                        principalTable: "PortalAccesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccesses_CustomerId",
                schema: "Ops",
                table: "PortalAccesses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccesses_Email",
                schema: "Ops",
                table: "PortalAccesses",
                column: "Email",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccesses_Status_NextAttemptOnUtc",
                schema: "Ops",
                table: "PortalAccesses",
                columns: new[] { "Status", "NextAttemptOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessIssuers_IssuerId",
                schema: "Ops",
                table: "PortalAccessIssuers",
                column: "IssuerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalAccessIssuers",
                schema: "Ops");

            migrationBuilder.DropTable(
                name: "PortalAccesses",
                schema: "Ops");
        }
    }
}
