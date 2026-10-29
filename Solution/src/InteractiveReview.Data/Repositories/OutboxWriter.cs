using InteractiveReview.Data.Context;

namespace InteractiveReview.Data.Repositories;

internal sealed class OutboxWriter(InteractiveReviewDbContext context) : IOutboxWriter
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await context.OutboxMessages.AddAsync(message, cancellationToken);
}
