-- Run against an unmodified isolated test database after seeding.
SET NOCOUNT ON;

DECLARE @owner uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @subject nvarchar(200) = CONVERT(nvarchar(36), @owner);

IF (SELECT COUNT(*) FROM recipe_management.Recipes
    WHERE OwnerId = @owner AND Id IN (
        '10000000-0000-0000-0000-000000000001',
        '10000000-0000-0000-0000-000000000002',
        '10000000-0000-0000-0000-000000000003',
        '10000000-0000-0000-0000-000000000004')) <> 4
    THROW 51000, 'Missing starter recipes.', 1;

IF (SELECT COUNT(*) FROM cookbook.Folders f
    JOIN cookbook.SavedRecipes s ON s.FolderId = f.Id
    WHERE f.OwnerId = @owner AND s.RecipeVersion = 1 AND
      ((f.Id = '20000000-0000-0000-0000-000000000001' AND s.RecipeId = '10000000-0000-0000-0000-000000000001')
       OR (f.Id = '20000000-0000-0000-0000-000000000002' AND s.RecipeId = '10000000-0000-0000-0000-000000000002')
       OR (f.Id = '20000000-0000-0000-0000-000000000003' AND s.RecipeId = '10000000-0000-0000-0000-000000000003'))) <> 3
    THROW 51000, 'Missing starter cookbook folders or saved recipes.', 1;

IF (SELECT COUNT(*) FROM interactive_review.Reviews
    WHERE OwnerId = @owner AND Id IN (
        '50000000-0000-0000-0000-000000000001',
        '50000000-0000-0000-0000-000000000002',
        '50000000-0000-0000-0000-000000000003')) <> 3
    THROW 51000, 'Missing starter reviews.', 1;

IF (SELECT COUNT(*) FROM ai_recipe.GenerationRequests
    WHERE OwnerId = @owner AND Id IN (
        '60000000-0000-0000-0000-000000000001',
        '60000000-0000-0000-0000-000000000002',
        '60000000-0000-0000-0000-000000000003')) <> 3
    THROW 51000, 'Missing starter generation requests.', 1;

IF (SELECT COUNT(*) FROM equipment.EquipmentItems
    WHERE OwnerId = @subject AND Id IN (
        '30000000-0000-0000-0000-000000000001',
        '30000000-0000-0000-0000-000000000002',
        '30000000-0000-0000-0000-000000000003',
        '30000000-0000-0000-0000-000000000004',
        '30000000-0000-0000-0000-000000000005',
        '30000000-0000-0000-0000-000000000006')) <> 6
    THROW 51000, 'Missing starter equipment.', 1;

IF (SELECT COUNT(*) FROM dbo.PantryItems
    WHERE OwnerId = @subject AND Id IN (
        '40000000-0000-0000-0000-000000000001',
        '40000000-0000-0000-0000-000000000002',
        '40000000-0000-0000-0000-000000000003',
        '40000000-0000-0000-0000-000000000004',
        '40000000-0000-0000-0000-000000000005',
        '40000000-0000-0000-0000-000000000006',
        '40000000-0000-0000-0000-000000000007',
        '40000000-0000-0000-0000-000000000008')) <> 8
    THROW 51000, 'Missing starter pantry items.', 1;

IF (SELECT COUNT(*) FROM dbo.MealPlans WHERE OwnerId = @subject
    AND Name IN ('Starter Week', 'Everyday Favorites')) <> 2
    THROW 51000, 'Missing starter meal plans.', 1;

IF (SELECT COUNT(*) FROM dbo.MealEntries e
    JOIN dbo.MealPlans p ON p.Id = e.MealPlanId
    WHERE p.OwnerId = @subject AND p.Name = 'Everyday Favorites'
      AND e.RecipeId IN (
        '10000000-0000-0000-0000-000000000002',
        '10000000-0000-0000-0000-000000000003',
        '10000000-0000-0000-0000-000000000004')) <> 3
    THROW 51000, 'Missing starter meal entries.', 1;

IF (SELECT COUNT(*) FROM dbo.Users WHERE DeletedAt IS NULL AND Subject IN (
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000002')) <> 2
    THROW 51000, 'Missing starter user profiles.', 1;

PRINT 'Starter data verification passed.';
