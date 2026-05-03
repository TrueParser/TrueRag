using System.Collections.Concurrent;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TrueRag.Ingestion.Queue;

internal static class NatsJetStreamProvisioner
{
    private static readonly ConcurrentDictionary<string, StreamRegistrationState> StreamStates = new(StringComparer.Ordinal);

    public static Task EnsureStreamAsync(
        NatsJSContext jsContext,
        string streamName,
        string subjectPrefix,
        string description,
        CancellationToken cancellationToken = default)
    {
        var state = StreamStates.GetOrAdd(streamName, _ => new StreamRegistrationState());
        if (state.IsProvisionedFor(subjectPrefix))
        {
            return Task.CompletedTask;
        }

        return EnsureStreamCoreAsync(jsContext, state, streamName, subjectPrefix, description, cancellationToken);
    }

    private static async Task EnsureStreamCoreAsync(
        NatsJSContext jsContext,
        StreamRegistrationState state,
        string streamName,
        string subjectPrefix,
        string description,
        CancellationToken cancellationToken)
    {
        await state.StreamLock.WaitAsync(cancellationToken);
        try
        {
            if (state.IsProvisionedFor(subjectPrefix))
            {
                return;
            }

            var addedSubject = state.Subjects.TryAdd(subjectPrefix, 0);
            var streamConfig = new StreamConfig(streamName, [.. state.Subjects.Keys])
            {
                Description = description
            };

            try
            {
                await jsContext.CreateStreamAsync(streamConfig, cancellationToken);
                state.MarkProvisioned();
            }
            catch
            {
                await jsContext.UpdateStreamAsync(streamConfig, cancellationToken);
                state.MarkProvisioned();
            }
        }
        catch
        {
            if (state.Subjects.ContainsKey(subjectPrefix) && !state.IsProvisionedFor(subjectPrefix))
            {
                state.Subjects.TryRemove(subjectPrefix, out _);
            }

            throw;
        }
        finally
        {
            state.StreamLock.Release();
        }
    }

    private sealed class StreamRegistrationState
    {
        public SemaphoreSlim StreamLock { get; } = new(1, 1);

        public ConcurrentDictionary<string, byte> Subjects { get; } = new(StringComparer.Ordinal);

        private volatile bool _isProvisioned;

        public bool IsProvisionedFor(string subjectPrefix)
            => _isProvisioned && Subjects.ContainsKey(subjectPrefix);

        public void MarkProvisioned()
            => _isProvisioned = true;
    }
}