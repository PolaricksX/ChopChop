-- Local development sample data for Recipe Management. Safe to re-run.
SET NOCOUNT ON;
USE ChopChop;

DECLARE @Owner uniqueidentifier = '00000000-0000-0000-0000-0000000000a1';
DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();
DECLARE @Sep nchar(1) = NCHAR(31);

DECLARE @Recipes TABLE (
    Id uniqueidentifier, Title nvarchar(200), Description nvarchar(max), Servings int,
    Allergens nvarchar(max), Tags nvarchar(max), DietaryLabels nvarchar(max),
    Prep int, Cook int, Calories decimal(18,3), Protein decimal(18,3), Carbs decimal(18,3),
    Fat decimal(18,3), Fiber decimal(18,3), Sodium decimal(18,3));

INSERT INTO @Recipes VALUES
('10000000-0000-0000-0000-000000000001', N'Classic Tomato Pasta',
 N'A simple weeknight pasta with a garlicky tomato sauce and fresh basil.', 4,
 N'WheatAndTriticale', N'dinner' + @Sep + N'pasta' + @Sep + N'quick', N'vegetarian',
 10, 20, 420, 14, 78, 7, 5, 560),
('10000000-0000-0000-0000-000000000002', N'Veggie Chickpea Curry',
 N'A creamy, mildly spiced curry with chickpeas and spinach, served over rice.', 4,
 N'', N'dinner' + @Sep + N'curry', N'vegan' + @Sep + N'gluten-free',
 15, 25, 380, 12, 55, 12, 11, 480),
('10000000-0000-0000-0000-000000000003', N'Fluffy Blueberry Pancakes',
 N'Light weekend pancakes studded with blueberries.', 4,
 N'WheatAndTriticale,Milk,Eggs', N'breakfast' + @Sep + N'sweet', N'vegetarian',
 10, 15, 340, 9, 52, 11, 2, 420);

INSERT INTO recipe_management.Recipes (Id, OwnerId, Status, CreatedAtUtc, UpdatedAtUtc)
SELECT r.Id, @Owner, 'Published', @Now, @Now FROM @Recipes r
WHERE NOT EXISTS (SELECT 1 FROM recipe_management.Recipes x WHERE x.Id = r.Id);

INSERT INTO recipe_management.RecipeVersions
    (RecipeId, Version, Title, Description, Servings, Allergens, AllergenStatus, Tags, DietaryLabels,
     PrepMinutes, CookMinutes, TotalMinutes, IsAiGenerated, Status, CreatedAtUtc, PublishedAtUtc, ConcurrencyToken)
SELECT r.Id, 1, r.Title, r.Description, r.Servings, r.Allergens, 'Declared', r.Tags, r.DietaryLabels,
     r.Prep, r.Cook, r.Prep + r.Cook, 0, 'Published', @Now, @Now, 0
FROM @Recipes r
WHERE NOT EXISTS (SELECT 1 FROM recipe_management.RecipeVersions v WHERE v.RecipeId = r.Id AND v.Version = 1);

INSERT INTO recipe_management.NutritionEstimates
    (RecipeId, Version, Calories, ProteinGrams, CarbohydratesGrams, FatGrams, FiberGrams, SodiumMilligrams)
SELECT r.Id, 1, r.Calories, r.Protein, r.Carbs, r.Fat, r.Fiber, r.Sodium FROM @Recipes r
WHERE NOT EXISTS (SELECT 1 FROM recipe_management.NutritionEstimates n WHERE n.RecipeId = r.Id AND n.Version = 1);

