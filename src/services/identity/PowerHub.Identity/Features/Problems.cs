using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace PowerHub.Identity.Features;

/// <summary>RFC 9457 responses with stable <c>type</c> URIs that clients can branch on.</summary>
public static class Problems
{
    private const string Base = "https://docs.powerhub.example/problems/";

    public static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem(statusCode: 401, title: "Invalid email or password.", type: Base + "invalid-credentials");

    public static ProblemHttpResult InvalidSession() =>
        TypedResults.Problem(statusCode: 401, title: "The session is no longer valid.", type: Base + "invalid-session");

    public static ProblemHttpResult AccountDisabled() =>
        TypedResults.Problem(statusCode: 403, title: "This account is disabled.", type: Base + "account-disabled");

    public static ProblemHttpResult EmailNotConfirmed() =>
        TypedResults.Problem(statusCode: 403, title: "Confirm your email address before signing in.", type: Base + "email-not-confirmed");

    public static ProblemHttpResult InvalidToken() =>
        TypedResults.Problem(statusCode: 400, title: "The link is invalid or has expired.", type: Base + "invalid-token");

    public static ProblemHttpResult MissingCsrfHeader() =>
        TypedResults.Problem(statusCode: 400, title: "A required request header is missing.", type: Base + "csrf");

    public static ProblemHttpResult NotFound() =>
        TypedResults.Problem(statusCode: 404, title: "The resource was not found.", type: Base + "not-found");

    public static ProblemHttpResult Conflict(string title) =>
        TypedResults.Problem(statusCode: 409, title: title, type: Base + "conflict");

    public static ValidationProblem Validation(string field, IEnumerable<string> messages) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]> { [field] = [.. messages] },
            type: Base + "validation");

    public static ValidationProblem Validation(string field, IdentityResult result) =>
        Validation(field, result.Errors.Select(error => error.Description));
}
