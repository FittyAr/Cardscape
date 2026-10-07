namespace Cardscape.Application.Common.Exceptions;

/// <summary>Thrown when a command or query requires authentication but the caller is anonymous.</summary>
public sealed class UnauthorizedException(string message) : Exception(message)
{
}

/// <summary>Thrown when a command or query requires a permission the caller does not have.</summary>
public sealed class ForbiddenException(string message) : Exception(message)
{
}
