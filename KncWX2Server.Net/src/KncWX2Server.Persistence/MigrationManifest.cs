namespace KncWX2Server.Persistence;

internal readonly record struct MigrationDefinition(string Version, string ResourceName);

internal static class MigrationManifest
{
    private static readonly MigrationDefinition[] s_all =
    [
        new("001_initial", "KncWX2Server.Persistence.Migrations.001_initial.sql"),
        new("002_gup_create_unit_schema", "KncWX2Server.Persistence.Migrations.002_gup_create_unit_schema.sql"),
        new("003_fix_gunit_nickname_schema", "KncWX2Server.Persistence.Migrations.003_fix_gunit_nickname_schema.sql"),
        new("004_gup_delete_unit_schema", "KncWX2Server.Persistence.Migrations.004_gup_delete_unit_schema.sql"),
    ];

    public static ReadOnlySpan<MigrationDefinition> All => s_all;
}
