using UserService.Domain.Entities;

namespace UserService.Data.Repositories;

public interface IUserProfileRepository : IRepository<UserProfile>
{
    /// <summary>Returns the non-deleted profile only when it belongs to <paramref name="subject"/>; otherwise null.</summary>
    Task<UserProfile?> GetOwnedAsync(Guid id, string subject, bool forUpdate, CancellationToken token);

    Task<UserProfile?> GetBySubjectAsync(string subject, CancellationToken token);

    /// <summary>Sets the version the caller last saw so the next save fails if the row changed since.</summary>
    void SetExpectedVersion(UserProfile profile, byte[] version);
}