DECLARE @Ingredients TABLE (RecipeId uniqueidentifier, Position int, Name nvarchar(200), Quantity decimal(18,3), Unit nvarchar(50));
INSERT INTO @Ingredients VALUES
('10000000-0000-0000-0000-000000000001', 0, N'Spaghetti', 400, N'g'),
('10000000-0000-0000-0000-000000000001', 1, N'Canned crushed tomatoes', 800, N'g'),
('10000000-0000-0000-0000-000000000001', 2, N'Garlic cloves', 3, N'piece'),
('10000000-0000-0000-0000-000000000001', 3, N'Olive oil', 2, N'tbsp'),
('10000000-0000-0000-0000-000000000001', 4, N'Fresh basil', 10, N'leaves'),
('10000000-0000-0000-0000-000000000002', 0, N'Chickpeas, drained', 800, N'g'),
('10000000-0000-0000-0000-000000000002', 1, N'Coconut milk', 400, N'ml'),
('10000000-0000-0000-0000-000000000002', 2, N'Onion, diced', 1, N'piece'),
('10000000-0000-0000-0000-000000000002', 3, N'Curry powder', 2, N'tbsp'),
('10000000-0000-0000-0000-000000000002', 4, N'Baby spinach', 150, N'g'),
('10000000-0000-0000-0000-000000000003', 0, N'All-purpose flour', 250, N'g'),
('10000000-0000-0000-0000-000000000003', 1, N'Milk', 300, N'ml'),
('10000000-0000-0000-0000-000000000003', 2, N'Egg', 1, N'piece'),
('10000000-0000-0000-0000-000000000003', 3, N'Baking powder', 2, N'tsp'),
('10000000-0000-0000-0000-000000000003', 4, N'Blueberries', 150, N'g');

INSERT INTO recipe_management.Ingredients (RecipeId, Version, Position, Name, Quantity, Unit)
SELECT i.RecipeId, 1, i.Position, i.Name, i.Quantity, i.Unit FROM @Ingredients i
WHERE NOT EXISTS (SELECT 1 FROM recipe_management.Ingredients x WHERE x.RecipeId = i.RecipeId AND x.Version = 1 AND x.Position = i.Position);

DECLARE @Steps TABLE (RecipeId uniqueidentifier, StepOrder int, Content nvarchar(2000));
INSERT INTO @Steps VALUES
('10000000-0000-0000-0000-000000000001', 1, N'Boil the spaghetti in salted water until al dente.'),
('10000000-0000-0000-0000-000000000001', 2, N'Saute the garlic in olive oil, add the tomatoes, and simmer for 15 minutes.'),
('10000000-0000-0000-0000-000000000001', 3, N'Toss the pasta with the sauce and top with torn basil.'),
('10000000-0000-0000-0000-000000000002', 1, N'Soften the onion in a large pan, then stir in the curry powder for one minute.'),
('10000000-0000-0000-0000-000000000002', 2, N'Add the chickpeas and coconut milk and simmer for 20 minutes.'),
('10000000-0000-0000-0000-000000000002', 3, N'Stir in the spinach until wilted and serve over rice.'),
('10000000-0000-0000-0000-000000000003', 1, N'Whisk the flour and baking powder, then mix in the milk and egg until just combined.'),
('10000000-0000-0000-0000-000000000003', 2, N'Fold in the blueberries.'),
('10000000-0000-0000-0000-000000000003', 3, N'Cook scoops of batter on a greased pan over medium heat until golden on both sides.');

INSERT INTO recipe_management.InstructionSteps (RecipeId, Version, [Order], Content)
SELECT s.RecipeId, 1, s.StepOrder, s.Content FROM @Steps s
WHERE NOT EXISTS (SELECT 1 FROM recipe_management.InstructionSteps x WHERE x.RecipeId = s.RecipeId AND x.Version = 1 AND x.[Order] = s.StepOrder);

DECLARE @Equipment TABLE (RecipeId uniqueidentifier, Position int, Name nvarchar(200));
INSERT INTO @Equipment VALUES
('10000000-0000-0000-0000-000000000001', 0, N'Large pot'),
('10000000-0000-0000-0000-000000000001', 1, N'Saucepan'),
('10000000-0000-0000-0000-000000000002', 0, N'Large pan'),
('10000000-0000-0000-0000-000000000003', 0, N'Non-stick pan'),
('10000000-0000-0000-0000-000000000003', 1, N'Mixing bowl');

INSERT INTO recipe_management.EquipmentRequirements (RecipeId, Version, Position, Name, Required, MatchingMetadata)
SELECT e.RecipeId, 1, e.Position, e.Name, 1, NULL FROM @Equipment e
WHERE NOT EXISTS (SELECT 1 FROM recipe_management.EquipmentRequirements x WHERE x.RecipeId = e.RecipeId AND x.Version = 1 AND x.Position = e.Position);

SELECT COUNT(*) AS PublishedRecipes FROM recipe_management.Recipes WHERE Status = 'Published';
