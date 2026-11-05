using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Data.Migrations;

[Migration("20261104120000_PantryDataInitial")]
public partial class PantryDataInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PantryItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                CustomCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                Unit = table.Column<int>(type: "int", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Availability = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                OwnerIdNormalized = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NameNormalized = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PantryItems", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_PublishedAt_OccurredAt",
            table: "OutboxMessages",
            columns: new[] { "PublishedAt", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "UX_PantryItems_ActiveOwnerIngredientType",
            table: "PantryItems",
            columns: new[] { "OwnerIdNormalized", "NameNormalized", "Type" },
            unique: true,
            filter: "[Availability] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessages");
        migrationBuilder.DropTable(name: "PantryItems");
    }
}
