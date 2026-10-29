using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UserService.Data.Configurations;
using UserService.Data.Context;
using UserService.Domain.Exceptions;

namespace UserService.Data.Repositories;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>()
        where T : class;

    TRepository CustomRepository<TRepository>()
        where TRepository : class;

    /// <summary>Persists all staged changes together. Database constraint violations surface as domain exceptions.</summary>
    Task<int> SaveChangesAsync(CancellationToken token = default);
}

public sealed class UnitOfWork(UserServiceDbContext context, IRepositoryProvider provider) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

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
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            throw Translate(sql.Message);
        }
    }

    private static Exception Translate(string message)
    {
        if (message.Contains(IndexNames.UserEmail, StringComparison.Ordinal))
        {
            return new DuplicateUserException("email", "A user with this email already exists.");
        }

        if (message.Contains(IndexNames.UserUsername, StringComparison.Ordinal))
        {
            return new DuplicateUserException("username", "A user with this username already exists.");
        }

        if (message.Contains(IndexNames.UserSubject, StringComparison.Ordinal))
        {
            return new DuplicateUserException("subject", "A profile already exists for this account.");
        }

        if (message.Contains(IndexNames.IdempotencyKey, StringComparison.Ordinal))
        {
            return new ConflictException("A request with this Idempotency-Key is already being processed.");
        }

        return new ConflictException("The request conflicts with existing data.");
    }
}
