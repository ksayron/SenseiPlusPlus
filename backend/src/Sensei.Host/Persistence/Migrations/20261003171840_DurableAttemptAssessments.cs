using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sensei.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableAttemptAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_attempts_owner_id_id_exercise_version_id",
                schema: "learning",
                table: "attempts",
                columns: new[] { "owner_id", "id", "exercise_version_id" });

            migrationBuilder.CreateTable(
                name: "assessment_jobs",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_digest = table.Column<string>(type: "text", nullable: false),
                    input_json = table.Column<string>(type: "jsonb", nullable: false),
                    input_digest = table.Column<string>(type: "text", nullable: false),
                    request_digest = table.Column<string>(type: "text", nullable: false),
                    route_json = table.Column<string>(type: "jsonb", nullable: false),
                    route_digest = table.Column<string>(type: "text", nullable: false),
                    disclosure_digest = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    template_version = table.Column<string>(type: "text", nullable: false),
                    contract_version = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    fence = table.Column<long>(type: "bigint", nullable: false),
                    invocation_count = table.Column<int>(type: "integer", nullable: false),
                    transient_retries = table.Column<int>(type: "integer", nullable: false),
                    repairs = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    not_before = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_jobs", x => x.id);
                    table.UniqueConstraint("AK_assessment_jobs_owner_id_id", x => new { x.owner_id, x.id });
                    table.UniqueConstraint("AK_assessment_jobs_owner_id_id_attempt_id_exercise_version_id", x => new { x.owner_id, x.id, x.attempt_id, x.exercise_version_id });
                    table.CheckConstraint("ck_assessment_bounds", "invocation_count BETWEEN 0 AND 3 AND transient_retries BETWEEN 0 AND 1 AND repairs BETWEEN 0 AND 1 AND version >= 1 AND generation >= 0 AND fence >= 0");
                    table.CheckConstraint("ck_assessment_origin", "origin IN ('Provider','Fixture')");
                    table.CheckConstraint("ck_assessment_state", "state IN ('Queued','Running','Retry','Succeeded','Failed','Cancelled','Obsolete') AND ((state = 'Running') = (lease_until IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_assessment_jobs_attempts_owner_id_attempt_id_exercise_versi~",
                        columns: x => new { x.owner_id, x.attempt_id, x.exercise_version_id },
                        principalSchema: "learning",
                        principalTable: "attempts",
                        principalColumns: new[] { "owner_id", "id", "exercise_version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_invocations",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    fence = table.Column<long>(type: "bigint", nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    request_digest = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "text", nullable: true),
                    audit_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_invocations", x => x.id);
                    table.UniqueConstraint("AK_assessment_invocations_owner_id_job_id_id", x => new { x.owner_id, x.job_id, x.id });
                    table.ForeignKey(
                        name: "FK_assessment_invocations_assessment_jobs_owner_id_job_id",
                        columns: x => new { x.owner_id, x.job_id },
                        principalSchema: "learning",
                        principalTable: "assessment_jobs",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_feedback_revisions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    input_digest = table.Column<string>(type: "text", nullable: false),
                    route_digest = table.Column<string>(type: "text", nullable: false),
                    template_version = table.Column<string>(type: "text", nullable: false),
                    contract_version = table.Column<string>(type: "text", nullable: false),
                    output_json = table.Column<string>(type: "jsonb", nullable: false),
                    audit_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_feedback_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessment_feedback_revisions_assessment_invocations_owner_~",
                        columns: x => new { x.owner_id, x.job_id, x.invocation_id },
                        principalSchema: "learning",
                        principalTable: "assessment_invocations",
                        principalColumns: new[] { "owner_id", "job_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assessment_feedback_revisions_assessment_jobs_owner_id_job_~",
                        columns: x => new { x.owner_id, x.job_id, x.attempt_id, x.exercise_version_id },
                        principalSchema: "learning",
                        principalTable: "assessment_jobs",
                        principalColumns: new[] { "owner_id", "id", "attempt_id", "exercise_version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_feedback_revisions_job_id",
                schema: "learning",
                table: "assessment_feedback_revisions",
                column: "job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_feedback_revisions_owner_id_attempt_id_revision",
                schema: "learning",
                table: "assessment_feedback_revisions",
                columns: new[] { "owner_id", "attempt_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_feedback_revisions_owner_id_job_id_attempt_id_ex~",
                schema: "learning",
                table: "assessment_feedback_revisions",
                columns: new[] { "owner_id", "job_id", "attempt_id", "exercise_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_feedback_revisions_owner_id_job_id_invocation_id",
                schema: "learning",
                table: "assessment_feedback_revisions",
                columns: new[] { "owner_id", "job_id", "invocation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_invocations_job_id_number",
                schema: "learning",
                table: "assessment_invocations",
                columns: new[] { "job_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_invocations_started_at",
                schema: "learning",
                table: "assessment_invocations",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_assessment_jobs_owner_id_attempt_id",
                schema: "learning",
                table: "assessment_jobs",
                columns: new[] { "owner_id", "attempt_id" },
                unique: true,
                filter: "state IN ('Queued', 'Running', 'Retry')");

            migrationBuilder.CreateIndex(
                name: "IX_assessment_jobs_owner_id_attempt_id_exercise_version_id",
                schema: "learning",
                table: "assessment_jobs",
                columns: new[] { "owner_id", "attempt_id", "exercise_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_jobs_owner_id_operation_id",
                schema: "learning",
                table: "assessment_jobs",
                columns: new[] { "owner_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_jobs_state_not_before_created_at",
                schema: "learning",
                table: "assessment_jobs",
                columns: new[] { "state", "not_before", "created_at" });
            migrationBuilder.Sql("""
                CREATE FUNCTION learning.guard_assessment_feedback() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Assessment feedback revisions are immutable' USING ERRCODE = '23514'; END $$;
                CREATE TRIGGER assessment_feedback_immutable BEFORE UPDATE ON learning.assessment_feedback_revisions
                FOR EACH ROW EXECUTE FUNCTION learning.guard_assessment_feedback();
                CREATE FUNCTION learning.guard_assessment_input() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF ROW(NEW.owner_id,NEW.attempt_id,NEW.exercise_version_id,NEW.operation_id,NEW.command_digest,
                        NEW.input_json,NEW.input_digest,NEW.request_digest,NEW.route_json,NEW.route_digest,
                        NEW.disclosure_digest,NEW.origin,NEW.template_version,NEW.contract_version,NEW.created_at)
                        IS DISTINCT FROM ROW(OLD.owner_id,OLD.attempt_id,OLD.exercise_version_id,OLD.operation_id,OLD.command_digest,
                        OLD.input_json,OLD.input_digest,OLD.request_digest,OLD.route_json,OLD.route_digest,
                        OLD.disclosure_digest,OLD.origin,OLD.template_version,OLD.contract_version,OLD.created_at)
                    THEN RAISE EXCEPTION 'Assessment job input is immutable' USING ERRCODE = '23514'; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER assessment_input_immutable BEFORE UPDATE ON learning.assessment_jobs
                FOR EACH ROW EXECUTE FUNCTION learning.guard_assessment_input();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER assessment_feedback_immutable ON learning.assessment_feedback_revisions; DROP FUNCTION learning.guard_assessment_feedback(); DROP TRIGGER assessment_input_immutable ON learning.assessment_jobs; DROP FUNCTION learning.guard_assessment_input();");
            migrationBuilder.DropTable(
                name: "assessment_feedback_revisions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "assessment_invocations",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "assessment_jobs",
                schema: "learning");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_attempts_owner_id_id_exercise_version_id",
                schema: "learning",
                table: "attempts");
        }
    }
}
