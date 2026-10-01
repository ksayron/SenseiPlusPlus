using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sensei.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningProvenanceHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_contributions_observations_observation_id",
                schema: "evidence",
                table: "knowledge_contributions");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_contributions_observation_id",
                schema: "evidence",
                table: "knowledge_contributions");

            migrationBuilder.AddColumn<string>(
                name: "difficulty",
                schema: "learning",
                table: "exercise_versions",
                type: "text",
                nullable: false,
                defaultValue: "Intermediate");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "client_recorded_at",
                schema: "learning",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                schema: "learning",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "device_sequence",
                schema: "learning",
                table: "events",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_observations_owner_id_concept_id_id",
                schema: "evidence",
                table: "observations",
                columns: new[] { "owner_id", "concept_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_owner_id_concept_id_observation_id",
                schema: "evidence",
                table: "knowledge_contributions",
                columns: new[] { "owner_id", "concept_id", "observation_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_contributions_observations_owner_id_concept_id_ob~",
                schema: "evidence",
                table: "knowledge_contributions",
                columns: new[] { "owner_id", "concept_id", "observation_id" },
                principalSchema: "evidence",
                principalTable: "observations",
                principalColumns: new[] { "owner_id", "concept_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_contributions_observations_owner_id_concept_id_ob~",
                schema: "evidence",
                table: "knowledge_contributions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_observations_owner_id_concept_id_id",
                schema: "evidence",
                table: "observations");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_contributions_owner_id_concept_id_observation_id",
                schema: "evidence",
                table: "knowledge_contributions");

            migrationBuilder.DropColumn(
                name: "difficulty",
                schema: "learning",
                table: "exercise_versions");

            migrationBuilder.DropColumn(
                name: "client_recorded_at",
                schema: "learning",
                table: "events");

            migrationBuilder.DropColumn(
                name: "device_id",
                schema: "learning",
                table: "events");

            migrationBuilder.DropColumn(
                name: "device_sequence",
                schema: "learning",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_observation_id",
                schema: "evidence",
                table: "knowledge_contributions",
                column: "observation_id");

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_contributions_observations_observation_id",
                schema: "evidence",
                table: "knowledge_contributions",
                column: "observation_id",
                principalSchema: "evidence",
                principalTable: "observations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
