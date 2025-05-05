using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsToLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DetectionId",
                table: "Logs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_DetectionId",
                table: "Logs",
                column: "DetectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Logs_Detections_DetectionId",
                table: "Logs",
                column: "DetectionId",
                principalTable: "Detections",
                principalColumn: "DetectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Logs_Detections_DetectionId",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_Logs_DetectionId",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "DetectionId",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Logs");
        }
    }
}
