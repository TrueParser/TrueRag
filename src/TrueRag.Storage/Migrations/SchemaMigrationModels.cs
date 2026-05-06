namespace TrueRag.Storage.Migrations;

public sealed record SchemaMigrationDefinition(
    string Version,
    string Description,
    string Sql);

public sealed record AppliedSchemaMigration(
    string Version,
    string Checksum,
    DateTimeOffset AppliedUtc);

public sealed record SchemaMigrationPlan(
    IReadOnlyCollection<SchemaMigrationDefinition> Pending,
    IReadOnlyCollection<AppliedSchemaMigration> Applied);

public sealed record SchemaMigrationDrift(
    string Version,
    string ExpectedChecksum,
    string ActualChecksum);
