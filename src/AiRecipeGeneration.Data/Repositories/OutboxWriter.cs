using AiRecipeGeneration.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace AiRecipeGeneration.Data.Repositories;

internal sealed class OutboxWriter(AiRecipeDbContext context) : IOutboxWriter
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await context.OutboxMessages.AddAsync(message, cancellationToken);
}
