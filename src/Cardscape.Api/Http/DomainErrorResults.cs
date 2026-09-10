using Cardscape.Domain.Common;

namespace Cardscape.Api.Http;

/// <summary>Maps application failures to one RFC 7807 representation.</summary>
internal static class DomainErrorResults
{
    internal static IResult ToProblem(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        (int status, string title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status422UnprocessableEntity, "Validation failed"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
            ErrorType.External => (StatusCodes.Status502BadGateway, "External dependency failed"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Unknown domain error type.")
        };

        return Results.Problem(
            detail: error.Message,
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code
            });
    }
}
