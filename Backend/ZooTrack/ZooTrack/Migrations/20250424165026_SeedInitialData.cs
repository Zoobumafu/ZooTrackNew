using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZooTrack.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Devices",
                columns: new[] { "DeviceId", "LastActive", "Location", "Status" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 4, 24, 19, 50, 23, 586, DateTimeKind.Local).AddTicks(2169), "North Zone", "Online" },
                    { 2, new DateTime(2025, 4, 24, 19, 20, 23, 587, DateTimeKind.Local).AddTicks(6355), "South Zone", "Offline" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Email", "Name", "Role" },
                values: new object[,]
                {
                    { 1, "admin@zootrack.local", "Admin", "Admin" },
                    { 2, "ranger@zootrack.local", "Ranger Rick", "Ranger" }
                });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "UserId", "DetectionThreshold", "NotificationPreference" },
                values: new object[,]
                {
                    { 1, 0.8f, "Email" },
                    { 2, 0.7f, "SMS" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Devices",
                keyColumn: "DeviceId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Devices",
                keyColumn: "DeviceId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "UserSettings",
                keyColumn: "UserId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UserSettings",
                keyColumn: "UserId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2);
        }
    }
}
