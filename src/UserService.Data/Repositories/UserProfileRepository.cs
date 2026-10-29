using Microsoft.EntityFrameworkCore;
using UserService.Data.Context;
using UserService.Domain.Entities;

namespace UserService.Data.Repositories;

public sealed class UserProfileRepository(UserServiceDbContext context)
    : EfRepository<UserProfile>(context), IUserProfileRepository
{
    public Task<UserProfile?> GetOwnedAsync(Guid id, string subject, bool forUpdate, CancellationToken token)
    {
        var query = Context.Users.Where(x => x.Id == id && x.Subject == subject);
        return (forUpdate ? query : query.AsNoTracking()).FirstOrDefaultAsync(token);
    }

    public Task<UserProfile?> GetBySubjectAsync(string subject, CancellationToken token) =>
        Context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Subject == subject, token);

    public void SetExpectedVersion(UserProfile profile, byte[] version) =>
        Context.Entry(profile).Property(x => x.RowVersion).OriginalValue = version;
}
