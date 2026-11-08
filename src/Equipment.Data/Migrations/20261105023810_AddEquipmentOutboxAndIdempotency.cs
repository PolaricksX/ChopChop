using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Equipment.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentOutboxAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EquipmentIdempotencyRecords",
                schema: "equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentIdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentOutbox",
                schema: "equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_EquipmentIdempotency_Owner_Key",
                schema: "equipment",
                table: "EquipmentIdempotencyRecords",
                columns: new[] { "OwnerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentOutbox_Dispatch",
                schema: "equipment",
                table: "EquipmentOutbox",
                columns: new[] { "PublishedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentIdempotencyRecords",
                schema: "equipment");

            migrationBuilder.DropTable(
                name: "EquipmentOutbox",
                schema: "equipment");
        }
    }
}
