using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialRecipeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recipe_management");

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "recipe_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "recipe_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedEvents",
                schema: "recipe_management",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "recipe_management",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDeletionOutcomes",
                schema: "recipe_management",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftsDeleted = table.Column<int>(type: "int", nullable: false),
                    RecipesAnonymized = table.Column<int>(type: "int", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDeletionOutcomes", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                schema: "recipe_management",
                columns: table => new
                {
                    Version = table.Column<int>(type: "int", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    Servings = table.Column<int>(type: "int", nullable: false),
                    Allergens = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllergenStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DietaryLabels = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrepMinutes = table.Column<int>(type: "int", nullable: false),
                    CookMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalMinutes = table.Column<int>(type: "int", nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => new { x.RecipeId, x.Version });
                    table.ForeignKey(
                        name: "FK_RecipeVersions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "recipe_management",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentRequirements",
                schema: "recipe_management",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    MatchingMetadata = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRequirements", x => new { x.RecipeId, x.Version, x.Position });
                    table.ForeignKey(
                        name: "FK_EquipmentRequirements_RecipeVersions_RecipeId_Version",
                        columns: x => new { x.RecipeId, x.Version },
                        principalSchema: "recipe_management",
                        principalTable: "RecipeVersions",
                        principalColumns: new[] { "RecipeId", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                schema: "recipe_management",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => new { x.RecipeId, x.Version, x.Position });
                    table.ForeignKey(
                        name: "FK_Ingredients_RecipeVersions_RecipeId_Version",
                        columns: x => new { x.RecipeId, x.Version },
                        principalSchema: "recipe_management",
                        principalTable: "RecipeVersions",
                        principalColumns: new[] { "RecipeId", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstructionSteps",
                schema: "recipe_management",
                columns: table => new
                {
                    Order = table.Column<int>(type: "int", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructionSteps", x => new { x.RecipeId, x.Version, x.Order });
                    table.ForeignKey(
                        name: "FK_InstructionSteps_RecipeVersions_RecipeId_Version",
                        columns: x => new { x.RecipeId, x.Version },
                        principalSchema: "recipe_management",
                        principalTable: "RecipeVersions",
                        principalColumns: new[] { "RecipeId", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NutritionEstimates",
                schema: "recipe_management",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Calories = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProteinGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CarbohydratesGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FatGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FiberGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SodiumMilligrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionEstimates", x => new { x.RecipeId, x.Version });
                    table.ForeignKey(
                        name: "FK_NutritionEstimates_RecipeVersions_RecipeId_Version",
                        columns: x => new { x.RecipeId, x.Version },
                        principalSchema: "recipe_management",
                        principalTable: "RecipeVersions",
                        principalColumns: new[] { "RecipeId", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_OwnerId_Key",
                schema: "recipe_management",
                table: "IdempotencyRecords",
                columns: new[] { "OwnerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAtUtc_DeadLetteredAtUtc_NextAttemptAtUtc",
                schema: "recipe_management",
                table: "OutboxMessages",
                columns: new[] { "PublishedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OwnerId_Status",
                schema: "recipe_management",
                table: "Recipes",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_RecipeId_Status",
                schema: "recipe_management",
                table: "RecipeVersions",
                columns: new[] { "RecipeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeletionOutcomes_UserId",
                schema: "recipe_management",
                table: "UserDeletionOutcomes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentRequirements",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "Ingredients",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "InstructionSteps",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "NutritionEstimates",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "ProcessedEvents",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "UserDeletionOutcomes",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "RecipeVersions",
                schema: "recipe_management");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "recipe_management");
        }
    }
}
