using InteractiveReview.Domain;

namespace InteractiveReview.UnitTests;

public sealed class ReviewTests
{
    [Fact]
    public void Create_StoresReviewAsPending()
    {
        var review = CreateReview();

        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Nice recipe", review.Body);
    }

    [Fact]
    public void Update_RequiresOwnerAndResetsModeration()
    {
        var review = CreateReview();
        review.Moderate(Guid.NewGuid(), ReviewStatus.Published, "Approved", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            review.Update(Guid.NewGuid(), 3, "Updated text", null, DateTimeOffset.UtcNow));

        review.Update(review.OwnerId, 3, "Updated text", "Reduce salt", DateTimeOffset.UtcNow);
        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.Null(review.ModeratedAtUtc);
        Assert.Equal("Reduce salt", review.Suggestion);
    }

    [Fact]
    public void Moderate_OnlyAllowsPendingToPublishedOrRejectedAndPublishedToHidden()
    {
        var review = CreateReview();
        var moderatorId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            review.Moderate(moderatorId, ReviewStatus.Hidden, "Hide it", DateTimeOffset.UtcNow));

        review.Moderate(moderatorId, ReviewStatus.Published, "Approved", DateTimeOffset.UtcNow);
        review.Moderate(moderatorId, ReviewStatus.Hidden, "Policy violation", DateTimeOffset.UtcNow);

        Assert.Equal(ReviewStatus.Hidden, review.Status);
    }

    [Fact]
    public void Create_RejectsOutOfRangeRatings()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Review.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                6,
                "Text",
                null,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DomainAssembly_DoesNotDependOnDataOrWebFrameworks()
    {
        var references = typeof(Review).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("InteractiveReview.Data", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
    }

    private static Review CreateReview() =>
        Review.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            "Nice recipe",
            null,
            DateTimeOffset.UtcNow);
}
