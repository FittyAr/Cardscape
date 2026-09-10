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

    [Theory]
    [InlineData("bad-request", StatusCodes.Status400BadRequest, "Bad request")]
    [InlineData("not-found", StatusCodes.Status404NotFound, "Resource not found")]
    [InlineData("conflict", StatusCodes.Status409Conflict, "Conflict")]
    public void ApiProblemResult_KnownTransportFailure_ReturnsCanonicalProblemDetails(
        string kind,
        int expectedStatus,
        string expectedTitle)
    {
        IResult result = kind switch
        {
            "bad-request" => ApiProblemResults.BadRequest("request.invalid", "Invalid request."),
            "not-found" => ApiProblemResults.NotFound("resource.missing", "Resource is missing."),
            "conflict" => ApiProblemResults.Conflict("resource.conflict", "Resource conflicts."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        ProblemHttpResult problem = result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.ProblemDetails.Status.Should().Be(expectedStatus);
        problem.ProblemDetails.Title.Should().Be(expectedTitle);
        problem.ProblemDetails.Detail.Should().NotBeNullOrWhiteSpace();
        problem.ProblemDetails.Extensions.Should().ContainSingle();
        problem.ProblemDetails.Extensions.Should().ContainKey("code");
        problem.ProblemDetails.Extensions["code"].Should()
            .BeOfType<string>().Which.Should().NotBeNullOrWhiteSpace();
    }
}
