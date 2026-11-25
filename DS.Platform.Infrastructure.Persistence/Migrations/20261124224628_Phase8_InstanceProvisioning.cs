using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_InstanceProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoolifyServiceUuid",
                schema: "Erp",
                table: "Instances",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployedVersion",
                schema: "Erp",
                table: "Instances",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstanceDeployments",
                schema: "Ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstanceId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    SecretsProtected = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
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
                    table.PrimaryKey("PK_InstanceDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstanceDeployments_Instances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "Erp",
                        principalTable: "Instances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceDeployments_InstanceId",
                schema: "Ops",
                table: "InstanceDeployments",
                column: "InstanceId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (1, 2, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_InstanceDeployments_Status_NextAttemptOnUtc",
                schema: "Ops",
                table: "InstanceDeployments",
                columns: new[] { "Status", "NextAttemptOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstanceDeployments",
                schema: "Ops");

            migrationBuilder.DropColumn(
                name: "CoolifyServiceUuid",
                schema: "Erp",
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "DeployedVersion",
                schema: "Erp",
                table: "Instances");
        }
    }
}
