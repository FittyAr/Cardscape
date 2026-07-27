using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs every FluentValidation
/// validator registered for the request type. On validation
/// failure, throws a <see cref="ValidationException"/> instead
/// of invoking the handler.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, ct))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        logger.LogWarning(
            "Validation failed for {RequestType}: {ErrorCount} error(s)",
            typeof(TRequest).Name,
            failures.Count);

        throw new ValidationException(failures);
    }
}
