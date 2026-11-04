using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sensei.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalSignInProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_login_providers_UserId",
                schema: "identity",
                table: "login_providers");

            migrationBuilder.CreateTable(
                name: "connected_profiles",
                schema: "identity",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProfileUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connected_profiles", x => new { x.Provider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_connected_profiles_login_accounts_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "login_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_connected_profiles_login_providers_Provider_ProviderKey",
                        columns: x => new { x.Provider, x.ProviderKey },
                        principalSchema: "identity",
                        principalTable: "login_providers",
                        principalColumns: new[] { "LoginProvider", "ProviderKey" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_auth_flows",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BindingHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReturnPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChallengedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_auth_flows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_login_providers_UserId_LoginProvider",
                schema: "identity",
                table: "login_providers",
                columns: new[] { "UserId", "LoginProvider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connected_profiles_OwnerId_Provider",
                schema: "identity",
                table: "connected_profiles",
                columns: new[] { "OwnerId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_auth_flows_ExpiresAt",
                schema: "identity",
                table: "external_auth_flows",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connected_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "external_auth_flows",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_login_providers_UserId_LoginProvider",
                schema: "identity",
                table: "login_providers");

            migrationBuilder.CreateIndex(
                name: "IX_login_providers_UserId",
                schema: "identity",
                table: "login_providers",
                column: "UserId");
        }
    }
}
