namespace Cardscape.Api.Http;

/// <summary>Creates canonical RFC 7807 responses for transport-level failures.</summary>
internal static class ApiProblemResults
{
    internal static IResult BadRequest(string code, string detail) =>
        Create(StatusCodes.Status400BadRequest, "Bad request", code, detail);

    internal static IResult NotFound(string code, string detail) =>
        Create(StatusCodes.Status404NotFound, "Resource not found", code, detail);

    internal static IResult Conflict(string code, string detail) =>
        Create(StatusCodes.Status409Conflict, "Conflict", code, detail);

    internal static IResult PayloadTooLarge(string code, string detail) =>
        Create(StatusCodes.Status413PayloadTooLarge, "Payload too large", code, detail);

    internal static IResult ServiceUnavailable(string code, string detail) =>
        Create(StatusCodes.Status503ServiceUnavailable, "Service unavailable", code, detail);

    private static IResult Create(int status, string title, string code, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        return Results.Problem(
            detail: detail,
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code
            });
    }
}
