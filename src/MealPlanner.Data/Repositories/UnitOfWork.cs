using MealPlanner.Data.Context;
using MealPlanner.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Data.Repositories;

public sealed class UnitOfWork(MealPlannerDbContext context, IRepositoryProvider provider) : IUnitOfWork
{
    public IRepository<T> Repository<T>()
        where T : class =>
        provider.GetRepository<T>();

    public TRepository CustomRepository<TRepository>()
        where TRepository : class =>
        provider.GetCustomRepository<TRepository>();

    public async Task<int> SaveChangesAsync(CancellationToken token = default)
    {
        try
        {
            return await context.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The resource was modified by another request. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
        {
            var message = sql.Message.Contains("UX_MealEntries_Plan_Date_Slot", StringComparison.Ordinal)
                ? "A meal entry already exists for this date and slot."
                : "A request conflicts with an existing resource or Idempotency-Key.";
            throw new ConflictException(message);
        }
    }
}
