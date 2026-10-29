using AiRecipeGeneration.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiRecipeGeneration.Data.Context;

public sealed class AiRecipeDbContext(DbContextOptions<AiRecipeDbContext> options) : DbContext(options)
{
    public DbSet<GenerationRequest> GenerationRequests => Set<GenerationRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ai_recipe");

        modelBuilder.Entity<GenerationRequest>(entity =>
        {
            entity.ToTable("GenerationRequests");
            entity.HasKey(request => request.Id);
            entity.HasIndex(request => new { request.OwnerId, request.IdempotencyKey }).IsUnique();
            entity.Property(request => request.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(request => request.Prompt).HasMaxLength(2_000).IsRequired();
            entity.Property(request => request.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(request => request.PantryItems).HasConversion(
                items => System.Text.Json.JsonSerializer.Serialize(items, (System.Text.Json.JsonSerializerOptions?)null),
                json => System.Text.Json.JsonSerializer.Deserialize<string[]>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? Array.Empty<string>());
            entity.Property(request => request.Draft).HasConversion(
                draft => System.Text.Json.JsonSerializer.Serialize(draft, (System.Text.Json.JsonSerializerOptions?)null),
                json => System.Text.Json.JsonSerializer.Deserialize<RecipeDraft>(json, (System.Text.Json.JsonSerializerOptions?)null));
            entity.Property(request => request.FailureCode).HasMaxLength(80);
            entity.Property(request => request.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(message => message.Id);
            entity.HasIndex(message => new { message.PublishedAtUtc, message.DeadLetteredAtUtc, message.NextAttemptAtUtc });
            entity.Property(message => message.EventType).HasMaxLength(160).IsRequired();
            entity.Property(message => message.RoutingKey).HasMaxLength(200).IsRequired();
            entity.Property(message => message.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(message => message.Payload).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(500);
        });
    }
}
