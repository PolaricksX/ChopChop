using Microsoft.EntityFrameworkCore;

namespace UserPantry.Infrastructure;

public sealed class PantryDbContext(DbContextOptions<PantryDbContext> options) : DbContext(options)
{
}
