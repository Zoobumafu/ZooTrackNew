using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooTrackBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDetectionValidationDbSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DetectionValidations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DetectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsValidated = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTruePositive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidationNotes = table.Column<string>(type: "TEXT", nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    IsFalsePositive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFalseNegative = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetectionValidations_Detections_DetectionId",
                        column: x => x.DetectionId,
                        principalTable: "Detections",
                        principalColumn: "DetectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetectionValidations_DetectionId",
                table: "DetectionValidations",
                column: "DetectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetectionValidations");
        }
    }
}
