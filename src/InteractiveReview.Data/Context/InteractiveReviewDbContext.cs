using InteractiveReview.Domain;
using Microsoft.EntityFrameworkCore;

namespace InteractiveReview.Data.Context;

public sealed class InteractiveReviewDbContext(DbContextOptions<InteractiveReviewDbContext> options)
    : DbContext(options)
{
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("interactive_review");

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Reviews");
            entity.HasKey(review => review.Id);
            entity.HasIndex(review => new { review.RecipeId, review.OwnerId }).IsUnique();
            entity.HasIndex(review => new { review.RecipeId, review.Status, review.CreatedAtUtc });
            entity.Property(review => review.Body).HasMaxLength(2_000).IsRequired();
            entity.Property(review => review.Suggestion).HasMaxLength(1_000);
            entity.Property(review => review.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(review => review.ModerationReason).HasMaxLength(500);
            entity.Property(review => review.Version).IsConcurrencyToken();
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
