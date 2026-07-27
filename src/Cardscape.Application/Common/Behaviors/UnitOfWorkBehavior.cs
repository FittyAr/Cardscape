using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using MediatR;

namespace Cardscape.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps mutating commands in a
/// unit-of-work. Queries are not affected.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var isCommand = typeof(TRequest).GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)
                      && !i.GetGenericArguments()[0].Name.StartsWith("IReadOnly", StringComparison.Ordinal));

        if (!isCommand)
        {
            return await next();
        }

        var response = await next();

        if (response is Result { IsSuccess: true } ||
            (response?.GetType().IsGenericType == true &&
             response.GetType().GetGenericTypeDefinition() == typeof(Result<>) &&
             (bool)response.GetType().GetProperty("IsSuccess")!.GetValue(response)!))
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        return response;
    }
}
