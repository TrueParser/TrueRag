namespace TrueRag.Ingestion.Admission;

public interface IFamilyQueueDepthTracker
{
    bool TryReserve(
        string tenantId,
        string appId,
        string collectionId,
        string familyKey,
        string documentId,
        int maxDepth,
        out int currentDepth,
        out bool reservedNew);

    bool Release(
        string tenantId,
        string appId,
        string collectionId,
        string familyKey,
        string documentId);

    bool MarkPublished(
        string tenantId,
        string appId,
        string collectionId,
        string familyKey,
        string documentId);

    bool MarkTerminal(
        string tenantId,
        string appId,
        string collectionId,
        string familyKey,
        string documentId);

    int GetDepth(string tenantId, string appId, string collectionId, string familyKey);

    int GetTotalLiveDepth();
}
