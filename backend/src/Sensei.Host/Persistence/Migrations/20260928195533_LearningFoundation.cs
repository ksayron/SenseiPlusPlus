using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sensei.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "concept_relations",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_relations", x => x.id);
                    table.ForeignKey(
                        name: "FK_concept_relations_concepts_source_id",
                        column: x => x.source_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_concept_relations_concepts_target_id",
                        column: x => x.target_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_families",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_families", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_states",
                schema: "evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimate = table.Column<double>(type: "double precision", nullable: true),
                    policy_version = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_states_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_versions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    blocks_json = table.Column<string>(type: "jsonb", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_material_versions_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operation_receipts",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    digest = table.Column<string>(type: "text", nullable: false),
                    outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_operation_receipts_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_versions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "self_review_allowances",
                schema: "evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_admitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_self_review_allowances", x => x.id);
                    table.ForeignKey(
                        name: "FK_self_review_allowances_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_self_review_allowances_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "status_revisions",
                schema: "evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_status_revisions_observations_observation_id",
                        column: x => x.observation_id,
                        principalSchema: "evidence",
                        principalTable: "observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_versions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    primary_concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    prompt_json = table.Column<string>(type: "jsonb", nullable: false),
                    interaction_json = table.Column<string>(type: "jsonb", nullable: false),
                    evaluation_json = table.Column<string>(type: "jsonb", nullable: false),
                    hints_json = table.Column<string>(type: "jsonb", nullable: false),
                    evaluator_version = table.Column<string>(type: "text", nullable: false),
                    estimated_seconds = table.Column<int>(type: "integer", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercise_versions_concepts_primary_concept_id",
                        column: x => x.primary_concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_versions_exercise_families_family_id",
                        column: x => x.family_id,
                        principalSchema: "learning",
                        principalTable: "exercise_families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_versions_material_versions_material_version_id",
                        column: x => x.material_version_id,
                        principalSchema: "learning",
                        principalTable: "material_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goals",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intention = table.Column<string>(type: "text", nullable: false),
                    roadmap_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.id);
                    table.ForeignKey(
                        name: "FK_goals_roadmap_versions_roadmap_version_id",
                        column: x => x.roadmap_version_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goals_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_stages",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    roadmap_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    minimum_families = table.Column<int>(type: "integer", nullable: false),
                    minimum_sessions = table.Column<int>(type: "integer", nullable: false),
                    minimum_correctness = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_stages", x => x.id);
                    table.ForeignKey(
                        name: "FK_roadmap_stages_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_roadmap_stages_roadmap_versions_roadmap_version_id",
                        column: x => x.roadmap_version_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_concepts",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_concepts", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercise_concepts_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_concepts_exercise_versions_exercise_version_id",
                        column: x => x.exercise_version_id,
                        principalSchema: "learning",
                        principalTable: "exercise_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goal_concepts",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_concepts", x => x.id);
                    table.ForeignKey(
                        name: "FK_goal_concepts_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goal_concepts_goals_goal_id",
                        column: x => x.goal_id,
                        principalSchema: "learning",
                        principalTable: "goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_enrollments",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    roadmap_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paused = table.Column<bool>(type: "boolean", nullable: false),
                    current_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_enrollments", x => x.id);
                    table.UniqueConstraint("AK_roadmap_enrollments_owner_id_id", x => new { x.owner_id, x.id });
                    table.ForeignKey(
                        name: "FK_roadmap_enrollments_roadmap_stages_current_stage_id",
                        column: x => x.current_stage_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_roadmap_enrollments_roadmap_versions_roadmap_version_id",
                        column: x => x.roadmap_version_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_roadmap_enrollments_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: true),
                    roadmap_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_count = table.Column<int>(type: "integer", nullable: false),
                    actual_count = table.Column<int>(type: "integer", nullable: false),
                    feedback_policy = table.Column<string>(type: "text", nullable: false),
                    selection_policy = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    return_context = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.UniqueConstraint("AK_sessions_owner_id_id", x => new { x.owner_id, x.id });
                    table.ForeignKey(
                        name: "FK_sessions_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessions_roadmap_enrollments_owner_id_enrollment_id",
                        columns: x => new { x.owner_id, x.enrollment_id },
                        principalSchema: "learning",
                        principalTable: "roadmap_enrollments",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessions_roadmap_stages_roadmap_stage_id",
                        column: x => x.roadmap_stage_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessions_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stage_traversals",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_traversals", x => x.id);
                    table.ForeignKey(
                        name: "FK_stage_traversals_roadmap_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stage_traversals_roadmap_stages_stage_id",
                        column: x => x.stage_id,
                        principalSchema: "learning",
                        principalTable: "roadmap_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_items",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    draft_json = table.Column<string>(type: "jsonb", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: true),
                    hint_used = table.Column<bool>(type: "boolean", nullable: false),
                    reference_used = table.Column<bool>(type: "boolean", nullable: false),
                    revealed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_items", x => x.id);
                    table.UniqueConstraint("AK_session_items_owner_id_session_id_id_exercise_version_id", x => new { x.owner_id, x.session_id, x.id, x.exercise_version_id });
                    table.ForeignKey(
                        name: "FK_session_items_exercise_versions_exercise_version_id",
                        column: x => x.exercise_version_id,
                        principalSchema: "learning",
                        principalTable: "exercise_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_items_sessions_owner_id_session_id",
                        columns: x => new { x.owner_id, x.session_id },
                        principalSchema: "learning",
                        principalTable: "sessions",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_items_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    assisted = table.Column<bool>(type: "boolean", nullable: false),
                    answer_aware = table.Column<bool>(type: "boolean", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempts", x => x.id);
                    table.UniqueConstraint("AK_attempts_owner_id_id", x => new { x.owner_id, x.id });
                    table.ForeignKey(
                        name: "FK_attempts_attempts_owner_id_previous_attempt_id",
                        columns: x => new { x.owner_id, x.previous_attempt_id },
                        principalSchema: "learning",
                        principalTable: "attempts",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempts_session_items_owner_id_session_id_item_id_exercise~",
                        columns: x => new { x.owner_id, x.session_id, x.item_id, x.exercise_version_id },
                        principalSchema: "learning",
                        principalTable: "session_items",
                        principalColumns: new[] { "owner_id", "session_id", "id", "exercise_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempts_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exercise_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_events_attempts_owner_id_attempt_id",
                        columns: x => new { x.owner_id, x.attempt_id },
                        principalSchema: "learning",
                        principalTable: "attempts",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_events_exercise_versions_exercise_version_id",
                        column: x => x.exercise_version_id,
                        principalSchema: "learning",
                        principalTable: "exercise_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_events_material_versions_material_version_id",
                        column: x => x.material_version_id,
                        principalSchema: "learning",
                        principalTable: "material_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_events_sessions_owner_id_session_id",
                        columns: x => new { x.owner_id, x.session_id },
                        principalSchema: "learning",
                        principalTable: "sessions",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_events_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "results",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_version = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    self_review = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_results", x => x.id);
                    table.UniqueConstraint("AK_results_owner_id_id", x => new { x.owner_id, x.id });
                    table.ForeignKey(
                        name: "FK_results_attempts_owner_id_attempt_id",
                        columns: x => new { x.owner_id, x.attempt_id },
                        principalSchema: "learning",
                        principalTable: "attempts",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_results_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_contributions",
                schema: "evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    assisted = table.Column<bool>(type: "boolean", nullable: false),
                    answer_aware = table.Column<bool>(type: "boolean", nullable: false),
                    self_review = table.Column<string>(type: "text", nullable: true),
                    admitted = table.Column<bool>(type: "boolean", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    policy_version = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_contributions", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_attempts_owner_id_attempt_id",
                        columns: x => new { x.owner_id, x.attempt_id },
                        principalSchema: "learning",
                        principalTable: "attempts",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_concepts_concept_id",
                        column: x => x.concept_id,
                        principalSchema: "learning",
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_exercise_families_family_id",
                        column: x => x.family_id,
                        principalSchema: "learning",
                        principalTable: "exercise_families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_observations_observation_id",
                        column: x => x.observation_id,
                        principalSchema: "evidence",
                        principalTable: "observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_results_owner_id_result_id",
                        columns: x => new { x.owner_id, x.result_id },
                        principalSchema: "learning",
                        principalTable: "results",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_sessions_owner_id_session_id",
                        columns: x => new { x.owner_id, x.session_id },
                        principalSchema: "learning",
                        principalTable: "sessions",
                        principalColumns: new[] { "owner_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attempts_owner_id_previous_attempt_id",
                schema: "learning",
                table: "attempts",
                columns: new[] { "owner_id", "previous_attempt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attempts_owner_id_session_id_item_id_exercise_version_id",
                schema: "learning",
                table: "attempts",
                columns: new[] { "owner_id", "session_id", "item_id", "exercise_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_concept_relations_source_id_target_id_kind",
                schema: "learning",
                table: "concept_relations",
                columns: new[] { "source_id", "target_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_concept_relations_target_id",
                schema: "learning",
                table: "concept_relations",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "IX_events_exercise_version_id",
                schema: "learning",
                table: "events",
                column: "exercise_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_events_material_version_id",
                schema: "learning",
                table: "events",
                column: "material_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_events_owner_id_attempt_id",
                schema: "learning",
                table: "events",
                columns: new[] { "owner_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_events_owner_id_received_at_id",
                schema: "learning",
                table: "events",
                columns: new[] { "owner_id", "received_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_events_owner_id_session_id",
                schema: "learning",
                table: "events",
                columns: new[] { "owner_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_concepts_concept_id",
                schema: "learning",
                table: "exercise_concepts",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_concepts_exercise_version_id_concept_id",
                schema: "learning",
                table: "exercise_concepts",
                columns: new[] { "exercise_version_id", "concept_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_families_key",
                schema: "learning",
                table: "exercise_families",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_versions_exercise_id_revision",
                schema: "learning",
                table: "exercise_versions",
                columns: new[] { "exercise_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_versions_family_id",
                schema: "learning",
                table: "exercise_versions",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_versions_material_version_id",
                schema: "learning",
                table: "exercise_versions",
                column: "material_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_versions_primary_concept_id",
                schema: "learning",
                table: "exercise_versions",
                column: "primary_concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_goal_concepts_concept_id",
                schema: "learning",
                table: "goal_concepts",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_goal_concepts_goal_id_concept_id",
                schema: "learning",
                table: "goal_concepts",
                columns: new[] { "goal_id", "concept_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goals_owner_id",
                schema: "learning",
                table: "goals",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_goals_roadmap_version_id",
                schema: "learning",
                table: "goals",
                column: "roadmap_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_concept_id",
                schema: "evidence",
                table: "knowledge_contributions",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_family_id",
                schema: "evidence",
                table: "knowledge_contributions",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_observation_id",
                schema: "evidence",
                table: "knowledge_contributions",
                column: "observation_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_owner_id_attempt_id",
                schema: "evidence",
                table: "knowledge_contributions",
                columns: new[] { "owner_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_owner_id_result_id",
                schema: "evidence",
                table: "knowledge_contributions",
                columns: new[] { "owner_id", "result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_owner_id_session_id",
                schema: "evidence",
                table: "knowledge_contributions",
                columns: new[] { "owner_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_result_id_concept_id",
                schema: "evidence",
                table: "knowledge_contributions",
                columns: new[] { "result_id", "concept_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_states_owner_id_concept_id",
                schema: "evidence",
                table: "knowledge_states",
                columns: new[] { "owner_id", "concept_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_versions_concept_id",
                schema: "learning",
                table: "material_versions",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_operation_receipts_owner_id_operation_id",
                schema: "learning",
                table: "operation_receipts",
                columns: new[] { "owner_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_results_attempt_id",
                schema: "learning",
                table: "results",
                column: "attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_results_owner_id_attempt_id",
                schema: "learning",
                table: "results",
                columns: new[] { "owner_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_enrollments_current_stage_id",
                schema: "learning",
                table: "roadmap_enrollments",
                column: "current_stage_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_enrollments_owner_id_roadmap_version_id",
                schema: "learning",
                table: "roadmap_enrollments",
                columns: new[] { "owner_id", "roadmap_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_enrollments_roadmap_version_id",
                schema: "learning",
                table: "roadmap_enrollments",
                column: "roadmap_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_stages_concept_id",
                schema: "learning",
                table: "roadmap_stages",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_stages_roadmap_version_id_position",
                schema: "learning",
                table: "roadmap_stages",
                columns: new[] { "roadmap_version_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_self_review_allowances_concept_id",
                schema: "evidence",
                table: "self_review_allowances",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_self_review_allowances_owner_id_concept_id",
                schema: "evidence",
                table: "self_review_allowances",
                columns: new[] { "owner_id", "concept_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_items_exercise_version_id",
                schema: "learning",
                table: "session_items",
                column: "exercise_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_items_session_id_position",
                schema: "learning",
                table: "session_items",
                columns: new[] { "session_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_concept_id",
                schema: "learning",
                table: "sessions",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_owner_id",
                schema: "learning",
                table: "sessions",
                column: "owner_id",
                unique: true,
                filter: "state IN ('Active', 'Paused')");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_owner_id_enrollment_id",
                schema: "learning",
                table: "sessions",
                columns: new[] { "owner_id", "enrollment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_roadmap_stage_id",
                schema: "learning",
                table: "sessions",
                column: "roadmap_stage_id");

            migrationBuilder.CreateIndex(
                name: "IX_stage_traversals_enrollment_id_stage_id",
                schema: "learning",
                table: "stage_traversals",
                columns: new[] { "enrollment_id", "stage_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stage_traversals_stage_id",
                schema: "learning",
                table: "stage_traversals",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "IX_status_revisions_observation_id",
                schema: "evidence",
                table: "status_revisions",
                column: "observation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "concept_relations",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "events",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "exercise_concepts",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "goal_concepts",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "knowledge_contributions",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "knowledge_states",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "operation_receipts",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "self_review_allowances",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "stage_traversals",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "status_revisions",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "goals",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "results",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "attempts",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "session_items",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "exercise_versions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "exercise_families",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "material_versions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "roadmap_enrollments",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "roadmap_stages",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "roadmap_versions",
                schema: "learning");
        }
    }
}
