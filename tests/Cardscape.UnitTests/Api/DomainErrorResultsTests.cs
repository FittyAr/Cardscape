using Cardscape.Api.Http;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cardscape.UnitTests.Api;

public sealed class DomainErrorResultsTests
{
    public static TheoryData<ErrorType, int, string> ErrorMappings => new()
    {
        { ErrorType.Validation, StatusCodes.Status422UnprocessableEntity, "Validation failed" },
        { ErrorType.NotFound, StatusCodes.Status404NotFound, "Resource not found" },
        { ErrorType.Conflict, StatusCodes.Status409Conflict, "Conflict" },
        { ErrorType.Forbidden, StatusCodes.Status403Forbidden, "Forbidden" },
        { ErrorType.Unauthenticated, StatusCodes.Status401Unauthorized, "Authentication required" },
        { ErrorType.External, StatusCodes.Status502BadGateway, "External dependency failed" }
    };

    [Theory]
    [MemberData(nameof(ErrorMappings))]
    public void ToProblem_KnownErrorType_ReturnsCanonicalProblemDetails(
        ErrorType errorType,
        int expectedStatus,
        string expectedTitle)
    {
        var error = new DomainError(errorType, "resource.failure", "Safe failure detail.");

        IResult result = DomainErrorResults.ToProblem(error);

        ProblemHttpResult problem = result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.ProblemDetails.Status.Should().Be(expectedStatus);
        problem.ProblemDetails.Title.Should().Be(expectedTitle);
        problem.ProblemDetails.Detail.Should().Be("Safe failure detail.");
        problem.ProblemDetails.Extensions.Should().ContainSingle("code")
            .Which.Value.Should().Be("resource.failure");
    }
}
