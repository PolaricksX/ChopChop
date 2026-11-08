using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExactPantryOwnerKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PantryItems_ActiveOwnerIngredientType",
                table: "PantryItems");

            migrationBuilder.DropColumn(
                name: "OwnerIdNormalized",
                table: "PantryItems");

            migrationBuilder.AddColumn<string>(
                name: "OwnerKey",
                table: "PantryItems",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "EXEC(N'UPDATE [PantryItems] SET [OwnerKey] = CONVERT(varchar(64), HASHBYTES(''SHA2_256'', CONVERT(varbinary(max), [OwnerId])), 2);');");

            migrationBuilder.CreateIndex(
                name: "UX_PantryItems_ActiveOwnerIngredientType",
                table: "PantryItems",
                columns: new[] { "OwnerKey", "NameNormalized", "Type" },
                unique: true,
                filter: "[Availability] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back exact owner isolation requires a reviewed data migration.");
        }
    }
}
