using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebIII_RepoInterfaces
{
    /// <summary>
    /// Defines CRUD operations for Recipe repository, repo will be all recipes in the system, not just user recipes.
    /// </summary>
    public interface IRecipe
    {
        Task<IEnumerable<Recipe>> GetAllRecipes();
        Task<Recipe?> GetRecipeById(int id);
        Task AddRecipe(Recipe recipe);
        Task UpdateRecipe(Recipe recipe);
        Task DeleteRecipe(int id);
    }
    /// <summary>
    /// Defines CRUD operations for Cookbook repository, each user can have multiple cookbooks, and each cookbook can contain multiple recipes.
    /// A cookbook is a collection of recipes that a user can create and manage.
    /// </summary>
    public interface ICookbook
    {
        Task<IEnumerable<Cookbook>> GetUserCookbooks(int userId);
        Task<Cookbook> CreateCookbook(Cookbook cookbook);
        Task AddRecipeToCookbook(int cookbookId, int recipeId);
        Task RemoveRecipeFromCookbook(int cookbookId, int recipeId);
    }
    /// <summary>
    /// Defines CRUD operations for Review repository, each recipe can have multiple reviews, and each review is associated with a user.
    /// </summary>
    public interface IReview
    {
        Task<Review> AddReview(Review review);
        Task<IEnumerable<Review>> GetReviewsForRecipe(int recipeId);
        Task UpdateReview(Review review);
        Task DeleteReview(int reviewId);
        Task<string> UploadImage(int reviewId, byte[] image);
    }
    /// <summary>
    /// Defines CRUD operations for AI Recipe repository, this interface is responsible for generating recipes based on user prompts.
    /// </summary>
    public interface IAIRecipe
    {
        Task<Recipe> GenerateRecipeFromPrompt(int userId, string prompt, IEnumerable<PantryItem>? pantry = null, IEnumerable<Equipment>? equipment = null);
        Task<Recipe> ModifyRecipeSwapIngredients(int recipeId, IDictionary<string, string> swaps);
        Task<Recipe> ScaleRecipe(int recipeId, decimal scaleFactor);
    }
    /// <summary>
    /// Defines CRUD operations for Meal Plan repository, each user can have a meal plan that consists of multiple meal slots, and each meal slot can be filled with a recipe.
    /// </summary>
    public interface IMealPlan
    {
        Task<IEnumerable<MealSlot>> GetMealPlan(int userId);
        Task<IEnumerable<MealSlot>> GetMealPlanByDateRange(int userId, DateTime from, DateTime to);
        Task<MealSlot> AddMealSlot(MealSlot slot);
        Task UpdateMealSlot(MealSlot slot);
        Task DeleteMealSlot(int slotId);
        Task<MealSlot?> RecommendFillSlot(int userId, DateTime date, string mealType);
    }
    /// <summary>
    /// Defines CRUD operations for Pantry repository, each user can have a pantry that consists of multiple pantry items, 
    /// and this interface allows for managing the pantry items, including adding, updating, deleting, and retrieving items, as well as checking for expiring items.
    /// </summary>
    public interface IPantry
    {
        Task<IEnumerable<PantryItem>> GetPantry(int userId);
        Task<PantryItem> AddItem(PantryItem item);
        Task UpdateItem(PantryItem item);
        Task DeleteItem(int itemId);
        Task<IEnumerable<PantryItem>> GetExpiringItems(int userId, TimeSpan horizon);
    }
    /// <summary>
    /// Defines CRUD operations for Equipment repository, each user can have a list of equipment that they own, and this interface allows for checking if a user has the necessary equipment for a recipe.
    /// </summary>
    public interface IEquipment
    {
        Task<IEnumerable<Equipment>> GetEquipment(int userId);
        Task<Equipment> AddEquipment(Equipment equipment);
        Task DeleteEquipment(int equipmentId);
        Task<bool> HasEquipmentForRecipe(int userId, Recipe recipe);
    }
    /// <summary>
    /// Defines CRUD operations for User repository, this interface is responsible for user registration, authentication, and profile management.
    /// </summary>
    public interface IUser
    {
        Task<User> Register(User user, string password);
        Task<string?> Authenticate(string usernameOrEmail, string password);
        Task<User?> GetUserById(int id);
        Task UpdateProfile(User user);
    }
}