using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebIII_RepoInterfaces
{
    // Mock classes made them just for an idea of what they could look like and to not have errors in the interfaces.
    public class Recipe
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<Ingredient> Ingredients { get; set; } = new();
        public List<string> Steps { get; set; } = new();
        public int PrepMinutes { get; set; }
        public int Calories { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class Ingredient
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class Cookbook
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OwnerUserId { get; set; }
        public List<int> RecipeIds { get; set; } = new();
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Allergies { get; set; } = new();
        public List<string> DietaryExclusions { get; set; } = new();
    }

    public class Review
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public int UserId { get; set; }
        public int Stars { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PantryItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime? Expiration { get; set; }
    }

    public class Equipment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class MealSlot
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public string MealType { get; set; } = string.Empty; // Breakfast/Lunch/Dinner
        public int? RecipeId { get; set; }
    }
}