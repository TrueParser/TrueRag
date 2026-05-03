using Microsoft.Extensions.Configuration;
using NATS.Client.Core;
using TrueRag.Ingestion.Configuration;

namespace TrueRag.Ingestion.Queue;

internal static class NatsConnectionFactory
{
    public static NatsConnection Create(IConfiguration configuration, string clientName)
    {
        var queueSection = configuration.GetSection(QueueConfiguration.SectionName);
        var url = queueSection[nameof(QueueConfiguration.Url)];

        if (string.IsNullOrWhiteSpace(url))
        {
            url = configuration.GetConnectionString("Nats") ?? "nats://localhost:4222";
        }

        var options = NatsOpts.Default with
        {
            Url = url,
            Name = clientName
        };

        return new NatsConnection(options);
    }
}