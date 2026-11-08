using Equipment.Data.Context;
using Equipment.Data.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Equipment.Data.Repositories;

public sealed class UnitOfWork(EquipmentDbContext context, IRepositoryProvider provider) : IUnitOfWork
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
            throw new EquipmentConcurrencyException();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
                && sqlException.Message.Contains(
                    "UX_Equipment_Owner_NormalizedName_Type",
                    StringComparison.Ordinal))
        {
            throw new EquipmentDuplicateException();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
                && sqlException.Message.Contains(
                    "UX_EquipmentIdempotency_Owner_Key",
                    StringComparison.Ordinal))
        {
            throw new EquipmentIdempotencyKeyException();
        }
    }
}
