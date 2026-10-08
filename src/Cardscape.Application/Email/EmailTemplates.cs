using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Email;

/// <summary>
/// The emails Cardscape sends, in the two UI languages (en, es). Every
/// value that comes from users (names, titles) is HTML-encoded in the HTML
/// body; the plain-text body carries the same content for clients that do
/// not render HTML.
/// </summary>
public static class EmailTemplates
{
    // Escapes markup characters but keeps accented letters readable.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>Languages with a translation; anything else falls back to <paramref name="fallback"/>.</summary>
    public static string ResolveLanguage(string? requested, string fallback) =>
        Normalize(requested) ?? Normalize(fallback) ?? "en";

    public static OutboundEmail WorkspaceInvitation(
        string to,
        string language,
        string instanceTitle,
        string inviterName,
        string workspaceName,
        WorkspaceRole role,
        string acceptUrl,
        DateTimeOffset expiresAt,
        string? boardName = null)
    {
        bool es = language == "es";
        CultureInfo culture = CultureInfo.GetCultureInfo(es ? "es" : "en");
        string roleName = RoleName(role, es);
        string expires = expiresAt.UtcDateTime.ToString("D", culture);

        string subject = es
            ? $"{inviterName} te invitó a «{workspaceName}» en {instanceTitle}"
            : $"{inviterName} invited you to \"{workspaceName}\" on {instanceTitle}";
        string intro = boardName is null
            ? es
                ? $"{inviterName} te invitó a unirte al espacio de trabajo «{workspaceName}» en {instanceTitle} como {roleName}."
                : $"{inviterName} invited you to join the workspace \"{workspaceName}\" on {instanceTitle} as {roleName}."
            : es
                ? $"{inviterName} te invitó al tablero «{boardName}» del espacio «{workspaceName}» en {instanceTitle} como {roleName}."
                : $"{inviterName} invited you to the board \"{boardName}\" in the workspace \"{workspaceName}\" on {instanceTitle} as {roleName}.";
        string action = es ? "Aceptar la invitación" : "Accept the invitation";
        string expiry = es
            ? $"La invitación vence el {expires}. Si no tenés cuenta, vas a poder crearla con esta dirección de correo."
            : $"The invitation expires on {expires}. If you don't have an account yet, you can create one with this email address.";
        string ignore = es
            ? "Si no esperabas esta invitación, podés ignorar este correo."
            : "If you weren't expecting this invitation, you can ignore this email.";

        return Compose(to, subject, instanceTitle, [intro], action, acceptUrl, [expiry, ignore]);
    }

    public static OutboundEmail PasswordReset(
        string to,
        string language,
        string instanceTitle,
        string resetUrl,
        TimeSpan lifetime)
    {
        bool es = language == "es";
        int hours = Math.Max(1, (int)Math.Round(lifetime.TotalHours));

        string subject = es
            ? $"Restablecé tu contraseña de {instanceTitle}"
            : $"Reset your {instanceTitle} password";
        string intro = es
            ? $"Recibimos un pedido para restablecer la contraseña de tu cuenta de {instanceTitle}."
            : $"We received a request to reset the password of your {instanceTitle} account.";
        string action = es ? "Elegir una contraseña nueva" : "Choose a new password";
        string expiry = es
            ? $"El enlace vence en {hours} {(hours == 1 ? "hora" : "horas")} y solo puede usarse una vez."
            : $"The link expires in {hours} {(hours == 1 ? "hour" : "hours")} and can only be used once.";
        string ignore = es
            ? "Si no lo pediste, ignorá este correo: tu contraseña no cambiará."
            : "If you didn't ask for this, ignore this email: your password won't change.";

        return Compose(to, subject, instanceTitle, [intro], action, resetUrl, [expiry, ignore]);
    }

    public static OutboundEmail EmailVerification(
        string to,
        string language,
        string instanceTitle,
        string displayName,
        string verifyUrl,
        TimeSpan lifetime)
    {
        bool es = language == "es";
        int hours = Math.Max(1, (int)Math.Round(lifetime.TotalHours));

        string subject = es
            ? $"Confirmá tu correo en {instanceTitle}"
            : $"Confirm your email on {instanceTitle}";
        string intro = es
            ? $"Hola {displayName}: confirmá que esta dirección es tuya para terminar de configurar tu cuenta de {instanceTitle}."
            : $"Hi {displayName}, confirm this address is yours to finish setting up your {instanceTitle} account.";
        string action = es ? "Confirmar mi correo" : "Confirm my email";
        string expiry = es
            ? $"El enlace vence en {hours} horas. Desde tu cuenta podés pedir uno nuevo."
            : $"The link expires in {hours} hours. You can request a new one from your account.";
        string ignore = es
            ? "Si no creaste una cuenta, ignorá este correo."
            : "If you didn't create an account, ignore this email.";

        return Compose(to, subject, instanceTitle, [intro], action, verifyUrl, [expiry, ignore]);
    }

