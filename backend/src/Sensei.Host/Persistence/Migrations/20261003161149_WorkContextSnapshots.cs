using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sensei.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkContextSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "accepted_interpretation_id",
                schema: "work_reflection",
                table: "work_episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "context_number",
                schema: "work_reflection",
                table: "work_episodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "current_snapshot_id",
                schema: "work_reflection",
                table: "work_episodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_work_episodes_owner_id_id",
                schema: "work_reflection",
                table: "work_episodes",
                columns: new[] { "owner_id", "id" });

            migrationBuilder.CreateTable(
                name: "context_snapshots",
                schema: "work_reflection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_json = table.Column<string>(type: "jsonb", nullable: false),
                    source_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fingerprint_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data_scope = table.Column<string>(type: "text", nullable: false),
                    disclosure_json = table.Column<string>(type: "jsonb", nullable: false),
                    disclosure_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_snapshots", x => x.id);
                    table.UniqueConstraint("AK_context_snapshots_owner_id_episode_id_id", x => new { x.owner_id, x.episode_id, x.id });
                    table.CheckConstraint("ck_context_number", "number > 0");
                    table.CheckConstraint("ck_context_operation", "operation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_context_scope", "data_scope IN ('Restricted','PublicSynthetic')");
                    table.ForeignKey(
                        name: "FK_context_snapshots_work_episodes_owner_id_episode_id",
                        columns: x => new { x.owner_id, x.episode_id },
                        principalSchema: "work_reflection",
                        principalTable: "work_episodes",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interpretation_revisions",
                schema: "work_reflection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    text_json = table.Column<string>(type: "jsonb", nullable: false),
                    provenance_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interpretation_revisions", x => x.id);
                    table.UniqueConstraint("AK_interpretation_revisions_owner_id_episode_id_snapshot_id_id", x => new { x.owner_id, x.episode_id, x.snapshot_id, x.id });
                    table.CheckConstraint("ck_interpretation_number", "number > 0");
                    table.CheckConstraint("ck_interpretation_operation", "operation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_interpretation_origin", "origin IN ('User','Fixture','Provider') AND (origin = 'User' OR provenance_json IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_interpretation_revisions_context_snapshots_owner_id_episode~",
                        columns: x => new { x.owner_id, x.episode_id, x.snapshot_id },
                        principalSchema: "work_reflection",
                        principalTable: "context_snapshots",
                        principalColumns: new[] { "owner_id", "episode_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_interpretation_revisions_interpretation_revisions_owner_id_~",
                        columns: x => new { x.owner_id, x.episode_id, x.snapshot_id, x.supersedes_id },
                        principalSchema: "work_reflection",
                        principalTable: "interpretation_revisions",
                        principalColumns: new[] { "owner_id", "episode_id", "snapshot_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_confirmations",
                schema: "work_reflection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    episode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interpretation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_confirmations", x => x.id);
                    table.CheckConstraint("ck_confirmation_operation", "operation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_context_confirmations_interpretation_revisions_owner_id_epi~",
                        columns: x => new { x.owner_id, x.episode_id, x.snapshot_id, x.interpretation_id },
                        principalSchema: "work_reflection",
                        principalTable: "interpretation_revisions",
                        principalColumns: new[] { "owner_id", "episode_id", "snapshot_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_episodes_owner_id_id_current_snapshot_id_accepted_inte~",
                schema: "work_reflection",
                table: "work_episodes",
                columns: new[] { "owner_id", "id", "current_snapshot_id", "accepted_interpretation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_context_confirmations_owner_id_episode_id_operation_id",
                schema: "work_reflection",
                table: "context_confirmations",
                columns: new[] { "owner_id", "episode_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_context_confirmations_owner_id_episode_id_snapshot_id_inter~",
                schema: "work_reflection",
                table: "context_confirmations",
                columns: new[] { "owner_id", "episode_id", "snapshot_id", "interpretation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_context_snapshots_owner_id_episode_id_number",
                schema: "work_reflection",
                table: "context_snapshots",
                columns: new[] { "owner_id", "episode_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_context_snapshots_owner_id_episode_id_operation_id",
                schema: "work_reflection",
                table: "context_snapshots",
                columns: new[] { "owner_id", "episode_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interpretation_revisions_owner_id_episode_id_operation_id",
                schema: "work_reflection",
                table: "interpretation_revisions",
                columns: new[] { "owner_id", "episode_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interpretation_revisions_owner_id_episode_id_snapshot_id_nu~",
                schema: "work_reflection",
                table: "interpretation_revisions",
                columns: new[] { "owner_id", "episode_id", "snapshot_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interpretation_revisions_owner_id_episode_id_snapshot_id_su~",
                schema: "work_reflection",
                table: "interpretation_revisions",
                columns: new[] { "owner_id", "episode_id", "snapshot_id", "supersedes_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_work_episodes_context_snapshots_owner_id_id_current_snapsho~",
                schema: "work_reflection",
                table: "work_episodes",
                columns: new[] { "owner_id", "id", "current_snapshot_id" },
                principalSchema: "work_reflection",
                principalTable: "context_snapshots",
                principalColumns: new[] { "owner_id", "episode_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_episodes_interpretation_revisions_owner_id_id_current_~",
                schema: "work_reflection",
                table: "work_episodes",
                columns: new[] { "owner_id", "id", "current_snapshot_id", "accepted_interpretation_id" },
                principalSchema: "work_reflection",
                principalTable: "interpretation_revisions",
                principalColumns: new[] { "owner_id", "episode_id", "snapshot_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION work_reflection.reject_context_revision_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Context source, interpretation and confirmation records are immutable; append a new version.';
                END;
                $$;
                CREATE TRIGGER context_snapshots_immutable BEFORE UPDATE ON work_reflection.context_snapshots
                    FOR EACH ROW EXECUTE FUNCTION work_reflection.reject_context_revision_update();
                CREATE TRIGGER interpretation_revisions_immutable BEFORE UPDATE ON work_reflection.interpretation_revisions
                    FOR EACH ROW EXECUTE FUNCTION work_reflection.reject_context_revision_update();
                CREATE TRIGGER context_confirmations_immutable BEFORE UPDATE ON work_reflection.context_confirmations
                    FOR EACH ROW EXECUTE FUNCTION work_reflection.reject_context_revision_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER context_snapshots_immutable ON work_reflection.context_snapshots;
                DROP TRIGGER interpretation_revisions_immutable ON work_reflection.interpretation_revisions;
                DROP TRIGGER context_confirmations_immutable ON work_reflection.context_confirmations;
                DROP FUNCTION work_reflection.reject_context_revision_update();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_work_episodes_context_snapshots_owner_id_id_current_snapsho~",
                schema: "work_reflection",
                table: "work_episodes");

            migrationBuilder.DropForeignKey(
                name: "FK_work_episodes_interpretation_revisions_owner_id_id_current_~",
                schema: "work_reflection",
                table: "work_episodes");

            migrationBuilder.DropTable(
                name: "context_confirmations",
                schema: "work_reflection");

            migrationBuilder.DropTable(
                name: "interpretation_revisions",
                schema: "work_reflection");

            migrationBuilder.DropTable(
                name: "context_snapshots",
                schema: "work_reflection");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_work_episodes_owner_id_id",
                schema: "work_reflection",
                table: "work_episodes");

            migrationBuilder.DropIndex(
                name: "IX_work_episodes_owner_id_id_current_snapshot_id_accepted_inte~",
                schema: "work_reflection",
                table: "work_episodes");

            migrationBuilder.DropColumn(
                name: "accepted_interpretation_id",
                schema: "work_reflection",
                table: "work_episodes");

            migrationBuilder.DropColumn(
                name: "context_number",
                schema: "work_reflection",
                table: "work_episodes");

            migrationBuilder.DropColumn(
                name: "current_snapshot_id",
                schema: "work_reflection",
                table: "work_episodes");
        }
    }
}
