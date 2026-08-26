using KncWX2Server.Core;
using Microsoft.Data.Sqlite;

namespace KncWX2Server.Persistence;

public sealed class UserRepository(SqliteDatabase database)
{
    public async ValueTask<GameUser?> FindByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Login, passwd, sex, LoginUID, firstLogin, lastConnect, lastLogin,
                   playTime, gamePoint, IPAddress, Connecting, ModeLevel, Grade
            FROM users
            WHERE Login = $login
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$login", login);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new GameUser(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt64(3),
            ParseDate(reader.GetString(4)),
            ParseDate(reader.GetString(5)),
            ParseDate(reader.GetString(6)),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetInt64(10) != 0,
            (byte[])reader[11],
            reader.GetInt32(12));
    }

    public async ValueTask<long> CreateAsync(GameUser user, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users
                (Login, passwd, sex, firstLogin, lastConnect, lastLogin, playTime,
                 gamePoint, IPAddress, Connecting, ModeLevel, Grade)
            VALUES
                ($login, $passwd, $sex, $firstLogin, $lastConnect, $lastLogin, $playTime,
                 $gamePoint, $ipAddress, $connecting, $modeLevel, $grade);
            SELECT last_insert_rowid();
            """;
        BindUser(command, user);
        var scalar = await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("SQLite did not return the new LoginUID.");
        return Convert.ToInt64(scalar);
    }

    private static void BindUser(SqliteCommand command, GameUser user)
    {
        command.Parameters.AddWithValue("$login", user.Login);
        command.Parameters.AddWithValue("$passwd", user.Passwd);
        command.Parameters.AddWithValue("$sex", user.Sex);
        command.Parameters.AddWithValue("$firstLogin", user.FirstLogin.ToString("O"));
        command.Parameters.AddWithValue("$lastConnect", user.LastConnect.ToString("O"));
        command.Parameters.AddWithValue("$lastLogin", user.LastLogin.ToString("O"));
        command.Parameters.AddWithValue("$playTime", user.PlayTime);
        command.Parameters.AddWithValue("$gamePoint", user.GamePoint);
        command.Parameters.AddWithValue("$ipAddress", user.IpAddress);
        command.Parameters.AddWithValue("$connecting", user.Connecting ? 1 : 0);
        command.Parameters.AddWithValue("$modeLevel", user.ModeLevel);
        command.Parameters.AddWithValue("$grade", user.Grade);
    }

    private static DateTime ParseDate(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}

public sealed class GUserRepository(SqliteDatabase database)
{
    public async ValueTask<GUser?> FindAsync(long userUid, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserUID, UserID, USSize, RegDate, DelDate FROM GUser WHERE UserUID = $uid LIMIT 1;";
        command.Parameters.AddWithValue("$uid", userUid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new GUser(reader.GetInt64(0), reader.GetString(1), checked((byte)reader.GetInt32(2)), ParseDate(reader.GetString(3)), ParseDate(reader.GetString(4)));
    }

    public async ValueTask UpsertAsync(GUser user, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GUser (UserUID, UserID, USSize, RegDate, DelDate)
            VALUES ($uid, $userId, $size, $regDate, $delDate)
            ON CONFLICT(UserUID) DO UPDATE SET
                UserID = excluded.UserID,
                USSize = excluded.USSize,
                RegDate = excluded.RegDate,
                DelDate = excluded.DelDate;
            """;
        command.Parameters.AddWithValue("$uid", user.UserUid);
        command.Parameters.AddWithValue("$userId", user.UserId);
        command.Parameters.AddWithValue("$size", user.UsSize);
        command.Parameters.AddWithValue("$regDate", user.RegDate.ToString("O"));
        command.Parameters.AddWithValue("$delDate", user.DelDate.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTime ParseDate(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}

public sealed class GUnitRepository(SqliteDatabase database)
{
    public async IAsyncEnumerable<GUnit> ListByUserAsync(long userUid, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UnitUID, UserUID, UnitClass, Exp, Level, GamePoint, VSPoint, VSPointMax,
                   BaseHP, AtkPhysic, AtkMagic, DefPhysic, DefMagic, SPoint, Win, Lose,
                   Seceder, RegDate, DelDate, LastDate, LastPosition, PlayDayCnt, LoginCount
            FROM GUnit
            WHERE UserUID = $uid
            ORDER BY UnitUID;
            """;
        command.Parameters.AddWithValue("$uid", userUid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new GUnit(
                reader.GetInt64(0), reader.GetInt64(1), checked((byte)reader.GetInt32(2)), reader.GetInt32(3),
                checked((byte)reader.GetInt32(4)), reader.GetInt32(5), reader.GetInt32(6), reader.IsDBNull(7) ? null : reader.GetInt32(7),
                checked((short)reader.GetInt32(8)), checked((byte)reader.GetInt32(9)), checked((byte)reader.GetInt32(10)),
                checked((byte)reader.GetInt32(11)), checked((byte)reader.GetInt32(12)), reader.GetInt32(13), reader.GetInt32(14),
                reader.GetInt32(15), reader.GetInt32(16), ParseDate(reader.GetString(17)), ParseDate(reader.GetString(18)),
                ParseDate(reader.GetString(19)), reader.GetInt32(20), reader.GetInt32(21), reader.GetInt32(22));
        }
    }

    private static DateTime ParseDate(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}

public sealed class GItemRepository(SqliteDatabase database)
{
    public async IAsyncEnumerable<GItem> ListByUnitAsync(long unitUid, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ItemUID, UnitUID, ItemID, InventoryCategory, SlotID, RegDate, DelDate FROM GItem WHERE UnitUID = $uid AND Deleted = 0 ORDER BY SlotID, ItemUID;";
        command.Parameters.AddWithValue("$uid", unitUid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new GItem(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2), checked((byte)reader.GetInt32(3)), checked((byte)reader.GetInt32(4)), ParseDate(reader.GetString(5)), ParseDate(reader.GetString(6)));
        }
    }

    private static DateTime ParseDate(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}

public sealed class GSkillRepository(SqliteDatabase database)
{
    public async IAsyncEnumerable<GSkill> ListByUnitAsync(long unitUid, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UnitUID, SkillID, RegDate FROM GSkill WHERE UnitUID = $uid ORDER BY SkillID;";
        command.Parameters.AddWithValue("$uid", unitUid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            yield return new GSkill(reader.GetInt64(0), reader.GetInt32(1), DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}
