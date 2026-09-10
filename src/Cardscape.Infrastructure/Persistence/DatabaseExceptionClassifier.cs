using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Cardscape.Infrastructure.Persistence;

internal static class DatabaseExceptionClassifier
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException switch
        {
            SqliteException sqlite =>
                sqlite.SqliteErrorCode == 19 && sqlite.SqliteExtendedErrorCode == 2067,
            PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation,
            MySqlException mysql => mysql.Number == 1062,
            _ => false
        };
    }
}
