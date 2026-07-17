using System.Buffers.Binary;
using System.Text;
using IsaacEdenTokenEditor.Core;

namespace IsaacEdenTokenEditor.Core.Tests;

public sealed class IsaacSaveCodecTests
{
    private readonly IsaacSaveCodec _codec = new();

    [Theory]
    [InlineData(0x7E, IsaacSaveVersion.Repentance)]
    [InlineData(0x82, IsaacSaveVersion.RepentancePlus)]
    public void Parse_ReadsSupportedVersionsAndTokens(byte versionByte, IsaacSaveVersion version)
    {
        var data = BuildSave(versionByte, 374);
        var info = _codec.Parse(data);
        Assert.Equal(version, info.Version);
        Assert.Equal(374u, info.EdenTokens);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(100000u)]
    [InlineData(uint.MaxValue)]
    public void SetEdenTokens_RoundTrips_AndOnlyChangesValueAndChecksum(uint value)
    {
        var source = BuildSave(0x82, 171);
        var modified = _codec.SetEdenTokens(source, value);
        var info = _codec.Parse(modified);
        Assert.Equal(value, info.EdenTokens);
        Assert.False(ReferenceEquals(source, modified));
        for (var i = 0; i < source.Length; i++)
        {
            if (i >= info.EdenOffset && i < info.EdenOffset + 4) continue;
            if (i >= source.Length - 4) continue;
            Assert.Equal(source[i], modified[i]);
        }
    }

    [Fact]
    public void Parse_RejectsDamagedChecksum()
    {
        var data = BuildSave(0x82, 10);
        data[60] ^= 1;
        Assert.Throws<SaveValidationException>(() => _codec.Parse(data));
    }

    [Fact]
    public void Parse_RejectsUnsupportedVersion()
    {
        var data = BuildSave(0x82, 10);
        data[0x18] = 0x55;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(data.Length - 4), _codec.CalculateChecksum(data));
        Assert.Throws<SaveValidationException>(() => _codec.Parse(data));
    }

    private byte[] BuildSave(byte version, uint tokens)
    {
        var data = new byte[244];
        Encoding.ASCII.GetBytes("ISAACNGSAVE09R  ").CopyTo(data, 0);
        var offset = 0x14;
        for (var section = 0; section < 11; section++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), (uint)section);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4), section == 0 ? version : 0u);
            var count = section == 1 ? 22u : 0u;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8), count);
            offset += 12;
            if (section == 1)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 0x54), tokens);
                offset += 88;
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(data.Length - 4), _codec.CalculateChecksum(data));
        return data;
    }
}
