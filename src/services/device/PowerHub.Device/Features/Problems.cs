using Microsoft.AspNetCore.Http.HttpResults;

namespace PowerHub.Device.Features;

/// <summary>RFC 9457 responses with stable <c>type</c> URIs that clients can branch on.</summary>
public static class Problems
{
    private const string Base = "https://docs.powerhub.example/problems/";

    /// <summary>
    /// Also returned when the device exists but the caller has no access to it, so the
    /// response never confirms that someone else's device exists.
    /// </summary>
    public static ProblemHttpResult NotFound() =>
        TypedResults.Problem(statusCode: 404, title: "The device was not found.", type: Base + "not-found");

    public static ProblemHttpResult Forbidden() =>
        TypedResults.Problem(statusCode: 403, title: "Your access to this device does not allow that action.", type: Base + "forbidden");

    public static ProblemHttpResult PreconditionRequired() =>
        TypedResults.Problem(statusCode: 428, title: "Send the current ETag in If-Match.", type: Base + "precondition-required");

    public static ProblemHttpResult PreconditionFailed() =>
        TypedResults.Problem(statusCode: 412, title: "The device was changed by someone else. Reload and try again.", type: Base + "precondition-failed");
}
