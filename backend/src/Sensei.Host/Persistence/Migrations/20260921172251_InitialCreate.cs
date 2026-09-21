using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sensei.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "learning");

            migrationBuilder.EnsureSchema(
                name: "experience");

            migrationBuilder.EnsureSchema(
                name: "evidence");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "work_reflection");

            migrationBuilder.CreateTable(
                name: "concepts",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    locale = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concepts", x => x.id);
                    table.CheckConstraint("ck_concepts_difficulty", "difficulty IN ('Beginner', 'Intermediate', 'Advanced')");
                    table.CheckConstraint("ck_concepts_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_concepts_version_positive", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ui_locale = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    answer_language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_users_version_positive", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "entries",
                schema: "experience",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => x.id);
                    table.CheckConstraint("ck_entries_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_entries_owner_not_empty", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_entries_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "fk_entries_owner",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "observations",
                schema: "evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aspect = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    signal_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assistance = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    conditions = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_observations", x => x.id);
                    table.CheckConstraint("ck_observations_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_observations_owner_not_empty", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_observations_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "fk_observations_concept",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_observations_owner",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_episodes",
                schema: "work_reflection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    setting = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    role = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_episodes", x => x.id);
                    table.CheckConstraint("ck_work_episodes_id_not_empty", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_work_episodes_owner_not_empty", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_work_episodes_setting", "setting IN ('Employment', 'Coursework', 'PersonalProject', 'Other')");
                    table.CheckConstraint("ck_work_episodes_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "fk_work_episodes_owner",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entry_revisions",
                schema: "experience",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    setting = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    context = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    actions = table.Column<string>(type: "text", nullable: false),
                    alternatives = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    impact_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approval_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_revisions", x => new { x.entry_id, x.number });
                    table.CheckConstraint("ck_entry_revisions_approval", "approval_state IN ('Draft', 'Approved', 'Retracted')");
                    table.CheckConstraint("ck_entry_revisions_impact", "impact_state IN ('Unknown', 'Qualitative', 'Measured')");
                    table.CheckConstraint("ck_entry_revisions_number_positive", "number >= 1");
                    table.CheckConstraint("ck_entry_revisions_setting", "setting IN ('Employment', 'Coursework', 'PersonalProject', 'Other')");
                    table.ForeignKey(
                        name: "FK_entry_revisions_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "experience",
                        principalTable: "entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_revision_concepts",
                schema: "experience",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_revision_concepts", x => new { x.entry_id, x.revision_number, x.concept_id });
                    table.CheckConstraint("ck_entry_revision_concepts_id_not_empty", "concept_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_entry_revision_concepts_entry_revisions_entry_id_revision_n~",
                        columns: x => new { x.entry_id, x.revision_number },
                        principalSchema: "experience",
                        principalTable: "entry_revisions",
                        principalColumns: new[] { "entry_id", "number" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_entry_revision_concepts_concept",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_concepts_key_id",
                schema: "learning",
                table: "concepts",
                columns: new[] { "key", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_concepts_key",
                schema: "learning",
                table: "concepts",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entries_owner_created_id",
                schema: "experience",
                table: "entries",
                columns: new[] { "owner_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_entries_owner_id",
                schema: "experience",
                table: "entries",
                columns: new[] { "owner_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entry_revision_concepts_concept",
                schema: "experience",
                table: "entry_revision_concepts",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_observations_concept_id",
                schema: "evidence",
                table: "observations",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_observations_owner_concept",
                schema: "evidence",
                table: "observations",
                columns: new[] { "owner_id", "concept_id" });

            migrationBuilder.CreateIndex(
                name: "ix_observations_owner_observed_id",
                schema: "evidence",
                table: "observations",
                columns: new[] { "owner_id", "observed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_observations_owner_id",
                schema: "evidence",
                table: "observations",
                columns: new[] { "owner_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_created_at_id",
                schema: "identity",
                table: "users",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_episodes_owner_event_id",
                schema: "work_reflection",
                table: "work_episodes",
                columns: new[] { "owner_id", "event_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_work_episodes_owner_id",
                schema: "work_reflection",
                table: "work_episodes",
                columns: new[] { "owner_id", "id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_revision_concepts",
                schema: "experience");

            migrationBuilder.DropTable(
                name: "observations",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "work_episodes",
                schema: "work_reflection");

            migrationBuilder.DropTable(
                name: "entry_revisions",
                schema: "experience");

            migrationBuilder.DropTable(
                name: "concepts",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "entries",
                schema: "experience");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
