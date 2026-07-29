using Cardscape.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace Cardscape.Infrastructure.Authentication;

/// <summary>
/// <see cref="ISecretProtector"/> backed by ASP.NET Core's
/// <see cref="IDataProtector"/>. The protected payload is
/// key-rotation-friendly: the data-protection ring
/// transparently handles the "current key + previous key"
/// scenarios so a re-key does not invalidate stored secrets.
/// </summary>
public sealed class DataProtectionSecretProtector(IDataProtector protector)
    : ISecretProtector
{
    public string Protect(string plaintext) =>
        protector.Protect(plaintext);

    public string Unprotect(string protectedValue) =>
        protector.Unprotect(protectedValue);
}
