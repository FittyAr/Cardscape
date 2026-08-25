using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Saml;

public sealed record SamlConnectionDto(
    Guid Id,
    Guid WorkspaceId,
    string Slug,
    string DisplayName,
    string IdpEntityId,
    string IdpMetadataUrl,
    string? IdpMetadataXml,
    string SpEntityId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);


