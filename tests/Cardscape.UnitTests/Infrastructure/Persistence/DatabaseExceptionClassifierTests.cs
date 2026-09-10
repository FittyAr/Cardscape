using Cardscape.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cardscape.UnitTests.Infrastructure.Persistence;

public sealed class DatabaseExceptionClassifierTests
{
    [Theory]
    [InlineData(19, 2067, true)]
    [InlineData(19, 787, false)]
    [InlineData(1, 1, false)]
    public void IsUniqueConstraintViolation_WithSqliteCodes_ReturnsExpectedResult(
        int errorCode,
        int extendedErrorCode,
        bool expected)
    {
        var providerException = new SqliteException("provider failure", errorCode, extendedErrorCode);
        var exception = new DbUpdateException("save failed", providerException);

        DatabaseExceptionClassifier.IsUniqueConstraintViolation(exception).Should().Be(expected);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, true)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, false)]
    public void IsUniqueConstraintViolation_WithPostgresState_ReturnsExpectedResult(
        string sqlState,
        bool expected)
    {
        var providerException = new PostgresException(
            "provider failure",
            "ERROR",
            "ERROR",
            sqlState);
        var exception = new DbUpdateException("save failed", providerException);

        DatabaseExceptionClassifier.IsUniqueConstraintViolation(exception).Should().Be(expected);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithUnknownExceptionContainingDuplicateText_ReturnsFalse()
    {
        var providerException = new InvalidOperationException("duplicate unique constraint");
        var exception = new DbUpdateException("save failed", providerException);

        DatabaseExceptionClassifier.IsUniqueConstraintViolation(exception).Should().BeFalse();
    }
}
