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
using UserService.Data.Context;
using UserService.Domain.Entities;
using RecipeNutritionEstimate = RecipeManagement.Domain.Entities.NutritionEstimate;

const string databaseName = "ChopChopTest";
var ownerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
var recipeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
var folderId = Guid.Parse("20000000-0000-0000-0000-000000000001");
var equipmentId = Guid.Parse("30000000-0000-0000-0000-000000000001");
var pantryItemId = Guid.Parse("40000000-0000-0000-0000-000000000001");
var reviewId = Guid.Parse("50000000-0000-0000-0000-000000000001");
var generationRequestId = Guid.Parse("60000000-0000-0000-0000-000000000001");
var now = DateTimeOffset.UtcNow;
var additionalRecipes = new[]
{
    new RecipeSeed(Guid.Parse("10000000-0000-0000-0000-000000000002"),
        "Chickpea and Lemon Rice", "A lemony rice bowl with chickpeas and fresh herbs.",
        "Rice", "Chickpeas", "Simmer the rice, then fold in chickpeas and lemon.", "vegetarian"),
    new RecipeSeed(Guid.Parse("10000000-0000-0000-0000-000000000003"),
        "Apple Cinnamon Oatmeal", "Warm oats with apple and cinnamon.",
        "Rolled oats", "Apple", "Simmer the oats and apple, then sprinkle with cinnamon.", "breakfast"),
    new RecipeSeed(Guid.Parse("10000000-0000-0000-0000-000000000004"),
        "Roasted Chicken and Vegetables", "A simple tray of chicken and seasonal vegetables.",
        "Chicken", "Carrots", "Roast the chicken and vegetables until cooked through.", "dinner"),
};

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

    var completedId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    if (!await db.GenerationRequests.AnyAsync(request => request.Id == completedId))
    {
        var request = GenerationRequest.Create(
            completedId, ownerId, "starter-generation-request-2",
            "Suggest a quick chickpea lunch.", true, ["chickpeas", "rice"], "starter-seed", now);
        request.MarkProcessing(now);
        request.Complete(new RecipeDraft(
            "Chickpea Rice Bowl", "An AI-generated chickpea and rice bowl for review.",
            2, 10, 15,
            [new RecipeIngredient("Chickpeas", 1, "cup", []), new RecipeIngredient("Rice", 1, "cup", [])],
            ["Cook the rice.", "Warm the chickpeas and serve with the rice."],
            [], ["Saucepan"], new AiRecipeGeneration.Domain.NutritionEstimate(350, 12, 58, 6),
            IsAiGenerated: true, NutritionIsEstimate: true), now);
        db.GenerationRequests.Add(request);
        await db.SaveChangesAsync();
    }

    var failedId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    if (!await db.GenerationRequests.AnyAsync(request => request.Id == failedId))
    {
        var request = GenerationRequest.Create(
            failedId, ownerId, "starter-generation-request-3",
            "Suggest a warming breakfast.", false, [], "starter-seed", now);
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

    var examples = new[]
    {
        (Id: Guid.Parse("50000000-0000-0000-0000-000000000002"), RecipeId: additionalRecipes[0].Id,
            Rating: 4, Body: "A comforting and easy lunch.", Published: true),
        (Id: Guid.Parse("50000000-0000-0000-0000-000000000003"), RecipeId: additionalRecipes[1].Id,
            Rating: 5, Body: "A cozy breakfast with plenty of flavor.", Published: false),
    };
    foreach (var example in examples)
    {
        if (await db.Reviews.AnyAsync(review => review.Id == example.Id)) continue;
        var review = Review.Create(example.Id, example.RecipeId, ownerId, example.Rating, example.Body, null, now);
        if (example.Published) review.Moderate(ownerId, ReviewStatus.Published, "Starter example", now);
        db.Reviews.Add(review);
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

    foreach (var example in additionalRecipes)
    {
        if (await db.Recipes.AnyAsync(recipe => recipe.Id == example.Id)) continue;
        var version = new RecipeVersion(
            example.Title,
            example.Description,
            2,
            [new Ingredient(example.FirstIngredient, 1, "cup"), new Ingredient(example.SecondIngredient, 1, "cup")],
            [new InstructionStep(1, example.Instruction)],
            tags: [example.Tag, "starter"],
            prepMinutes: 10, cookMinutes: 20, totalMinutes: 30,
            createdAtUtc: now);
        var recipe = Recipe.Create(ownerId, version, now, example.Id);
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

    foreach (var example in new[]
    {
        (Id: Guid.Parse("20000000-0000-0000-0000-000000000002"), Name: "Easy Lunches", Recipe: additionalRecipes[0]),
        (Id: Guid.Parse("20000000-0000-0000-0000-000000000003"), Name: "Breakfast Ideas", Recipe: additionalRecipes[1]),
    })
    {
        var savedFolder = await db.Folders.Include(item => item.SavedRecipes)
            .SingleOrDefaultAsync(item => item.Id == example.Id);
        if (savedFolder is null)
        {
            savedFolder = Folder.Create(ownerId, example.Name, now, example.Id);
            db.Folders.Add(savedFolder);
        }
        if (!savedFolder.SavedRecipes.Any(saved => saved.RecipeId == example.Recipe.Id && saved.RecipeVersion == 1))
            savedFolder.AddSavedRecipe(SavedRecipe.Create(example.Id, example.Recipe.Id, 1, now));
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

    foreach (var example in new[]
    {
        (Id: Guid.Parse("30000000-0000-0000-0000-000000000002"), Name: "Chef's knife", Type: EquipmentType.Knives, Quantity: 1, Location: "Knife block"),
        (Id: Guid.Parse("30000000-0000-0000-0000-000000000003"), Name: "Baking sheet", Type: EquipmentType.Bakeware, Quantity: 2, Location: "Oven drawer"),
        (Id: Guid.Parse("30000000-0000-0000-0000-000000000004"), Name: "Blender", Type: EquipmentType.Appliances, Quantity: 1, Location: "Counter"),
        (Id: Guid.Parse("30000000-0000-0000-0000-000000000005"), Name: "Measuring cups", Type: EquipmentType.Measuring, Quantity: 1, Location: "Utensil drawer"),
        (Id: Guid.Parse("30000000-0000-0000-0000-000000000006"), Name: "Mixing bowl", Type: EquipmentType.PrepTools, Quantity: 2, Location: "Upper cabinet"),
    })
    {
        if (await db.EquipmentItems.AnyAsync(item => item.Id == example.Id)) continue;
        var item = new EquipmentItem(example.Id, ownerId.ToString("D"), example.Name, example.Type,
            example.Quantity, now, location: example.Location);
        if (await db.EquipmentItems.AnyAsync(existing =>
                existing.OwnerId == item.OwnerId && existing.NormalizedName == item.NormalizedName && existing.Type == item.Type))
        {
            Console.WriteLine($"Skipping equipment starter item already present: {example.Name}.");
            continue;
        }
        db.EquipmentItems.Add(item);
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

    var examples = new[]
    {
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000002"), Name: "Rice", Type: PantryType.Grains, Quantity: 2m, Unit: Unit.Kilogram, Days: (int?)null),
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000003"), Name: "Chickpeas", Type: PantryType.Custom, Quantity: 2m, Unit: Unit.Count, Days: (int?)90),
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000004"), Name: "Apples", Type: PantryType.Fruits, Quantity: 4m, Unit: Unit.Count, Days: (int?)14),
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000005"), Name: "Rolled oats", Type: PantryType.Grains, Quantity: 750m, Unit: Unit.Gram, Days: (int?)120),
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000006"), Name: "Cinnamon", Type: PantryType.Spices, Quantity: 50m, Unit: Unit.Gram, Days: (int?)null),
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000007"), Name: "Carrots", Type: PantryType.Vegetables, Quantity: 500m, Unit: Unit.Gram, Days: (int?)7),
        (Id: Guid.Parse("40000000-0000-0000-0000-000000000008"), Name: "Chicken breast", Type: PantryType.Meat, Quantity: 2m, Unit: Unit.Count, Days: (int?)3),
    };
    foreach (var example in examples)
    {
        var normalized = string.Join(' ', example.Name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        var customCategory = example.Type == PantryType.Custom ? "Legumes" : null;
        var expiresAt = example.Days is int days ? now.AddDays(days) : (DateTimeOffset?)null;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS (SELECT 1 FROM [dbo].[PantryItems] WHERE [Id] = {example.Id})
               AND NOT EXISTS (SELECT 1 FROM [dbo].[PantryItems]
                   WHERE [OwnerKey] = {ownerKey} AND [NameNormalized] = {normalized}
                     AND [Type] = {(int)example.Type}
                     AND [Availability] = {(int)Availability.Available})
            BEGIN
                INSERT INTO [dbo].[PantryItems]
                    ([Id], [OwnerId], [Name], [Type], [CustomCategory], [Notes],
                     [Quantity], [Unit], [ExpiresAt], [Availability], [CreatedAt],
                     [UpdatedAt], [OwnerKey], [NameNormalized])
                VALUES
                    ({example.Id}, {owner}, {example.Name}, {(int)example.Type}, {customCategory},
                     NULL, {example.Quantity}, {(int)example.Unit}, {expiresAt},
                     {(int)Availability.Available}, {now}, {now}, {ownerKey}, {normalized})
            END
            """);
    }
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
    if (plan.Status is not (MealPlanStatus.Completed or MealPlanStatus.Cancelled) &&
        !await db.MealEntries.AnyAsync(entry =>
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

    const string extraPlanName = "Everyday Favorites";
    var extraPlan = await db.MealPlans.SingleOrDefaultAsync(item =>
        item.OwnerId == ownerId.ToString("D") && item.Name == extraPlanName);
    if (extraPlan is null)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        extraPlan = MealPlan.Create(ownerId.ToString("D"), extraPlanName, today, today.AddDays(6), now);
        db.MealPlans.Add(extraPlan);
        await db.SaveChangesAsync();
    }

    foreach (var example in new[]
    {
        (Offset: 0, Slot: MealSlot.Breakfast, Recipe: additionalRecipes[1]),
        (Offset: 0, Slot: MealSlot.Lunch, Recipe: additionalRecipes[0]),
        (Offset: 1, Slot: MealSlot.Dinner, Recipe: additionalRecipes[2]),
    })
    {
        var date = extraPlan.StartDate.AddDays(example.Offset);
        if (date > extraPlan.EndDate || extraPlan.Status is MealPlanStatus.Completed or MealPlanStatus.Cancelled)
            continue;
        if (await db.MealEntries.AnyAsync(entry =>
                entry.MealPlanId == extraPlan.Id && entry.Date == date && entry.Slot == example.Slot))
            continue;
        db.MealEntries.Add(extraPlan.AddEntry(date, example.Slot, example.Recipe.Title,
            example.Recipe.Id, 2, slotAlreadyExists: false, now));
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

    var otherSubject = Guid.Parse("00000000-0000-0000-0000-000000000002").ToString("D");
    if (!await db.Users.IgnoreQueryFilters().AnyAsync(user => user.Subject == otherSubject))
    {
        db.Users.Add(UserProfile.Create(otherSubject, "local-test-user-2",
            "local-test-user-2@example.test", "Second Test User", now));
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

internal sealed record RecipeSeed(
    Guid Id, string Title, string Description, string FirstIngredient,
    string SecondIngredient, string Instruction, string Tag);
