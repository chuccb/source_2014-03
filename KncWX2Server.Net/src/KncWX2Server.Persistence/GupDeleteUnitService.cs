using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KncWX2Server.Persistence;

/// <summary>SQLite implementation of the legacy dbo.gup_delete_unit procedure.</summary>
public sealed class GupDeleteUnitService
{
    private static readonly DateTime LegacySqlDateFallback =
        new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private const string LegacyDateFormat = "yyyy-MM-dd HH:mm:ss";

    public async ValueTask<int> DeleteAsync(
        SqliteConnection connection,
        long unitUid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var nickname = await LoadActiveUnitNicknameAsync(connection, unitUid, cancellationToken)
            .ConfigureAwait(false);
        if (nickname.Status != 0)
            return nickname.Status;

        var now = NormalizeSqlSmallDateTime(DateTime.Now.AddMinutes(1));
        var nowText = FormatDate(now);
        var itemCount = await CountActiveItemsAsync(connection, unitUid, cancellationToken)
            .ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    "UPDATE GUnit SET DelDate = $nowText WHERE UnitUID = $unitUid AND Deleted = 0;",
                    cancellationToken,
                    ("$nowText", nowText),
                    ("$unitUid", unitUid)).ConfigureAwait(false) != 1)
                return await RollbackAsync(transaction, -11, cancellationToken).ConfigureAwait(false);

            _ = await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE GTutor SET DelDate = $nowText WHERE (TeacherUID = $unitUid OR StudentUID = $unitUid) AND Deleted = 0;",
                cancellationToken,
                ("$nowText", nowText),
                ("$unitUid", unitUid)).ConfigureAwait(false);

            if (await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    "UPDATE GUnitNickName SET NickName = NULL WHERE UnitUID = $unitUid;",
                    cancellationToken,
                    ("$unitUid", unitUid)).ConfigureAwait(false) != 1)
                return await RollbackAsync(transaction, -12, cancellationToken).ConfigureAwait(false);

            if (await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    "INSERT INTO GDeletedNickNameHistory (NickName, UnitUID, RegDate) VALUES ($nickname, $unitUid, $nowText);",
                    cancellationToken,
                    ("$nickname", nickname.Value!),
                    ("$unitUid", unitUid),
                    ("$nowText", nowText)).ConfigureAwait(false) != 1)
                return await RollbackAsync(transaction, -14, cancellationToken).ConfigureAwait(false);

            if (await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    "UPDATE GItem SET DelDate = $nowText WHERE UnitUID = $unitUid AND Deleted = 0;",
                    cancellationToken,
                    ("$nowText", nowText),
                    ("$unitUid", unitUid)).ConfigureAwait(false) != itemCount)
                return await RollbackAsync(transaction, -15, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (SqliteException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return -11;
        }
    }

    private static async ValueTask<(int Status, string? Value)> LoadActiveUnitNicknameAsync(
        SqliteConnection connection,
        long unitUid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NickName
            FROM GUnitNickName
            WHERE UnitUID = $unitUid
              AND EXISTS
              (
                  SELECT 1
                  FROM GUnit
                  WHERE UnitUID = $unitUid
                    AND Deleted = 0
              )
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$unitUid", unitUid);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null || value is DBNull)
        {
            await using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT EXISTS(SELECT 1 FROM GUnit WHERE UnitUID = $unitUid AND Deleted = 0);";
            exists.Parameters.AddWithValue("$unitUid", unitUid);
            var active = await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return active is long { } activeValue && activeValue != 0 ? (-2, null) : (-1, null);
        }

        return (0, Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static async ValueTask<int> CountActiveItemsAsync(
        SqliteConnection connection,
        long unitUid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM GItem WHERE UnitUID = $unitUid AND Deleted = 0;";
        command.Parameters.AddWithValue("$unitUid", unitUid);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return checked(Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    private static async ValueTask<int> ExecuteNonQueryAsync(
        SqliteConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<int> RollbackAsync(
        DbTransaction transaction,
        int status,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return status;
    }

    private static DateTime NormalizeSqlSmallDateTime(DateTime value)
    {
        var truncated = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);
        return value.Second >= 30 ? truncated.AddMinutes(1) : truncated;
    }

    private static string FormatDate(DateTime value) =>
        value.ToString(LegacyDateFormat, CultureInfo.InvariantCulture);
}
