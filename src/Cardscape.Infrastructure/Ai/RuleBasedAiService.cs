using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;

namespace Cardscape.Infrastructure.Ai;

/// <summary>
/// No-network, no-config AI provider. The default when
/// <c>Ai:Provider</c> is missing or set to <c>RuleBased</c>.
///
/// The "AI" is a small set of deterministic templates that
/// produce a useful-enough response for each Cardscape use
/// case. The point is not to be clever — it is to make the
/// "✨ generate" buttons in the UI *do something* even when
/// no OpenAI-compatible endpoint is configured.
///
/// The templates are deliberately boring and the output is
/// deterministic, so a test asserting on the output of the
/// rule-based provider is stable.
/// </summary>
public sealed class RuleBasedAiService : IAiService
{
    public Task<Result<AiTextCompletion>> CompleteAsync(AiPrompt prompt, AiOptions options, CancellationToken ct = default)
    {
        string text = RenderTemplate(prompt);
        return Task.FromResult(Result<AiTextCompletion>.Success(
            new AiTextCompletion(text, Model: "rule-based", PromptTokens: null, CompletionTokens: null)));
    }

    private static string RenderTemplate(AiPrompt prompt)
    {
        string user = prompt.User.Trim();
        string system = prompt.System.Trim();

        if (system.StartsWith("describe-card", StringComparison.OrdinalIgnoreCase))
        {
            return $"This card covers: {user}\n\nNext step: pick the smallest deliverable that moves the work forward and write a one-line acceptance check.";
        }
        if (system.StartsWith("summarize-thread", StringComparison.OrdinalIgnoreCase))
        {
            string[] lines = user.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
            {
                return "No comments to summarize.";
            }
            StringBuilder sb = new();
            sb.AppendLine("Summary:");
            for (int i = 0; i < Math.Min(lines.Length, 5); i++)
            {
                sb.Append("- ");
                sb.AppendLine(lines[i]);
            }
            return sb.ToString();
        }
        if (system.StartsWith("make-checklist", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join("\n", new[]
            {
                "Scope the work",
                "Identify the smallest deliverable",
                "Implement and self-review",
                "Cover with a test",
                "Open the PR / mark done"
            });
        }
        if (system.StartsWith("suggest-owners", StringComparison.OrdinalIgnoreCase))
        {
            // The caller is expected to list candidate user ids and the rule
            // picks the first one as a stand-in. The OpenAI-compatible
            // provider does this with a real model.
            return "Pick the first candidate. The rule-based engine does not have access to history.";
        }
        return user.Length > 0 ? user : "Cardscape rule-based AI: configure an OpenAI-compatible provider for richer answers.";
    }
}
