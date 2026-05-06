namespace TrueRag.Storage.Migrations;

public interface ISchemaMigrationService
{
    Task<SchemaMigrationPlan> GetPlanAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SchemaMigrationDrift>> ValidateChecksumsAsync(CancellationToken cancellationToken = default);

    Task<SchemaMigrationPlan> ApplyPendingAsync(CancellationToken cancellationToken = default);
}
