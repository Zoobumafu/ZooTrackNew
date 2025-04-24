using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooTrack.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Devices",
                keyColumn: "DeviceId",
                keyValue: 1,
                column: "LastActive",
                value: new DateTime(2025, 4, 24, 14, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "Devices",
                keyColumn: "DeviceId",
                keyValue: 2,
                column: "LastActive",
                value: new DateTime(2025, 4, 24, 14, 30, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Devices",
                keyColumn: "DeviceId",
                keyValue: 1,
                column: "LastActive",
                value: new DateTime(2025, 4, 24, 19, 50, 23, 586, DateTimeKind.Local).AddTicks(2169));

            migrationBuilder.UpdateData(
                table: "Devices",
                keyColumn: "DeviceId",
                keyValue: 2,
                column: "LastActive",
                value: new DateTime(2025, 4, 24, 19, 20, 23, 587, DateTimeKind.Local).AddTicks(6355));
        }
    }
}
