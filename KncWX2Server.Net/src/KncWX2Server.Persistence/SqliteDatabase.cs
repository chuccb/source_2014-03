using Microsoft.Data.Sqlite;
using System.Data.Common;
using SQLitePCL;

namespace KncWX2Server.Persistence;

public sealed class SqliteDatabase(string databasePath) : IAsyncDisposable
{
    private static int s_nativeInitialized;
    private readonly string _databasePath = databasePath;

    public string DatabasePath => _databasePath;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureNativeSqlite();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_databasePath))!);

        await using var connection = await OpenAsync(cancellationToken);
        var version = await ScalarStringAsync(connection, "SELECT sqlite_version();", cancellationToken)
            ?? throw new InvalidOperationException("SQLite did not return a runtime version.");

        if (!Version.TryParse(version, out var parsed) || parsed.Major != 3 || parsed.Minor != 53)
            throw new NotSupportedException($"KncWX2Server requires SQLite 3.53.x, but the loaded native library reports {version}.");

        await ApplyMigrationsAsync(connection, cancellationToken);
    }

    public async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        EnsureNativeSqlite();

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(_databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        };

        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);

        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    private static void EnsureNativeSqlite()
    {
        if (Interlocked.Exchange(ref s_nativeInitialized, 1) == 0)
            Batteries_V2.Init();
    }

    private static async ValueTask ApplyMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var metadata = connection.CreateCommand())
        {
            metadata.CommandText = """
                CREATE TABLE IF NOT EXISTS __KncMigrations
                (
                    Version TEXT NOT NULL PRIMARY KEY,
                    AppliedAt TEXT NOT NULL
                );
                """;
            await metadata.ExecuteNonQueryAsync(cancellationToken);
        }

        var assembly = typeof(SqliteDatabase).Assembly;
        foreach (var migrationDefinition in MigrationManifest.All)
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT 1 FROM __KncMigrations WHERE Version = $version LIMIT 1;";
            check.Parameters.AddWithValue("$version", migrationDefinition.Version);

            if (await check.ExecuteScalarAsync(cancellationToken) is not null)
                continue;

            await using var stream = assembly.GetManifestResourceStream(migrationDefinition.ResourceName)
                ?? throw new InvalidOperationException($"Missing migration resource: {migrationDefinition.ResourceName}");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migration = connection.CreateCommand();
            migration.Transaction = transaction;
            migration.CommandText = sql;
            await migration.ExecuteNonQueryAsync(cancellationToken);

            await using var mark = connection.CreateCommand();
            mark.Transaction = transaction;
            mark.CommandText = "INSERT INTO __KncMigrations (Version, AppliedAt) VALUES ($version, $appliedAt);";
            mark.Parameters.AddWithValue("$version", migrationDefinition.Version);
            mark.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
            await mark.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async ValueTask<string?> ScalarStringAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
