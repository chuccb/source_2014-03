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

        if (!await ExistsActiveUnitAsync(connection, unitUid, cancellationToken).ConfigureAwait(false))
            return -1;

        var nickname = await LoadUnitNicknameAsync(connection, unitUid, cancellationToken).ConfigureAwait(false);
        if (nickname is null)
            return -2;

        var now = NormalizeSqlSmallDateTime(DateTime.Now.AddMinutes(1));
        var nowText = FormatDate(now);
        var itemCount = await CountActiveItemsAsync(connection, unitUid, cancellationToken).ConfigureAwait(false);

        await using SqliteTransaction transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (!await ExecuteExpectedAsync(
                connection,
                transaction,
                "UPDATE GUnit SET DelDate = $nowText WHERE UnitUID = $unitUid AND Deleted = 0;",
                expected: 1,
                cancellationToken,
                ("$nowText", nowText),
                ("$unitUid", unitUid)).ConfigureAwait(false))
            return await RollbackAsync(transaction, -11, cancellationToken).ConfigureAwait(false);

        _ = await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE GTutor SET DelDate = $nowText WHERE (TeacherUID = $unitUid OR StudentUID = $unitUid) AND Deleted = 0;",
            cancellationToken,
            ("$nowText", nowText),
            ("$unitUid", unitUid)).ConfigureAwait(false);

        if (!await ExecuteExpectedAsync(
                connection,
                transaction,
                "UPDATE GUnitNickName SET NickName = NULL WHERE UnitUID = $unitUid;",
                expected: 1,
                cancellationToken,
                ("$unitUid", unitUid)).ConfigureAwait(false))
            return await RollbackAsync(transaction, -12, cancellationToken).ConfigureAwait(false);

        if (!await ExecuteExpectedAsync(
                connection,
                transaction,
                "INSERT INTO GDeletedNickNameHistory (NickName, UnitUID, RegDate) VALUES ($nickname, $unitUid, $nowText);",
                expected: 1,
                cancellationToken,
                ("$nickname", nickname),
                ("$unitUid", unitUid),
                ("$nowText", nowText)).ConfigureAwait(false))
            return await RollbackAsync(transaction, -14, cancellationToken).ConfigureAwait(false);

        if (!await ExecuteExpectedAsync(
                connection,
                transaction,
                "UPDATE GItem SET DelDate = $nowText WHERE UnitUID = $unitUid AND Deleted = 0;",
                expected: itemCount,
                cancellationToken,
                ("$nowText", nowText),
                ("$unitUid", unitUid)).ConfigureAwait(false))
            return await RollbackAsync(transaction, -15, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static async ValueTask<bool> ExistsActiveUnitAsync(
        SqliteConnection connection,
        long unitUid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM GUnit WHERE UnitUID = $unitUid AND Deleted = 0);";
        command.Parameters.AddWithValue("$unitUid", unitUid);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
    }

    private static async ValueTask<string?> LoadUnitNicknameAsync(
        SqliteConnection connection,
        long unitUid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT NickName FROM GUnitNickName WHERE UnitUID = $unitUid LIMIT 1;";
        command.Parameters.AddWithValue("$unitUid", unitUid);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);
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

    private static async ValueTask<bool> ExecuteExpectedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        int expected,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            return await ExecuteNonQueryAsync(
                connection,
                transaction,
                sql,
                cancellationToken,
                parameters).ConfigureAwait(false) == expected;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static async ValueTask<int> ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
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
        SqliteTransaction transaction,
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
