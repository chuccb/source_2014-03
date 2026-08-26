using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KncWX2Server.Persistence;

/// <summary>
/// SQLite implementation of the legacy dbo.gup_create_unit procedure.
/// The source procedure performs the whole character creation inside one SQL transaction.
/// </summary>
public sealed class GupCreateUnitService
{
    private static readonly DateTime LegacySqlDateFallback =
        new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private const string LegacyDateFormat = "yyyy-MM-dd HH:mm:ss";

    public async ValueTask<GupCreateUnitResult> CreateAsync(
        SqliteConnection connection,
        long userUid,
        string nickname,
        byte unitClass,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);

        var now = DateTime.Now;
        var nowText = FormatDate(now);

        var startSpirit = await ScalarNullableInt64Async(
            connection,
            "SELECT StartSpirit FROM GResurrectionStoneCnt LIMIT 1;",
            cancellationToken).ConfigureAwait(false);

        if (startSpirit is null)
            return new(-21, 0, null);

        var user = await LoadUserAsync(connection, userUid, cancellationToken).ConfigureAwait(false);
        if (user is { Deleted: true })
            return new(-1, 0, null);

        if (user is { UnitSlotSize: { } unitSlotSize })
        {
            var activeUnitCount = await ScalarInt64Async(
                connection,
                "SELECT COUNT(*) FROM GUnit WHERE Deleted = 0 AND UserUID = $userUid;",
                cancellationToken,
                ("$userUid", userUid)).ConfigureAwait(false);

            if (activeUnitCount >= unitSlotSize)
                return new(-3, 0, null);
        }

        var nicknameAlreadyExists = await ScalarInt64Async(
            connection,
            "SELECT EXISTS(SELECT 1 FROM GUnitNickName WHERE NickName = $nickname);",
            cancellationToken,
            ("$nickname", nickname)).ConfigureAwait(false);

        if (nicknameAlreadyExists != 0)
            return new(-2, 0, null);

        var legacyNicknameDate = await FindLatestDeletedNicknameDateAsync(
            connection,
            nickname,
            cancellationToken).ConfigureAwait(false);

