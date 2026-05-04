namespace TrueRag.Api.Models;

public sealed record IngestAsyncAcceptedView(
    string NodeId,
    string TenantId,
    string AppId,
    string CollectionId,
    string WalPath,
    string WalSegmentId,
    long WalOffset,
    long WalLength);
