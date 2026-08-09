using Cardscape.Domain.Common;

namespace Cardscape.Domain.Authentication.PasswordResets;

public readonly record struct PasswordResetId(Guid Value)
{
    public static PasswordResetId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
