using System.Security.Cryptography;
using System.Text;
using AiRecipeGeneration.Data.Context;
using AiRecipeGeneration.Domain;
using Cookbook.Data.Context;
using Cookbook.Domain.Entities;
using Equipment.Data.Context;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;
using InteractiveReview.Data.Context;
using InteractiveReview.Domain;
using MealPlanner.Data.Context;
using MealPlanner.Domain.Entities;
using MealPlanner.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pantry.Data.Context;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;
using RecipeManagement.Data.Context;
using RecipeManagement.Domain.Entities;
using RecipeManagement.Domain.Enums;
using RecipeNutritionEstimate = RecipeManagement.Domain.Entities.NutritionEstimate;
using UserService.Data.Context;
using UserService.Domain.Entities;

const string databaseName = "ChopChopTest";
var ownerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
var recipeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
var folderId = Guid.Parse("20000000-0000-0000-0000-000000000001");
var equipmentId = Guid.Parse("30000000-0000-0000-0000-000000000001");
var pantryItemId = Guid.Parse("40000000-0000-0000-0000-000000000001");
var reviewId = Guid.Parse("50000000-0000-0000-0000-000000000001");
var generationRequestId = Guid.Parse("60000000-0000-0000-0000-000000000001");
var now = DateTimeOffset.UtcNow;

var password = Environment.GetEnvironmentVariable("TEST_DB_PASSWORD");
if (string.IsNullOrWhiteSpace(password))
{
    throw new InvalidOperationException("TEST_DB_PASSWORD must be set.");
}

var masterConnection = new SqlConnectionStringBuilder
{
    DataSource = "sqlserver,1433",
    InitialCatalog = "master",
    UserID = "sa",
    Password = password,
    Encrypt = true,
    TrustServerCertificate = true,
}.ConnectionString;

await CreateDatabaseIfMissingAsync(masterConnection, databaseName);

var connection = new SqlConnectionStringBuilder(masterConnection)
{
    InitialCatalog = databaseName,
}.ConnectionString;

await using (var db = new AiRecipeDbContext(Options<AiRecipeDbContext>(connection)))
{
    await MigrateAsync(db, "AI Recipe Generation");
    if (!await db.GenerationRequests.AnyAsync(request => request.Id == generationRequestId))
    {
        var request = GenerationRequest.Create(
            generationRequestId,
            ownerId,
            "starter-generation-request",
            "Suggest a quick weeknight dinner using vegetables.",
            includePantry: true,
            ["tomatoes", "spinach"],
            "starter-seed",
            now);
        request.Fail("starter-example", now);
        db.GenerationRequests.Add(request);
        await db.SaveChangesAsync();
    }
}

await using (var db = new InteractiveReviewDbContext(Options<InteractiveReviewDbContext>(connection)))
{
    await MigrateAsync(db, "Interactive Review");
    if (!await db.Reviews.AnyAsync(review => review.Id == reviewId))
    {
        db.Reviews.Add(Review.Create(
            reviewId,
            recipeId,
            ownerId,
            5,
            "A bright, satisfying dinner that is easy to prepare.",
            "Add fresh basil just before serving.",
            now));
        await db.SaveChangesAsync();
    }
}

await using (var db = new RecipeManagementDbContext(Options<RecipeManagementDbContext>(connection)))
{
    await MigrateAsync(db, "Recipe Management");
    if (!await db.Recipes.AnyAsync(recipe => recipe.Id == recipeId))
    {
        var version = new RecipeVersion(
            "Roasted Tomato and Spinach Pasta",
            "A simple pasta with sweet roasted tomatoes, tender spinach, and lemon.",
            4,
            [
                new Ingredient("Pasta", 340, "g"),
                new Ingredient("Cherry tomatoes", 300, "g"),
                new Ingredient("Baby spinach", 120, "g"),
                new Ingredient("Olive oil", 2, "tbsp"),
                new Ingredient("Lemon", 1, "whole"),
            ],
            [
                new InstructionStep(1, "Roast the tomatoes with olive oil until softened."),
                new InstructionStep(2, "Cook the pasta, reserving a little pasta water."),
                new InstructionStep(3, "Fold the spinach into the tomatoes, then toss with pasta and lemon."),
            ],
            allergenStatus: AllergenStatus.Unknown,
            tags: ["quick", "vegetarian", "weeknight"],
            dietaryLabels: ["vegetarian"],
            nutrition: new RecipeNutritionEstimate(420, 14, 68, 11, 7, 390),
            equipmentRequirements: [new EquipmentRequirement("Large pot")],
            prepMinutes: 10,
            cookMinutes: 25,
            totalMinutes: 35,
            createdAtUtc: now);
        var recipe = Recipe.Create(ownerId, version, now, recipeId);
        recipe.Publish(now);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
    }
}

