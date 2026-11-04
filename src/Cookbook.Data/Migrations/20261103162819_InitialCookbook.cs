using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cookbook.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCookbook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cookbook");

            migrationBuilder.CreateTable(
                name: "Folders",
                schema: "cookbook",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedRecipes",
                schema: "cookbook",
                columns: table => new
                {
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeVersion = table.Column<int>(type: "int", nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedRecipes", x => new { x.FolderId, x.RecipeId, x.RecipeVersion });
                    table.ForeignKey(
                        name: "FK_SavedRecipes_Folders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "cookbook",
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_OwnerId_Name",
                schema: "cookbook",
                table: "Folders",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedRecipes_RecipeId_RecipeVersion",
                schema: "cookbook",
                table: "SavedRecipes",
                columns: new[] { "RecipeId", "RecipeVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedRecipes",
                schema: "cookbook");

            migrationBuilder.DropTable(
                name: "Folders",
                schema: "cookbook");
        }
    }
}
