using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRecipeGeneration.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerationCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "ai_recipe",
                table: "GenerationRequests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "ai_recipe",
                table: "GenerationRequests");
        }
    }
}