    public static OutboundEmail AccountCreated(
        string to,
        string language,
        string instanceTitle,
        string displayName,
        string choosePasswordUrl,
        TimeSpan lifetime)
    {
        bool es = language == "es";
        int hours = Math.Max(1, (int)Math.Round(lifetime.TotalHours));

        string subject = es
            ? $"Tu cuenta de {instanceTitle} está lista"
            : $"Your {instanceTitle} account is ready";
        string intro = es
            ? $"Hola {displayName}: un administrador te creó una cuenta en {instanceTitle} con esta dirección de correo."
            : $"Hi {displayName}, an administrator created a {instanceTitle} account for you with this email address.";
        string action = es ? "Elegir mi contraseña" : "Choose my password";
        string expiry = es
            ? $"El enlace vence en {hours} horas. Si vence, pedí uno nuevo con «¿Olvidaste tu contraseña?»."
            : $"The link expires in {hours} hours. If it does, request a new one with \"Forgot your password?\".";

        return Compose(to, subject, instanceTitle, [intro], action, choosePasswordUrl, [expiry]);
    }

    public static OutboundEmail Test(string to, string language, string instanceTitle)
    {
        bool es = language == "es";
        string subject = es ? $"Correo de prueba de {instanceTitle}" : $"{instanceTitle} test email";
        string body = es
            ? $"Si estás leyendo esto, {instanceTitle} puede enviar correos con la configuración SMTP guardada."
            : $"If you are reading this, {instanceTitle} can send email with the saved SMTP settings.";
        return Compose(to, subject, instanceTitle, [body], action: null, url: null, []);
    }

    private static OutboundEmail Compose(
        string to,
        string subject,
        string instanceTitle,
        IReadOnlyList<string> before,
        string? action,
        string? url,
        IReadOnlyList<string> after)
    {
        StringBuilder text = new();
        foreach (string paragraph in before)
        {
            text.AppendLine(paragraph).AppendLine();
        }

        if (action is not null && url is not null)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{action}: {url}").AppendLine();
        }

        foreach (string paragraph in after)
        {
            text.AppendLine(paragraph).AppendLine();
        }

        text.Append("— ").Append(instanceTitle);

        StringBuilder html = new();
        html.Append("<!doctype html><html><body style=\"margin:0;padding:24px;background:#f4f5f7;")
            .Append("font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#172b4d;\">")
            .Append("<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:32px;\">")
            .Append("<p style=\"margin:0 0 24px;font-size:18px;font-weight:600;\">")
            .Append(Encode(instanceTitle))
            .Append("</p>");
        foreach (string paragraph in before)
        {
            html.Append("<p style=\"margin:0 0 16px;font-size:15px;line-height:1.5;\">").Append(Encode(paragraph)).Append("</p>");
        }

        if (action is not null && url is not null)
        {
            string href = Encode(url);
            html.Append("<p style=\"margin:24px 0;\"><a href=\"").Append(href)
                .Append("\" style=\"display:inline-block;padding:12px 20px;background:#0c66e4;color:#ffffff;")
                .Append("text-decoration:none;border-radius:6px;font-weight:600;\">")
                .Append(Encode(action))
                .Append("</a></p>")
                .Append("<p style=\"margin:0 0 16px;font-size:13px;line-height:1.5;color:#44546f;word-break:break-all;\">")
                .Append("<a href=\"").Append(href).Append("\" style=\"color:#0c66e4;\">").Append(href).Append("</a></p>");
        }

        foreach (string paragraph in after)
        {
            html.Append("<p style=\"margin:0 0 12px;font-size:13px;line-height:1.5;color:#44546f;\">").Append(Encode(paragraph)).Append("</p>");
        }

        html.Append("</div></body></html>");
        return new OutboundEmail(to, subject, text.ToString(), html.ToString());
    }

    private static string RoleName(WorkspaceRole role, bool es) => role switch
    {
        WorkspaceRole.Admin => es ? "administrador" : "an admin",
        WorkspaceRole.Observer => es ? "observador" : "an observer",
        WorkspaceRole.Guest => es ? "invitado" : "a guest",
        _ => es ? "miembro" : "a member",
    };

    private static string? Normalize(string? language)
    {
        string? twoLetter = language?.Trim().Split('-', '_')[0].ToLowerInvariant();
        return twoLetter is "en" or "es" ? twoLetter : null;
    }

    private static string Encode(string value) => Html.Encode(value);
}
