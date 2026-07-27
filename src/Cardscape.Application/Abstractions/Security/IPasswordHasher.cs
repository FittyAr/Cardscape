using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Hashes and verifies user passwords. The implementation
/// encapsulates the algorithm and the parameters; the domain
/// only sees a <see cref="PasswordHash"/> value object.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password.</summary>
    PasswordHash Hash(string plaintext);

    /// <summary>Verifies a plaintext password against a stored hash.</summary>
    bool Verify(string plaintext, PasswordHash hash);
}