        if (legacyNicknameDate is { } deletedNicknameDate &&
            deletedNicknameDate > now.AddDays(-14))
        {
            return new(-222, 0, deletedNicknameDate.AddDays(14));
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var unitUid = await InsertUnitAsync(
                connection,
                transaction,
                userUid,
                unitClass,
                nowText,
                cancellationToken).ConfigureAwait(false);

            if (unitUid <= 0)
                return await RollbackAsync(transaction, new(-12, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);

            if (!await InsertUnitNicknameAsync(
                    connection,
                    transaction,
                    unitUid,
                    nickname,
                    nowText,
                    cancellationToken).ConfigureAwait(false))
                return await RollbackAsync(transaction, new(-13, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);

            for (var questionNo = 1; questionNo <= 4; questionNo++)
            {
                if (!await InsertDenyOptionAsync(
                        connection,
                        transaction,
                        unitUid,
                        questionNo,
                        cancellationToken).ConfigureAwait(false))
                    return await RollbackAsync(transaction, new(-14, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);
            }

            if (!await InsertInitialQuestAsync(
                    connection,
                    transaction,
                    unitUid,
                    nowText,
                    cancellationToken).ConfigureAwait(false))
                return await RollbackAsync(transaction, new(-14, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);

            var skillSetup = GetInitialSkill(unitClass);
            if (skillSetup is { } initialSkill)
            {
                if (!await InsertSkillAsync(
                        connection,
                        transaction,
                        unitUid,
                        initialSkill.SkillId,
                        nowText,
                        cancellationToken).ConfigureAwait(false))
                    return await RollbackAsync(transaction, new(initialSkill.SkillErrorCode, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);

                if (!await InsertSkillSlotAsync(
                        connection,
                        transaction,
                        unitUid,
                        initialSkill.SkillId,
                        cancellationToken).ConfigureAwait(false))
                    return await RollbackAsync(transaction, new(initialSkill.SkillSlotErrorCode, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);
            }

            if (!await InsertSpiritAsync(
                    connection,
                    transaction,
                    unitUid,
                    checked((short)startSpirit.Value),
                    nowText,
                    cancellationToken).ConfigureAwait(false))
                return await RollbackAsync(transaction, new(-21, 0, legacyNicknameDate), cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(0, unitUid, legacyNicknameDate ?? LegacySqlDateFallback);
        }
        catch (SqliteException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(-12, 0, legacyNicknameDate);
        }
    }

    private static (int SkillId, int SkillErrorCode, int SkillSlotErrorCode)? GetInitialSkill(byte unitClass) =>
        unitClass switch
        {
            1 => (10_000, -16, -15),
            2 => (20_030, -18, -17),
            3 => (30_000, -20, -19),
            4 => (40_010, -20, -19),
            _ => null,
        };

    private static async ValueTask<long> InsertUnitAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long userUid,
        byte unitClass,
        string nowText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO GUnit
            (
                UserUID, UnitClass, Exp, Level, GamePoint, VSPoint, VSPointMax,
                BaseHP, AtkPhysic, AtkMagic, DefPhysic, DefMagic, SPoint,
                Win, Lose, Seceder, RegDate, DelDate, LastDate,
                LastPosition, PlayDayCnt, LoginCount
            )
            VALUES
            (
                $userUid, $unitClass, 0, 1, 0, 0, 0,
                0, 0, 0, 0, 0, 1,
                0, 0, 0, $nowText, $nowText, $legacyZeroDate,
                20_000, 0, 1
            )
            RETURNING UnitUID;
            """;
        command.Parameters.AddWithValue("$userUid", userUid);
        command.Parameters.AddWithValue("$unitClass", unitClass);
        command.Parameters.AddWithValue("$nowText", nowText);
        command.Parameters.AddWithValue("$legacyZeroDate", "1900-01-01 00:00:00");

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is long generatedUnitUid
            ? generatedUnitUid
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async ValueTask<bool> InsertUnitNicknameAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long unitUid,
        string nickname,
        string nowText,
        CancellationToken cancellationToken)
    {
        return await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO GUnitNickName (UnitUID, NickName, RegDate) VALUES ($unitUid, $nickname, $nowText);",
            cancellationToken,
            ("$unitUid", unitUid),
            ("$nickname", nickname),
            ("$nowText", nowText)).ConfigureAwait(false) == 1;
    }

    private static async ValueTask<bool> InsertDenyOptionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long unitUid,
        int questionNo,
        CancellationToken cancellationToken)
    {
        return await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO GDenyOption (UnitUID, QuestionNo, CodeNo) VALUES ($unitUid, $questionNo, 1);",
            cancellationToken,
            ("$unitUid", unitUid),
            ("$questionNo", questionNo)).ConfigureAwait(false) == 1;
    }

    private static async ValueTask<bool> InsertInitialQuestAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long unitUid,
        string nowText,
        CancellationToken cancellationToken)
    {
        return await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO GQuests (UnitUID, QuestID, SubQuest0, SubQuest1, SubQuest2, SubQuest3, SubQuest4, RegDate) VALUES ($unitUid, 13, 1, 0, 0, 0, 0, $nowText);",
            cancellationToken,
            ("$unitUid", unitUid),
            ("$nowText", nowText)).ConfigureAwait(false) == 1;
    }

    private static async ValueTask<bool> InsertSkillAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long unitUid,
        int skillId,
        string nowText,
        CancellationToken cancellationToken)
    {
        return await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO GSkill (UnitUID, SkillID, RegDate) VALUES ($unitUid, $skillId, $nowText);",
            cancellationToken,
            ("$unitUid", unitUid),
            ("$skillId", skillId),
            ("$nowText", nowText)).ConfigureAwait(false) == 1;
    }

    private static async ValueTask<bool> InsertSkillSlotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long unitUid,
        int skillId,
        CancellationToken cancellationToken)
    {
        return await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO GSkillSlot (UnitUID, Slot01, Slot02, Slot03) VALUES ($unitUid, $skillId, 0, 0);",
            cancellationToken,
            ("$unitUid", unitUid),
            ("$skillId", skillId)).ConfigureAwait(false) == 1;
    }

    private static async ValueTask<bool> InsertSpiritAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long unitUid,
        short spirit,
        string nowText,
        CancellationToken cancellationToken)
    {
        return await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO GSpirit (unitUID, Spirit, RegDate, Flag) VALUES ($unitUid, $spirit, $nowText, 0);",
            cancellationToken,
            ("$unitUid", unitUid),
            ("$spirit", spirit),
            ("$nowText", nowText)).ConfigureAwait(false) == 1;
    }

    private static async ValueTask<(bool Deleted, long? UnitSlotSize)?> LoadUserAsync(
        SqliteConnection connection,
        long userUid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Deleted, USSize FROM GUser WHERE UserUID = $userUid LIMIT 1;";
        command.Parameters.AddWithValue("$userUid", userUid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return new(false, null);

        var deleted = reader.GetInt64(0) != 0;
        var slotSize = reader.GetInt64(1);
        return (deleted, slotSize);
    }

    private static async ValueTask<DateTime?> FindLatestDeletedNicknameDateAsync(
        SqliteConnection connection,
        string nickname,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RegDate FROM GDeletedNickNameHistory WHERE NickName = $nickname ORDER BY RegDate DESC LIMIT 1;";
        command.Parameters.AddWithValue("$nickname", nickname);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null || value is DBNull)
            return null;

        return DateTime.TryParseExact(
            value.ToString(),
            [LegacyDateFormat, "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static async ValueTask<long> ScalarInt64Async(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var value = await ScalarNullableInt64Async(connection, sql, cancellationToken, parameters)
            .ConfigureAwait(false);
        return value ?? 0;
    }

    private static async ValueTask<long?> ScalarNullableInt64Async(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
            return null;

        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
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

    private static async ValueTask<GupCreateUnitResult> RollbackAsync(
        SqliteTransaction transaction,
        GupCreateUnitResult result,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static string FormatDate(DateTime value) =>
        value.ToString(LegacyDateFormat, CultureInfo.InvariantCulture);
}

public readonly record struct GupCreateUnitResult(
    int ProcedureStatus,
    long UnitUid,
    DateTime? NicknameReenableAt)
{
    public bool Succeeded => ProcedureStatus == 0 && UnitUid > 0;
}
