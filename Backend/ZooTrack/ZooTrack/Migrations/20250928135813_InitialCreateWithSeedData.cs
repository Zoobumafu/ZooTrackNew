using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZooTrackBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithSeedData : Migration
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
                    { 1, new DateTime(2025, 6, 20, 14, 30, 0, 0, DateTimeKind.Unspecified), "North Zone", "Online" },
                    { 2, new DateTime(2025, 6, 20, 14, 25, 0, 0, DateTimeKind.Unspecified), "South Zone", "Online" },
                    { 3, new DateTime(2025, 6, 19, 22, 45, 0, 0, DateTimeKind.Unspecified), "East Zone", "Offline" },
                    { 4, new DateTime(2025, 6, 20, 14, 35, 0, 0, DateTimeKind.Unspecified), "West Zone", "Online" },
                    { 5, new DateTime(2025, 6, 20, 8, 15, 0, 0, DateTimeKind.Unspecified), "Central Zone", "Maintenance" }
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "EventId", "EndTime", "StartTime", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 6, 19, 10, 25, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 19, 10, 15, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 2, new DateTime(2025, 6, 19, 14, 45, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 19, 14, 30, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 3, new DateTime(2025, 6, 20, 8, 35, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 20, 8, 20, 0, 0, DateTimeKind.Unspecified), "Completed" },
                    { 4, new DateTime(2025, 6, 20, 12, 20, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 20, 12, 10, 0, 0, DateTimeKind.Unspecified), "Active" },
                    { 5, new DateTime(2025, 6, 20, 15, 15, 0, 0, DateTimeKind.Unspecified), new DateTime(2025, 6, 20, 15, 0, 0, 0, DateTimeKind.Unspecified), "Active" }
                });

            migrationBuilder.InsertData(
                table: "TrackingRoutes",
                columns: new[] { "Id", "DetectedObject", "DeviceId", "EndTime", "PathJson", "StartTime", "TrackingId" },
                values: new object[,]
                {
                    { 1, "tiger", 1, new DateTime(2025, 6, 19, 10, 19, 15, 0, DateTimeKind.Unspecified), "[[120.5, 80.2], [122.0, 82.0], [125.0, 85.0]]", new DateTime(2025, 6, 19, 10, 18, 30, 0, DateTimeKind.Unspecified), 1001 },
                    { 2, "elephant", 2, new DateTime(2025, 6, 19, 14, 35, 0, 0, DateTimeKind.Unspecified), "[[200.0, 150.0], [205.0, 155.0], [210.0, 160.0]]", new DateTime(2025, 6, 19, 14, 32, 0, 0, DateTimeKind.Unspecified), 2001 },
                    { 3, "lion", 4, new DateTime(2025, 6, 20, 8, 25, 0, 0, DateTimeKind.Unspecified), "[[300.0, 200.0], [295.0, 205.0], [290.0, 210.0]]", new DateTime(2025, 6, 20, 8, 22, 45, 0, DateTimeKind.Unspecified), 4001 },
                    { 4, "wolf", 4, new DateTime(2025, 6, 20, 8, 27, 30, 0, DateTimeKind.Unspecified), "[[180.0, 120.0], [175.0, 125.0], [170.0, 130.0]]", new DateTime(2025, 6, 20, 8, 25, 0, 0, DateTimeKind.Unspecified), 4002 },
                    { 5, "bear", 1, new DateTime(2025, 6, 20, 15, 5, 0, 0, DateTimeKind.Unspecified), "[[220.0, 180.0], [225.0, 185.0], [230.0, 190.0]]", new DateTime(2025, 6, 20, 15, 2, 15, 0, DateTimeKind.Unspecified), 1002 },
                    { 6, "tiger", 1, new DateTime(2025, 6, 18, 14, 25, 0, 0, DateTimeKind.Unspecified), "[[0.2, 0.3], [0.22, 0.32], [0.25, 0.35], [0.28, 0.38]]", new DateTime(2025, 6, 18, 14, 20, 0, 0, DateTimeKind.Unspecified), 1003 },
                    { 7, "lion", 1, new DateTime(2025, 6, 17, 10, 35, 0, 0, DateTimeKind.Unspecified), "[[0.4, 0.5], [0.42, 0.48], [0.45, 0.46], [0.48, 0.44]]", new DateTime(2025, 6, 17, 10, 30, 0, 0, DateTimeKind.Unspecified), 1005 },
                    { 8, "elephant", 2, new DateTime(2025, 6, 17, 15, 30, 0, 0, DateTimeKind.Unspecified), "[[0.3, 0.4], [0.32, 0.42], [0.34, 0.44], [0.36, 0.46], [0.38, 0.48]]", new DateTime(2025, 6, 17, 15, 20, 0, 0, DateTimeKind.Unspecified), 2004 },
                    { 9, "bear", 3, new DateTime(2025, 6, 15, 9, 40, 0, 0, DateTimeKind.Unspecified), "[[0.5, 0.3], [0.52, 0.32], [0.54, 0.34], [0.56, 0.36]]", new DateTime(2025, 6, 15, 9, 30, 0, 0, DateTimeKind.Unspecified), 3001 },
                    { 10, "deer", 5, new DateTime(2025, 6, 19, 16, 15, 0, 0, DateTimeKind.Unspecified), "[[0.45, 0.35], [0.47, 0.37], [0.49, 0.39], [0.51, 0.41]]", new DateTime(2025, 6, 19, 16, 10, 0, 0, DateTimeKind.Unspecified), 5002 },
                    { 11, "tiger", 5, new DateTime(2025, 6, 20, 11, 12, 0, 0, DateTimeKind.Unspecified), "[[0.6, 0.4], [0.58, 0.42], [0.56, 0.44], [0.54, 0.46], [0.52, 0.48]]", new DateTime(2025, 6, 20, 11, 5, 0, 0, DateTimeKind.Unspecified), 5003 }
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
                    { 1, "Login", null, "Info", "Admin logged into system", new DateTime(2025, 6, 19, 9, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 3, "Login", null, "Info", "Zoo Manager logged into system", new DateTime(2025, 6, 19, 14, 0, 0, 0, DateTimeKind.Unspecified), 2 },
                    { 5, "System_Maintenance", null, "Warning", "Device maintenance scheduled for Central Zone", new DateTime(2025, 6, 20, 8, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 6, "Login", null, "Info", "Wildlife Observer logged into system", new DateTime(2025, 6, 20, 12, 30, 0, 0, DateTimeKind.Unspecified), 3 }
                });

            migrationBuilder.InsertData(
                table: "Media",
                columns: new[] { "MediaId", "DeviceId", "FilePath", "Timestamp", "Type" },
                values: new object[,]
                {
                    { 1, 1, "Media/Videos/north_zone_20250619_101500.jpg", new DateTime(2025, 6, 19, 10, 15, 0, 0, DateTimeKind.Unspecified), "Video" },
                    { 2, 1, "Media/Images/north_zone_20250619_101800.jpg", new DateTime(2025, 6, 19, 10, 18, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 3, 2, "Media/Videos/south_zone_20250619_143000.jpg", new DateTime(2025, 6, 19, 14, 30, 0, 0, DateTimeKind.Unspecified), "Video" },
                    { 4, 2, "Media/Images/south_zone_20250619_143500.jpg", new DateTime(2025, 6, 19, 14, 35, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 5, 4, "Media/Videos/west_zone_20250620_082000.jpg", new DateTime(2025, 6, 20, 8, 20, 0, 0, DateTimeKind.Unspecified), "Video" },
                    { 6, 5, "Media/Images/central_zone_20250620_121000.jpg", new DateTime(2025, 6, 20, 12, 10, 0, 0, DateTimeKind.Unspecified), "Image" },
                    { 7, 1, "Media/Videos/north_zone_20250620_150000.jpg", new DateTime(2025, 6, 20, 15, 0, 0, 0, DateTimeKind.Unspecified), "Video" }
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
                    { 1, 200f, 150f, 120.5f, 80.2f, 0.92f, new DateTime(2025, 6, 19, 10, 18, 30, 0, DateTimeKind.Unspecified), "tiger", 1, 1, 450, false, 1, 1001 },
                    { 2, 195f, 148f, 125f, 85f, 0.88f, new DateTime(2025, 6, 19, 10, 19, 15, 0, DateTimeKind.Unspecified), "tiger", 1, 1, 495, false, 1, 1001 },
                    { 3, 250f, 180f, 200f, 150f, 0.95f, new DateTime(2025, 6, 19, 14, 32, 0, 0, DateTimeKind.Unspecified), "elephant", 2, 2, 720, false, 3, 2001 },
                    { 4, 160f, 120f, 50f, 100f, 0.87f, new DateTime(2025, 6, 19, 14, 35, 30, 0, DateTimeKind.Unspecified), "giraffe", 2, 2, 0, false, 4, 2002 },
                    { 5, 130f, 100f, 300f, 200f, 0.91f, new DateTime(2025, 6, 20, 8, 22, 45, 0, DateTimeKind.Unspecified), "lion", 4, 3, 165, false, 5, 4001 },
                    { 6, 110f, 90f, 180f, 120f, 0.89f, new DateTime(2025, 6, 20, 8, 25, 0, 0, DateTimeKind.Unspecified), "wolf", 4, 3, 300, false, 5, 4002 },
                    { 7, 100f, 80f, 150f, 90f, 0.83f, new DateTime(2025, 6, 20, 12, 12, 30, 0, DateTimeKind.Unspecified), "deer", 5, 4, 0, false, 6, 5001 },
                    { 8, 180f, 140f, 220f, 180f, 0.94f, new DateTime(2025, 6, 20, 15, 2, 15, 0, DateTimeKind.Unspecified), "bear", 1, 5, 135, false, 7, 1002 },
                    { 9, 0.25f, 0.15f, 0.2f, 0.3f, 0.76f, new DateTime(2025, 6, 18, 14, 20, 0, 0, DateTimeKind.Unspecified), "tiger", 1, 1, 200, false, 1, 1003 },
                    { 10, 0.18f, 0.12f, 0.6f, 0.1f, 0.45f, new DateTime(2025, 6, 18, 16, 45, 0, 0, DateTimeKind.Unspecified), "deer", 1, 1, 300, false, 1, 1004 },
                    { 11, 0.3f, 0.2f, 0.4f, 0.5f, 0.92f, new DateTime(2025, 6, 17, 10, 30, 0, 0, DateTimeKind.Unspecified), "lion", 1, 1, 150, false, 1, 1005 },
                    { 12, 0.12f, 0.08f, 0.1f, 0.2f, 0.38f, new DateTime(2025, 6, 18, 11, 15, 0, 0, DateTimeKind.Unspecified), "bird", 2, 2, 100, false, 3, 2003 },
                    { 13, 0.35f, 0.25f, 0.3f, 0.4f, 0.89f, new DateTime(2025, 6, 17, 15, 20, 0, 0, DateTimeKind.Unspecified), "elephant", 2, 2, 250, false, 3, 2004 },
                    { 14, 0.15f, 0.1f, 0.7f, 0.6f, 0.55f, new DateTime(2025, 6, 16, 13, 45, 0, 0, DateTimeKind.Unspecified), "wolf", 2, 2, 180, false, 3, 2005 },
                    { 15, 0.28f, 0.18f, 0.5f, 0.3f, 0.91f, new DateTime(2025, 6, 15, 9, 30, 0, 0, DateTimeKind.Unspecified), "bear", 3, 1, 120, false, 1, 3001 },
                    { 16, 0.1f, 0.06f, 0.2f, 0.7f, 0.42f, new DateTime(2025, 6, 15, 14, 20, 0, 0, DateTimeKind.Unspecified), "rabbit", 3, 1, 90, false, 1, 3002 },
                    { 17, 0.2f, 0.15f, 0.8f, 0.2f, 0.67f, new DateTime(2025, 6, 19, 8, 45, 0, 0, DateTimeKind.Unspecified), "fox", 4, 3, 75, false, 5, 4003 },
                    { 18, 0.08f, 0.05f, 0.1f, 0.8f, 0.29f, new DateTime(2025, 6, 18, 17, 30, 0, 0, DateTimeKind.Unspecified), "bird", 4, 3, 45, false, 5, 4004 },
                    { 19, 0.3f, 0.2f, 0.45f, 0.35f, 0.84f, new DateTime(2025, 6, 19, 16, 10, 0, 0, DateTimeKind.Unspecified), "deer", 5, 4, 220, false, 6, 5002 },
                    { 20, 0.32f, 0.22f, 0.6f, 0.4f, 0.95f, new DateTime(2025, 6, 20, 11, 5, 0, 0, DateTimeKind.Unspecified), "tiger", 5, 4, 280, false, 6, 5003 }
                });

            migrationBuilder.InsertData(
                table: "Alerts",
                columns: new[] { "AlertId", "CreatedAt", "DetectionId", "Message", "UserId" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 6, 19, 10, 18, 35, 0, DateTimeKind.Unspecified), 1, "High confidence tiger detection in North Zone", 1 },
                    { 2, new DateTime(2025, 6, 19, 14, 32, 5, 0, DateTimeKind.Unspecified), 3, "Large animal detected in South Zone - requires attention", 2 },
                    { 3, new DateTime(2025, 6, 20, 8, 22, 50, 0, DateTimeKind.Unspecified), 5, "Predator activity detected in West Zone", 1 },
                    { 4, new DateTime(2025, 6, 20, 8, 25, 5, 0, DateTimeKind.Unspecified), 6, "Wolf pack movement detected in West Zone", 2 },
                    { 5, new DateTime(2025, 6, 20, 15, 2, 20, 0, DateTimeKind.Unspecified), 8, "Bear sighting in North Zone - immediate attention required", 1 }
                });

            migrationBuilder.InsertData(
                table: "Animals",
                columns: new[] { "AnimalId", "ConfidenceLevel", "DetectionId", "Species" },
                values: new object[,]
                {
                    { 1, 0.92f, 1, "Siberian Tiger" },
                    { 2, 0.88f, 2, "Siberian Tiger" },
                    { 3, 0.95f, 3, "African Elephant" },
                    { 4, 0.87f, 4, "Reticulated Giraffe" },
                    { 5, 0.91f, 5, "African Lion" },
                    { 6, 0.89f, 6, "Gray Wolf" },
                    { 7, 0.83f, 7, "White-tailed Deer" },
                    { 8, 0.94f, 8, "Brown Bear" }
                });

            migrationBuilder.InsertData(
                table: "DetectionValidations",
                columns: new[] { "Id", "DetectionId", "IsFalseNegative", "IsFalsePositive", "IsTruePositive", "IsValidated", "ValidatedAt", "ValidatedBy", "ValidationNotes" },
                values: new object[,]
                {
                    { 1, 1, false, false, true, true, new DateTime(2025, 6, 19, 11, 0, 0, 0, DateTimeKind.Unspecified), "Admin", "Confirmed tiger identification" },
                    { 2, 2, false, false, true, true, new DateTime(2025, 6, 19, 11, 5, 0, 0, DateTimeKind.Unspecified), "Admin", "Same tiger - tracking confirmed" },
                    { 3, 3, false, false, true, true, new DateTime(2025, 6, 19, 15, 0, 0, 0, DateTimeKind.Unspecified), "Zoo Manager", "Adult elephant confirmed" },
                    { 4, 4, false, false, false, false, new DateTime(2025, 6, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "", "" },
                    { 5, 7, false, true, false, true, new DateTime(2025, 6, 20, 13, 0, 0, 0, DateTimeKind.Unspecified), "Wildlife Observer", "False positive - was a large log" },
                    { 6, 9, false, false, true, true, new DateTime(2025, 6, 18, 15, 0, 0, 0, DateTimeKind.Unspecified), "Admin", "Tiger confirmed in North Zone" },
                    { 7, 11, false, false, true, true, new DateTime(2025, 6, 17, 11, 0, 0, 0, DateTimeKind.Unspecified), "Zoo Manager", "Lion sighting verified" },
                    { 8, 13, false, false, true, true, new DateTime(2025, 6, 17, 16, 0, 0, 0, DateTimeKind.Unspecified), "Wildlife Observer", "Elephant behavior normal" },
                    { 9, 15, false, false, true, true, new DateTime(2025, 6, 15, 10, 0, 0, 0, DateTimeKind.Unspecified), "Admin", "Bear spotted in East Zone" },
                    { 10, 19, false, false, true, true, new DateTime(2025, 6, 19, 17, 0, 0, 0, DateTimeKind.Unspecified), "Wildlife Observer", "Deer grazing - normal behavior" },
                    { 11, 20, false, false, true, true, new DateTime(2025, 6, 20, 12, 0, 0, 0, DateTimeKind.Unspecified), "Zoo Manager", "High confidence tiger detection" },
                    { 12, 10, false, true, false, true, new DateTime(2025, 6, 18, 17, 0, 0, 0, DateTimeKind.Unspecified), "Admin", "Shadow mistaken for deer" },
                    { 13, 12, false, true, false, true, new DateTime(2025, 6, 18, 12, 0, 0, 0, DateTimeKind.Unspecified), "Wildlife Observer", "Leaf movement, not a bird" },
                    { 14, 14, false, true, false, true, new DateTime(2025, 6, 16, 14, 30, 0, 0, DateTimeKind.Unspecified), "Zoo Manager", "Rock formation misidentified as wolf" },
                    { 15, 16, false, true, false, true, new DateTime(2025, 6, 15, 15, 0, 0, 0, DateTimeKind.Unspecified), "Admin", "Small debris, not rabbit" },
                    { 16, 17, false, true, false, true, new DateTime(2025, 6, 19, 9, 0, 0, 0, DateTimeKind.Unspecified), "Wildlife Observer", "Bush movement mistaken for fox" },
                    { 17, 18, false, true, false, true, new DateTime(2025, 6, 18, 18, 0, 0, 0, DateTimeKind.Unspecified), "Zoo Manager", "Flying plastic bag, not bird" },
                    { 18, 6, false, false, false, false, new DateTime(2025, 6, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "", "" },
                    { 19, 8, false, false, false, false, new DateTime(2025, 6, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "", "" }
                });

            migrationBuilder.InsertData(
                table: "Logs",
                columns: new[] { "LogId", "ActionType", "DetectionId", "Level", "Message", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 2, "Detection_Review", 1, "Info", "Validated tiger detection", new DateTime(2025, 6, 19, 11, 0, 0, 0, DateTimeKind.Unspecified), 1 },
                    { 4, "Alert_Response", 3, "Info", "Responded to elephant detection alert", new DateTime(2025, 6, 19, 14, 33, 0, 0, DateTimeKind.Unspecified), 2 },
                    { 7, "Detection_Review", 7, "Info", "Marked detection as false positive", new DateTime(2025, 6, 20, 13, 0, 0, 0, DateTimeKind.Unspecified), 3 },
                    { 8, "Emergency_Alert", 8, "Critical", "Emergency alert triggered for bear sighting", new DateTime(2025, 6, 20, 15, 2, 20, 0, DateTimeKind.Unspecified), 1 }
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
