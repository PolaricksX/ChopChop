using Pantry.Domain.Entities;

namespace Pantry.Domain.Services;

public static class PantryRules
{
    public static bool IsExpiring(PantryItem item, DateTimeOffset now, TimeSpan horizon) =>
        item.ExpiresAt is not null && item.ExpiresAt <= now.Add(horizon);

    public static bool IsDuplicate(PantryItem first, PantryItem second) =>
        first.HasSameIdentity(second);
}
