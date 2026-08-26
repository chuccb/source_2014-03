# KncWX2Server — C# 15 Preview / .NET 11 Preview / SQLite 3.53

This directory is a side-by-side modern rewrite of the legacy `KncWX2Server` tree. The original native source remains untouched so protocol behavior can be ported and compared incrementally.

## Target stack

- C# 15 Preview with `LangVersion=preview` and preview features enabled.
- .NET 11 Preview.
- `Microsoft.Data.Sqlite.Core` preview with SQLitePCLRaw native loading.
- `SourceGear.sqlite3` pinned to SQLite 3.53.4.
- Nullable reference types, implicit usings, analyzers and warnings-as-errors enabled.

The persistence layer checks `sqlite_version()` at startup and refuses to run against a SQLite release outside the 3.53.x line.

## Projects

| Project | Purpose |
| --- | --- |
| `KncWX2Server.Core` | Domain models, server roles, legacy performer IDs and framed packet transport. |
| `KncWX2Server.Persistence` | SQLite bootstrap, migrations and parameterized repositories. |
| `KncWX2Server.Host` | Shared asynchronous TCP listener/session lifecycle. |
| `KncWX2Server.LoginServer` | Login entry point, default port 9001. |
| `KncWX2Server.CenterServer` | Center entry point, default port 9002. |
| `KncWX2Server.ChannelServer` | Channel entry point, default port 9003. |
| `KncWX2Server.GameServer` | Game entry point, default port 9004. |

## SQLite migration currently covered

The first migration ports the schemas found in the original SQL Server sources for:

- `users`
- `GUser`
- `GUnit`
- `GItem`
- `GSkill`

SQL Server `IDENTITY` columns become SQLite `INTEGER PRIMARY KEY AUTOINCREMENT` where the legacy schema relied on generated numeric IDs. SQL Server `varbinary` maps to SQLite `BLOB`; boolean state is stored as `INTEGER` with checks; the legacy `Deleted` computed columns are represented as generated columns.

The original SQL Server schemas did not consistently declare relational keys. The SQLite migration adds only the minimal primary/foreign-key constraints needed to keep `GUser -> GUnit -> GItem/GSkill` referential integrity explicit.

## Legacy compatibility status

This is deliberately an incremental rewrite, not a claim of one-commit gameplay parity. The original server distributes wire protocol behavior across native event, performer, manager and script code, and the repository also contains many additional SQL tables/stored procedures.

The transport layer therefore does not invent opcode semantics. It preserves packets as `(length, opcode, flags, payload)` frames and logs unported opcodes until their exact legacy behavior is migrated.

The old configuration defaults already visible in the source are carried into `ServerOptions` where applicable, including six worker threads, sequence-number checking and a packet-authentication failure limit of 100.

## Run

From a .NET 11 preview SDK shell:

```powershell
dotnet build .\KncWX2Server.Net\KncWX2Server.slnx -c Release

dotnet run --project .\KncWX2Server.Net\src\KncWX2Server.GameServer -- --db .\data\kncwx2.db --bind 127.0.0.1 --port 9004
```

All server entry points accept the same arguments: `--bind`, `--port`, `--db`, `--backlog`, `--workers`, `--auth-fail-limit`, `--max-payload`, and `--no-sequence-check`.

## Next porting stages

1. Port the exact legacy packet headers/opcodes and sequence/authentication algorithm from the native protocol sources.
2. Port login/account flows against the migrated SQLite tables.
3. Port Center/Channel/Game performer routing and event dispatch.
4. Migrate the remaining game tables, stored procedures and blob payloads from `DataBase/Database2`.
5. Add packet-level compatibility tests against captured legacy client/server traffic.
