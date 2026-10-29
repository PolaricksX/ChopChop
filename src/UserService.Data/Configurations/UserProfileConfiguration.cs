using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain.Entities;
using UserService.Domain.Services;

namespace UserService.Data.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    private const string ActiveOnly = "[DeletedAt] IS NULL";

    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Subject).HasMaxLength(UserProfileRules.SubjectMaxLength).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(UserProfileRules.UsernameMaxLength).IsRequired();
        builder.Property(x => x.NormalizedUsername).HasMaxLength(UserProfileRules.UsernameMaxLength).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(UserProfileRules.EmailMaxLength).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(UserProfileRules.DisplayNameMaxLength);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.IsDeleted);

        // Soft-deleted rows are hidden from every query and free their unique values.
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName(IndexNames.UserEmail).HasFilter(ActiveOnly);
        builder.HasIndex(x => x.NormalizedUsername).IsUnique().HasDatabaseName(IndexNames.UserUsername).HasFilter(ActiveOnly);
        builder.HasIndex(x => x.Subject).IsUnique().HasDatabaseName(IndexNames.UserSubject).HasFilter(ActiveOnly);
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Subject).HasMaxLength(UserProfileRules.SubjectMaxLength).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.Subject, x.Key }).IsUnique().HasDatabaseName(IndexNames.IdempotencyKey);
    }
}
