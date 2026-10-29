using Microsoft.AspNetCore.Mvc;
using UserService.Api.Contracts;
using UserService.Api.Infrastructure;
using UserService.Api.Services;

namespace UserService.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(Policies.UsersWrite)
            .WithName("CreateUser")
            .WithSummary("Creates the caller's profile. Requires an Idempotency-Key header and the users.write scope.")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(Policies.UsersRead)
            .WithName("GetUser")
            .WithSummary("Gets the caller's profile. Requires the users.read scope; other users' profiles return 404.")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(Policies.UsersWrite)
            .WithName("UpdateUser")
            .WithSummary("Replaces the caller's profile fields. The body's version must match the current version, otherwise 409.")
            .Produces<UserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(Policies.UsersWrite)
            .WithName("DeleteUser")
            .WithSummary("Soft-deletes the caller's profile. Requires the users.write scope.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateUserRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentUser user,
        UserProfileService users,
        CancellationToken token)
    {
        var errors = UserRequestValidator.Validate(request, idempotencyKey);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var profile = await users.CreateAsync(user.Id, idempotencyKey!, request!, token);
        return Results.Created($"/api/v1/users/{profile.Id}", profile.ToResponse());
    }

    private static async Task<IResult> GetAsync(Guid id, ICurrentUser user, UserProfileService users, CancellationToken token) =>
        Results.Ok((await users.GetAsync(user.Id, id, token)).ToResponse());

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateUserRequest? request,
        ICurrentUser user,
        UserProfileService users,
        CancellationToken token)
    {
        var errors = UserRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return Results.Ok((await users.UpdateAsync(user.Id, id, request!, token)).ToResponse());
    }

    private static async Task<IResult> DeleteAsync(Guid id, ICurrentUser user, UserProfileService users, CancellationToken token)
    {
        await users.DeleteAsync(user.Id, id, token);
        return Results.NoContent();
    }
}
