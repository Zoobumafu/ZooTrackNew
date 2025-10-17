using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZooTrackBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                name: "TrackingRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackingId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectedObject = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PathJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false)
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
                    MediaId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsTarget = table.Column<bool>(type: "INTEGER", nullable: false)
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
                        principalColumn: "EventId");
                    table.ForeignKey(
                        name: "FK_Detections_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "MediaId");
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
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
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
                        principalColumn: "UserId");
                });

            migrationBuilder.InsertData(
                table: "Devices",
                columns: new[] { "DeviceId", "LastActive", "Location", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 17, 14, 55, 0, 0, DateTimeKind.Unspecified), "North Zone", "Online" },
                    { 2, new DateTime(2025, 10, 17, 14, 50, 0, 0, DateTimeKind.Unspecified), "South Zone", "Online" },
                    { 3, new DateTime(2025, 10, 16, 15, 0, 0, 0, DateTimeKind.Unspecified), "East Zone", "Offline" },
                    { 4, new DateTime(2025, 10, 17, 14, 58, 0, 0, DateTimeKind.Unspecified), "West Zone", "Online" },
                    { 5, new DateTime(2025, 10, 17, 11, 0, 0, 0, DateTimeKind.Unspecified), "Central Zone", "Maintenance" }
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "EventId", "EndTime", "StartTime", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 15, 16, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 15, 15, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 2, new DateTime(2025, 10, 16, 16, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 16, 15, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 3, new DateTime(2025, 10, 17, 12, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 17, 11, 0, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 4, new DateTime(2025, 10, 17, 16, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 17, 14, 0, 0, 0, DateTimeKind.Unspecified), "Active" },
                    { 5, new DateTime(2025, 10, 17, 17, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 10, 17, 15, 0, 0, 0, DateTimeKind.Unspecified), "Active" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Email", "Name", "PasswordHash", "PasswordSalt", "Role" },
                values: new object[,]
                {
                    { 1, "Admin", "Admin", "ERITFBUWFxgZGhscHR4fICEiIyQlJicoKSorLC0uLzAx", "AQIDBAUEDF+CAQIDBAUGCQ0ODw==", "Admin" },
                    { 2, "manager@zootrack.com", "Zoo Manager", "UVJTVFVWVxhZWltcXV5fYGFiY2RlZmdoaWprbG1ub3A=", "ITIzNDU2Nzg5Ojs8PT4/QEFCQ0RFRkdISUpLTE1OT1A=", "Manager" },
                    { 3, "observer@zootrack.com", "Wildlife Observer", "kZKTlJWWl5iZmpucnZ6foKGio6SlpqeoqaqrrK2ur7A=", "cXJzdHV2d3h5ent8fX5/gIGCg4SFhoeIiYqLjI2Oj5A=", "User" }
                });

            migrationBuilder.InsertData(
                table: "Logs",
                columns: new[] { "LogId", "ActionType", "DetectionId", "Level", "Message", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 1, "Login", null, "Info", "Admin logged into system", new DateTime(2025, 10, 17, 12, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 3, "Login", null, "Info", "Zoo Manager logged into system", new DateTime(2025, 10, 17, 14, 0, 0, 0, DateTimeKind.Unspecified), 2 }
                });

            migrationBuilder.InsertData(
                table: "Media",
                columns: new[] { "MediaId", "DeviceId", "FilePath", "Timestamp", "Type" },
                values: new object[,]
                {
                    { 1, 1, "Media/Videos/video1.mp4", new DateTime(2025, 10, 15, 15, 0, 0, 0, DateTimeKind.Unspecified), "Video" },
                    { 2, 1, "Media/Images/image1.jpg", new DateTime(2025, 10, 15, 15, 3, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 3, 2, "Media/Videos/video2.mp4", new DateTime(2025, 10, 16, 15, 0, 0, 0, DateTimeKind.Unspecified), "Video" },
                    { 4, 2, "Media/Images/image2.jpg", new DateTime(2025, 10, 16, 15, 5, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 5, 4, "Media/Videos/video3.mp4", new DateTime(2025, 10, 17, 11, 0, 0, 0, DateTimeKind.Unspecified), "Video" }
                });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "UserId", "DetectionThreshold", "HighlightSavePath", "NotificationPreference", "TargetAnimalsJson" },
                values: new object[,]
                {
                    { 1, 0.8f, "Media/HighlightFrames/Admin", "Email", "[\"person\",\"dog\",\"cow\",\"wolf\",\"tiger\",\"lion\",\"elephant\",\"giraffe\"]" },
                    { 2, 0.75f, "Media/HighlightFrames/Manager", "SMS", "[\"tiger\",\"lion\",\"elephant\",\"bear\",\"wolf\"]" },
                    { 3, 0.85f, "Media/HighlightFrames/Observer", "Email", "[\"bird\",\"deer\",\"fox\",\"rabbit\"]" }
                });

            migrationBuilder.InsertData(
                table: "Detections",
                columns: new[] { "DetectionId", "BoundingBoxHeight", "BoundingBoxWidth", "BoundingBoxX", "BoundingBoxY", "Confidence", "DetectedAt", "DetectedObject", "DeviceId", "EventId", "FrameNumber", "IsTarget", "MediaId", "TrackingId" },
                values: new object[,]
                {
                    { 1, 0f, 0f, 0f, 0f, 92f, new DateTime(2025, 10, 15, 15, 3, 0, 0, DateTimeKind.Unspecified), "tiger", 1, 1, 0, false, 1, null },
                    { 2, 0f, 0f, 0f, 0f, 95f, new DateTime(2025, 10, 16, 15, 2, 0, 0, DateTimeKind.Unspecified), "elephant", 2, 2, 0, false, 3, null },
                    { 3, 0f, 0f, 0f, 0f, 87f, new DateTime(2025, 10, 16, 15, 5, 0, 0, DateTimeKind.Unspecified), "giraffe", 2, 2, 0, false, 4, null },
                    { 4, 0f, 0f, 0f, 0f, 91f, new DateTime(2025, 10, 17, 11, 2, 0, 0, DateTimeKind.Unspecified), "lion", 4, 3, 0, false, 5, null },
                    { 5, 0f, 0f, 0f, 0f, 89f, new DateTime(2025, 10, 17, 11, 5, 0, 0, DateTimeKind.Unspecified), "wolf", 4, 3, 0, false, 5, null },
                    { 6, 0f, 0f, 0f, 0f, 94f, new DateTime(2025, 10, 17, 14, 45, 0, 0, DateTimeKind.Unspecified), "bear", 1, 5, 0, false, 1, null }
                });

            migrationBuilder.InsertData(
                table: "Logs",
                columns: new[] { "LogId", "ActionType", "DetectionId", "Level", "Message", "Timestamp", "UserId" },
                values: new object[] { 2, "Detection_Review", 1, "Info", "Validated tiger detection", new DateTime(2025, 10, 17, 13, 0, 0, 0, DateTimeKind.Unspecified), 1 });

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
                name: "TrackingRoutes");

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
