using Riok.Mapperly.Abstractions;

namespace Cardscape.Application.Boards.Mapping;

/// <summary>
/// Mapping contracts for board-related entities. Implemented by
/// Mapperly's source generator (no reflection at runtime). Add new
/// partial mapping methods here when DTO shapes diverge from the
/// entity; the rest is auto-generated.
///
/// We picked Mapperly over AutoMapper (and any other Jimmy Bogard
/// library) for its compile-time, AOT-friendly, zero-allocation
/// code path. The source generator emits a plain method per
/// mapping; there's no runtime reflection and no startup cost.
/// </summary>
[Mapper]
public partial class BoardMappers
{
}
