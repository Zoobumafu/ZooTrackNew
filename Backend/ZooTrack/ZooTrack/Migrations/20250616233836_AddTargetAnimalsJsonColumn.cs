using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZooTrackBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetAnimalsJsonColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastActive = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.MediaId);
                    table.ForeignKey(
                        name: "FK_Media_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionThreshold = table.Column<float>(type: "REAL", nullable: false),
                    NotificationPreference = table.Column<string>(type: "TEXT", nullable: true),
                    TargetAnimalsJson = table.Column<string>(type: "TEXT", nullable: false),
                    HighlightSavePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Detections",
                columns: table => new
                {
                    DetectionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Confidence = table.Column<float>(type: "REAL", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackingId = table.Column<int>(type: "INTEGER", nullable: true),
                    BoundingBoxX = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxY = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxWidth = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxHeight = table.Column<float>(type: "REAL", nullable: false),
                    FrameNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectedObject = table.Column<string>(type: "TEXT", nullable: true),
                    MediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Detections", x => x.DetectionId);
                    table.ForeignKey(
                        name: "FK_Detections_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Detections_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Detections_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "MediaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DetectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.AlertId);
                    table.ForeignKey(
                        name: "FK_Alerts_Detections_DetectionId",
                        column: x => x.DetectionId,
                        principalTable: "Detections",
                        principalColumn: "DetectionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Alerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Animals",
                columns: table => new
                {
                    AnimalId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Species = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidenceLevel = table.Column<float>(type: "REAL", nullable: false),
                    DetectionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Animals", x => x.AnimalId);
                    table.ForeignKey(
                        name: "FK_Animals_Detections_DetectionId",
                        column: x => x.DetectionId,
                        principalTable: "Detections",
                        principalColumn: "DetectionId",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    LogId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DetectionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_Logs_Detections_DetectionId",
                        column: x => x.DetectionId,
                        principalTable: "Detections",
                        principalColumn: "DetectionId");
                    table.ForeignKey(
                        name: "FK_Logs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Devices",
                columns: new[] { "DeviceId", "LastActive", "Location", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), "North Zone - Watering Hole", "Online" },
                    { 2, new DateTime(2025, 1, 1, 11, 30, 0, 0, DateTimeKind.Unspecified), "South Zone - Savanna", "Online" },
                    { 3, new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Unspecified), "East Zone - Forest Edge", "Offline" },
                    { 4, new DateTime(2025, 1, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), "West Zone - River Crossing", "Maintenance" }
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "EventId", "EndTime", "StartTime", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 12, 45, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 2, new DateTime(2025, 1, 2, 16, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 3, new DateTime(2025, 1, 3, 10, 20, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 4, new DateTime(2025, 1, 4, 9, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 4, 8, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 5, new DateTime(2025, 1, 5, 15, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 1, 5, 15, 0, 0, 0, DateTimeKind.Unspecified), "Active" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Email", "Name", "Role" },
                values: new object[,]
                {
                    { 1, "admin@zootrack.local", "Admin", "Admin" },
                    { 2, "ranger@zootrack.local", "Ranger Rick", "Ranger" },
                    { 3, "sarah@zootrack.local", "Zoologist Sarah", "Researcher" },
                    { 4, "tom@zootrack.local", "Guide Tom", "Guide" }
                });

            migrationBuilder.InsertData(
                table: "Logs",
                columns: new[] { "LogId", "ActionType", "DetectionId", "Level", "Message", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 1, "System Start", null, "Info", "System initialization complete", new DateTime(2024, 12, 22, 9, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 2, "User Login", null, "Info", "Admin logged in", new DateTime(2024, 12, 25, 8, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 4, "Settings Change", null, "Warning", "Detection threshold updated to 0.85", new DateTime(2025, 1, 1, 13, 0, 0, 0, DateTimeKind.Unspecified), 3 },
                    { 5, "Device Status", null, "Error", "Device East Zone went offline", new DateTime(2025, 1, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 7, "Alert Configuration", null, "Info", "Added new alert for endangered species", new DateTime(2025, 1, 5, 10, 0, 0, 0, DateTimeKind.Unspecified), 2 },
                    { 8, "System Maintenance", null, "Info", "Database backup completed", new DateTime(2025, 1, 5, 11, 0, 0, 0, DateTimeKind.Unspecified), 1 }
                });

            migrationBuilder.InsertData(
                table: "Media",
                columns: new[] { "MediaId", "DeviceId", "FilePath", "Timestamp", "Type" },
                values: new object[,]
                {
                    { 1, 1, "/storage/images/elephant_group_20250423.jpg", new DateTime(2025, 1, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 2, 2, "/storage/videos/lion_pride_20250424.mp4", new DateTime(2025, 1, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), "Video" },
                    { 3, 1, "/storage/images/rhino_watering_20250426.jpg", new DateTime(2025, 1, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 4, 2, "/storage/images/giraffe_family_20250427.jpg", new DateTime(2025, 1, 4, 8, 0, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 5, 4, "/storage/videos/zebra_crossing_20250428.mp4", new DateTime(2025, 1, 5, 15, 0, 0, 0, DateTimeKind.Unspecified), "Video" }
                });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "UserId", "DetectionThreshold", "HighlightSavePath", "NotificationPreference", "TargetAnimalsJson" },
                values: new object[,]
                {
                    { 1, 0.8f, "Media/HighlightFrames/Admin", "Email", "[\"person\",\"dog\",\"cow\",\"wolf\",\"tiger\",\"lion\",\"elephant\",\"zebra\",\"giraffe\",\"rhino\",\"leopard\",\"cheetah\"]" },
                    { 2, 0.7f, "Media/HighlightFrames/Ranger", "SMS", "[\"lion\",\"elephant\",\"zebra\",\"giraffe\",\"rhino\",\"leopard\",\"buffalo\"]" },
                    { 3, 0.85f, "Media/HighlightFrames/Researcher", "Both", "[\"lion\",\"elephant\",\"zebra\",\"giraffe\",\"rhino\",\"leopard\",\"cheetah\",\"buffalo\",\"antelope\",\"warthog\"]" },
                    { 4, 0.6f, "Media/HighlightFrames/Guide", "None", "[\"lion\",\"elephant\",\"zebra\",\"giraffe\",\"rhino\"]" }
                });

            migrationBuilder.InsertData(
                table: "Detections",
                columns: new[] { "DetectionId", "BoundingBoxHeight", "BoundingBoxWidth", "BoundingBoxX", "BoundingBoxY", "Confidence", "DetectedAt", "DetectedObject", "DeviceId", "EventId", "FrameNumber", "MediaId", "TrackingId" },
                values: new object[,]
                {
                    { 1, 0f, 0f, 0f, 0f, 0.92f, new DateTime(2025, 1, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), null, 1, 1, 0, 1, null },
                    { 2, 0f, 0f, 0f, 0f, 0.85f, new DateTime(2025, 1, 2, 14, 30, 0, 0, DateTimeKind.Unspecified), null, 2, 2, 0, 2, null },
                    { 3, 0f, 0f, 0f, 0f, 0.79f, new DateTime(2025, 1, 3, 10, 5, 0, 0, DateTimeKind.Unspecified), null, 1, 3, 0, 3, null },
                    { 4, 0f, 0f, 0f, 0f, 0.88f, new DateTime(2025, 1, 4, 8, 30, 0, 0, DateTimeKind.Unspecified), null, 2, 4, 0, 4, null },
                    { 5, 0f, 0f, 0f, 0f, 0.75f, new DateTime(2025, 1, 5, 15, 15, 0, 0, DateTimeKind.Unspecified), null, 4, 5, 0, 5, null }
                });

            migrationBuilder.InsertData(
                table: "Alerts",
                columns: new[] { "AlertId", "CreatedAt", "DetectionId", "Message", "UserId" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 12, 10, 0, 0, DateTimeKind.Unspecified), 1, "Elephant herd detected at North Zone watering hole", 2 },
                    { 2, new DateTime(2025, 1, 2, 14, 45, 0, 0, DateTimeKind.Unspecified), 2, "Lion pride spotted in South Zone", 1 },
                    { 3, new DateTime(2025, 1, 3, 10, 15, 0, 0, DateTimeKind.Unspecified), 3, "Rhino sighting at North Zone watering hole", 3 },
                    { 4, new DateTime(2025, 1, 4, 8, 45, 0, 0, DateTimeKind.Unspecified), 4, "Giraffe family detected in South Zone", 2 },
                    { 5, new DateTime(2025, 1, 5, 15, 25, 0, 0, DateTimeKind.Unspecified), 5, "Zebra crossing detected at West Zone river", 3 }
                });

            migrationBuilder.InsertData(
                table: "Animals",
                columns: new[] { "AnimalId", "ConfidenceLevel", "DetectionId", "Species" },
                values: new object[,]
                {
                    { 1, 0.93f, 1, "African Elephant" },
                    { 2, 0.89f, 1, "African Elephant" },
                    { 3, 0.87f, 2, "Lion" },
                    { 4, 0.82f, 2, "Lion" },
                    { 5, 0.79f, 2, "Lion" },
                    { 6, 0.91f, 3, "White Rhino" },
                    { 7, 0.94f, 4, "Giraffe" },
                    { 8, 0.92f, 4, "Giraffe" },
                    { 9, 0.88f, 5, "Zebra" },
                    { 10, 0.86f, 5, "Zebra" },
                    { 11, 0.84f, 5, "Zebra" },
                    { 12, 0.79f, 5, "Zebra" }
                });

            migrationBuilder.InsertData(
                table: "Logs",
                columns: new[] { "LogId", "ActionType", "DetectionId", "Level", "Message", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 3, "Detection Review", 1, "Info", "Ranger Rick reviewed elephant detection", new DateTime(2025, 1, 1, 12, 15, 0, 0, DateTimeKind.Unspecified), 2 },
                    { 6, "Media View", 3, "Info", "Guide Tom accessed rhino sighting media", new DateTime(2025, 1, 3, 11, 0, 0, 0, DateTimeKind.Unspecified), 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_DetectionId",
                table: "Alerts",
                column: "DetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId",
                table: "Alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Animals_DetectionId",
                table: "Animals",
                column: "DetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Detections_DeviceId",
                table: "Detections",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Detections_EventId",
                table: "Detections",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Detections_MediaId",
                table: "Detections",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_DetectionValidations_DetectionId",
                table: "DetectionValidations",
                column: "DetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_DetectionId",
                table: "Logs",
                column: "DetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_UserId",
                table: "Logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_DeviceId",
                table: "Media",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "Animals");

            migrationBuilder.DropTable(
                name: "DetectionValidations");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "Detections");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