await using (var db = new CookbookDbContext(Options<CookbookDbContext>(connection)))
{
    await MigrateAsync(db, "Cookbook");
    var folder = await db.Folders
        .Include(item => item.SavedRecipes)
        .SingleOrDefaultAsync(item => item.Id == folderId);
    if (folder is null)
    {
        folder = Folder.Create(ownerId, "Weeknight Favorites", now, folderId);
        db.Folders.Add(folder);
        await db.SaveChangesAsync();
    }

    if (!folder.SavedRecipes.Any(saved =>
            saved.RecipeId == recipeId && saved.RecipeVersion == 1))
    {
        folder.AddSavedRecipe(SavedRecipe.Create(folderId, recipeId, 1, now));
        await db.SaveChangesAsync();
    }
}

await using (var db = new EquipmentDbContext(Options<EquipmentDbContext>(connection)))
{
    await MigrateAsync(db, "Equipment");
    if (!await db.EquipmentItems.AnyAsync(item => item.Id == equipmentId))
    {
        db.EquipmentItems.Add(new EquipmentItem(
            equipmentId,
            ownerId.ToString("D"),
            "Large stock pot",
            EquipmentType.Cookware,
            1,
            now,
            EquipmentCondition.Good,
            brand: "Starter Kitchen",
            location: "Lower cabinet"));
        await db.SaveChangesAsync();
    }
}

await using (var db = new PantryDbContext(Options<PantryDbContext>(connection)))
{
    await MigrateAsync(db, "Pantry");

    var owner = ownerId.ToString("D");
    const string name = "Cherry tomatoes";
    var ownerKey = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(owner)));
    var normalizedName = string.Join(
        ' ',
        name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    await db.Database.ExecuteSqlInterpolatedAsync($"""
        IF NOT EXISTS (SELECT 1 FROM [dbo].[PantryItems] WHERE [Id] = {pantryItemId})
        BEGIN
            INSERT INTO [dbo].[PantryItems]
                ([Id], [OwnerId], [Name], [Type], [CustomCategory], [Notes],
                 [Quantity], [Unit], [ExpiresAt], [Availability], [CreatedAt],
                 [UpdatedAt], [OwnerKey], [NameNormalized])
            VALUES
                ({pantryItemId}, {owner}, {name}, {(int)PantryType.Vegetables},
                 NULL, N'Ripe and ready to use', 500, {(int)Unit.Gram}, NULL,
                 {(int)Availability.Available}, {now}, {now}, {ownerKey}, {normalizedName})
        END
        """);
}

await using (var db = new MealPlannerDbContext(Options<MealPlannerDbContext>(connection)))
{
    await MigrateAsync(db, "Meal Planner");
    const string planName = "Starter Week";
    var plan = await db.MealPlans.SingleOrDefaultAsync(item =>
        item.OwnerId == ownerId.ToString("D") && item.Name == planName);
    if (plan is null)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        plan = MealPlan.Create(ownerId.ToString("D"), planName, today, today.AddDays(6), now);
        db.MealPlans.Add(plan);
        await db.SaveChangesAsync();
    }

    var entryDate = plan.StartDate;
    if (!await db.MealEntries.AnyAsync(entry =>
            entry.MealPlanId == plan.Id &&
            entry.Date == entryDate &&
            entry.Slot == MealSlot.Dinner))
    {
        var entry = plan.AddEntry(
            entryDate,
            MealSlot.Dinner,
            "Roasted Tomato and Spinach Pasta",
            recipeId,
            4,
            slotAlreadyExists: false,
            now);
        db.MealEntries.Add(entry);
        await db.SaveChangesAsync();
    }
}

await using (var db = new UserServiceDbContext(Options<UserServiceDbContext>(connection)))
{
    await MigrateAsync(db, "User Service");
    var subject = ownerId.ToString("D");
    if (!await db.Users.AnyAsync(user => user.Subject == subject))
    {
        db.Users.Add(UserProfile.Create(
            subject,
            "local-test-user",
            "local-test-user@example.test",
            "Local Test User",
            now));
        await db.SaveChangesAsync();
    }
}

Console.WriteLine($"Starter data is ready in the isolated {databaseName} database.");
Console.WriteLine("Operational tables are left empty; the Pantry starter row is inserted without creating an outbox event.");

static DbContextOptions<TContext> Options<TContext>(string connection)
    where TContext : DbContext =>
    new DbContextOptionsBuilder<TContext>()
        .UseSqlServer(connection, sqlServer =>
            sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "dbo"))
        .Options;

static async Task CreateDatabaseIfMissingAsync(string masterConnection, string databaseName)
{
    await using var connection = new SqlConnection(masterConnection);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}]";
    await command.ExecuteNonQueryAsync();
}

static async Task MigrateAsync(DbContext db, string serviceName)
{
    Console.WriteLine($"Applying {serviceName} migrations...");
    await db.Database.MigrateAsync();
}
