using Microsoft.Data.Sqlite;

namespace CashCoach.Infrastructure.Persistence;

public static class SqliteConnectionStrings
{
    /// <summary>
    /// Anchors a relative SQLite file path to <paramref name="baseDirectory"/> so the database location
    /// does not depend on the process working directory. In-memory and absolute paths are left unchanged.
    /// </summary>
    public static string Resolve(string connectionString, string baseDirectory)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        var isInMemoryOrUri = builder.Mode == SqliteOpenMode.Memory
            || dataSource == ":memory:"
            || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

        if (isInMemoryOrUri || string.IsNullOrEmpty(dataSource) || Path.IsPathRooted(dataSource))
        {
            return connectionString;
        }

        builder.DataSource = Path.GetFullPath(Path.Combine(baseDirectory, dataSource));
        return builder.ToString();
    }
}
