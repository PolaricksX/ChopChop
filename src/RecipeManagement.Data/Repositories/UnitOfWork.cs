using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RecipeManagement.Data.Context;
using RecipeManagement.Domain.Exceptions;

namespace RecipeManagement.Data.Repositories;

public sealed class UnitOfWork(RecipeManagementDbContext context, IRepositoryProvider provider) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : class => provider.GetRepository<T>();
    public TRepository CustomRepository<TRepository>() where TRepository : class => provider.GetCustomRepository<TRepository>();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RecipeConflictException("The resource was modified by another request. Reload it and try again."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new RecipeConflictException("The request conflicts with existing data."); }
    }
}
