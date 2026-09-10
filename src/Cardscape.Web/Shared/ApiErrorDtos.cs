namespace Cardscape.Web.Shared;

// ── Error envelopes ─────────────────────────────────────
// RFC 7807 ProblemDetails is the canonical API error contract.
// The optional code extension preserves stable machine-readable
// domain error identifiers for the client.
public sealed record ApiErrorDto(string? Title, string? Detail, int? Status);
public sealed record ApiErrorBody(string? Code, string? Message);
public sealed record ApiErrorEnvelope(ApiErrorBody? Error);
