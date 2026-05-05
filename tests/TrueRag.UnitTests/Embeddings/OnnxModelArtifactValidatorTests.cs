using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.UnitTests.Embeddings;

public sealed class OnnxModelArtifactValidatorTests
{
    [Fact]
    public void ValidateAndGetModelPath_Throws_WhenFileMissing()
    {
        var validator = new OnnxModelArtifactValidator();
        var options = new OnnxEmbeddingOptions
        {
            ModelArtifactsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            ModelFileName = "model.onnx"
        };

        Assert.Throws<FileNotFoundException>(() => validator.ValidateAndGetModelPath(options));
    }

    [Fact]
    public void ValidateAndGetModelPath_Throws_WhenFileEmpty()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var modelPath = Path.Combine(directory, "model.onnx");
        File.WriteAllBytes(modelPath, []);

        try
        {
            var validator = new OnnxModelArtifactValidator();
            var options = new OnnxEmbeddingOptions
            {
                ModelArtifactsPath = directory,
                ModelFileName = "model.onnx"
            };

            Assert.Throws<InvalidOperationException>(() => validator.ValidateAndGetModelPath(options));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
