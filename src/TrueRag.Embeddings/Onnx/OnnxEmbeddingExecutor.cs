using System.Security.Cryptography;
using System.Text;

namespace TrueRag.Embeddings.Onnx;

internal interface IOnnxEmbeddingExecutor
{
    Task<float[]> GenerateVectorAsync(string normalizedText, int dimensions, CancellationToken cancellationToken);
}

internal sealed class DeterministicOnnxEmbeddingExecutor : IOnnxEmbeddingExecutor
{
    public Task<float[]> GenerateVectorAsync(string normalizedText, int dimensions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = Encoding.UTF8.GetBytes(normalizedText);
        var vector = new float[dimensions];

        for (var i = 0; i < dimensions; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var indexBytes = BitConverter.GetBytes(i);
            var payload = new byte[bytes.Length + indexBytes.Length];
            Buffer.BlockCopy(bytes, 0, payload, 0, bytes.Length);
            Buffer.BlockCopy(indexBytes, 0, payload, bytes.Length, indexBytes.Length);

            var hash = SHA256.HashData(payload);
            var unit = BitConverter.ToUInt32(hash, 0) / (float)uint.MaxValue;
            vector[i] = (unit * 2f) - 1f;
        }

        return Task.FromResult(vector);
    }
}
