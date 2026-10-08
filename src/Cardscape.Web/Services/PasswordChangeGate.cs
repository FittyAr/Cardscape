namespace Cardscape.Web.Services;

/// <summary>
/// Whether the signed-in account still has a temporary password set by an
/// administrator. Raised by the sign-in response and by any API call the
/// server refuses with the password-change header; MainLayout listens and
/// opens the change-password page.
/// </summary>
public sealed class PasswordChangeGate
{
    /// <summary>Header the API puts on its "change your password first" 403.</summary>
    public const string HeaderName = "X-Cardscape-Password-Change";

    public bool Required { get; private set; }

    public event Action? Changed;

    public void Require()
    {
        if (Required)
        {
            return;
        }

        Required = true;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (!Required)
        {
            return;
        }

        Required = false;
        Changed?.Invoke();
    }
}
