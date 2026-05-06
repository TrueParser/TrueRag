namespace TrueRag.Host.Migrations;

public sealed class SchemaMigrationStartupOptions
{
    public const string SectionName = "SchemaMigrations";

    public bool AutoMigrateOnStartup { get; set; } = false;

    public bool FailFastOnPendingMigrations { get; set; } = true;
}
