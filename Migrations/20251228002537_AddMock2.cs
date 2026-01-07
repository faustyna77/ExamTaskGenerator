using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ExamCreateApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMock2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MockExams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    exam_type = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    task_count = table.Column<int>(type: "integer", nullable: false),
                    time_limit_minutes = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_elapsed_seconds = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tasks_data = table.Column<string>(type: "text", nullable: false),
                    user_answers = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    max_score = table.Column<int>(type: "integer", nullable: true),
                    percentage = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockExams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockExams_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MockExamAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mock_exam_id = table.Column<int>(type: "integer", nullable: false),
                    task_index = table.Column<int>(type: "integer", nullable: false),
                    task_content = table.Column<string>(type: "text", nullable: false),
                    user_answer = table.Column<string>(type: "text", nullable: true),
                    correct_answer = table.Column<string>(type: "text", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    points_earned = table.Column<double>(type: "double precision", nullable: false),
                    max_points = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockExamAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockExamAnswers_MockExams_mock_exam_id",
                        column: x => x.mock_exam_id,
                        principalTable: "MockExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MockExamAnswers_mock_exam_id",
                table: "MockExamAnswers",
                column: "mock_exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_MockExams_user_id",
                table: "MockExams",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MockExamAnswers");

            migrationBuilder.DropTable(
                name: "MockExams");
        }
    }
}
