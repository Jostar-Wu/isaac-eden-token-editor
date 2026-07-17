using IsaacEdenTokenEditor.Core;

namespace IsaacEdenTokenEditor.Core.Tests;

public sealed class LocalSampleValidationTests
{
    [Fact]
    public void OptionalLocalSamples_ParseWithoutBeingCommitted()
    {
        var directory = Environment.GetEnvironmentVariable("ISAAC_SAVE_SAMPLE_DIR");
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

        var codec = new IsaacSaveCodec();
        var files = Directory.EnumerateFiles(directory, "*persistentgamedata?.dat")
            .Where(path => Path.GetFileName(path).StartsWith("rep", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var source = File.ReadAllBytes(file);
            var info = codec.Parse(source);
            Assert.True(info.EdenOffset > 0);
            var modified = codec.SetEdenTokens(source, 100000);
            Assert.Equal(100000u, codec.Parse(modified).EdenTokens);
            Assert.NotSame(source, modified);
        }
    }
}
