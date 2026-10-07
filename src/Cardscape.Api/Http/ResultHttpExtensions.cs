using Cardscape.Domain.Common;

namespace Cardscape.Api.Http;

/// <summary>
/// Turns application results into HTTP responses: the success shape the
/// endpoint asks for, or the RFC 7807 problem from
/// <see cref="DomainErrorResults"/> on failure.
/// </summary>
internal static class ResultHttpExtensions
{
    extension(Result result)
    {
        /// <summary>204 on success.</summary>
        public IResult ToNoContent() =>
            result.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(result.Error);
    }

    extension<T>(Result<T> result)
    {
        /// <summary>200 with the value on success.</summary>
        public IResult ToOk() =>
            result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);

        /// <summary>204 on success; the value is discarded.</summary>
        public IResult ToNoContent() =>
            result.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(result.Error);

        /// <summary>201 with the value and the location built from it on success.</summary>
        public IResult ToCreated(Func<T, string> location) =>
            result.IsSuccess
                ? Results.Created(location(result.Value), result.Value)
                : DomainErrorResults.ToProblem(result.Error);
    }
}
