using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cardscape.Infrastructure.Persistence.Conventions;

/// <summary>
/// Normalises every <see cref="DateTimeOffset"/> to UTC on its way to the
/// database. Npgsql rejects non-zero offsets for <c>timestamptz</c> and the
/// other providers compare offset-carrying values inconsistently, so a due
/// date posted from a browser at -03:00 must be stored (and compared) as UTC.
/// EF Core also applies the converter to query parameters compared against
/// these columns.
/// </summary>
internal sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    static value => value.ToUniversalTime(),
    static value => value.ToUniversalTime());
