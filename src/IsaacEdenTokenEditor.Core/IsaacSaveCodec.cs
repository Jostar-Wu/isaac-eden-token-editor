using System.Buffers.Binary;
using System.Text;

namespace IsaacEdenTokenEditor.Core;

public enum IsaacSaveVersion
{
    Repentance,
    RepentancePlus
}

public sealed record IsaacSaveInfo(
    IsaacSaveVersion Version,
    uint EdenTokens,
    int EdenOffset,
    int Length,
    uint Checksum);

public sealed class SaveValidationException(string message) : Exception(message);

public sealed class IsaacSaveCodec
{
    private const int SectionTableOffset = 0x14;
    private const int VersionOffset = 0x18;
    private const int ChecksumStart = 0x10;
    private const uint ChecksumSeed = 0xFEDCBA76;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ISAACNGSAVE");
    private static readonly int[] EntryLengths = [1, 4, 4, 1, 1, 1, 1, 4, 4, 1, 546];
    private static readonly uint[] CrcTable = DecodeCrcTable();

    private const string CrcTableBase64 =
        "AAAAAJYwBwksYQ4SulEJGxnEbf+P9Gr2NaVj7aOVZOQyiNv+pLjc9x7p1eyI2dLlK0y2Ab18sQgHLbgTkR2/GmQQt/3yILD0SHG5795BvuZ91NoC6+TdC1G11BDHhdMZVphsA8Coawp6+WIR7MllGE9cAfzZbAb1Yz0P7vUNCOfIIG77XhBp8uRBYOlycWfg0eQDBEfUBA39hQ0Wa7UKH/qotQVsmLIM1sm7F0D5vB7jbNj6dVzf888N1uhZPdHhrDDZBjoA3g+AUdcUFmHQHbX0tPkjxLPwmZW66w+lveKeuAL4CIgF8bLZDOok6Qvjh3xvBxFMaA6rHWEVPS1mHJBB3PYGcdv/vCDS5CoQ1e2JhbEJH7W2AKXkvxsz1LgSoskHCDT5AAGOqAkaGJgOE7sNavctPW3+l2xk5QFcY+z0UWsLYmFsAtgwZRlOAGIQ7ZUG9HulAf3B9AjmV8QP78bZsPVQ6bf86ri+53yIue7fHd0KSS3aA/N80xhlTNQRWGGyDc5RtQR0ALwf4jC7FkGl3/LXldj7bcTR4Pv01ulq6Wnz/Nlu+kaIZ+HQuGDocy0EDOUdAwVfTAoeyXwNFzxxBfCqQQL5EBAL4oYgDOsltWgPs4VvBgnUZh2f5GEUDvneDpjJ2QcimNActKjXFRc9s/GBDbT4O1y9461suuogg7jttrO/5Azitv+a0rH2OUfVEq930hsVJtsAgxbcCRILYxOEO2QaPmptAahaaggLzw7snf8J5SeuAP6xngf3RJMPENKjCBlo8gEC/sIGC11XYu/LZ2XmcTZs/ecGa/R2G9Tu4CvT51p62vzMSt31b9+5EfnvvhhDvrcD1Y6wCuij1hZ+k9EfxMLYBFLy3w3xZ7vpZ1e84N0GtftLNrLy2isN6EwbCuH2SgP6YHoE88PvYBdV32ce745uBXm+aQyMs2HrGoNm4qDSb/k24mjwlXcMFANHCx25FgIGLyYFD747uhUoC70cklq0BwRqsw6n/9fqMc/Q44ue2fgdrt7xsMJkGybyYxKco2oJCpNtAKkGCeQ/Ng7thWcH9hNXAP+CSr/lFHq47K4rsfc4G7b+m47SGg2+1RO379wIId/bAdTS0+ZC4tTv+LPd9G6D2v3NFr4ZWya5EOF3sAt3R7cC5loIGHBqDxHKOwYKXAsBA/+eZedprmLu0/9r9UXPbPx44grg7tIN6VSDBPLCswP7YSZnH/cWYBZNR2kN23duBEpq0R7cWtYXZgvfDPA72AVTrrzhxZ676H/PsvPp/7X6HPK9HYrCuhQwk7MPpqO0BgU20OKTBtfrKVfe8L9n2fkuembjuEph6gIbaPGUK2/4N74LHKGODBUb3wUOje8CBw==";

    public IsaacSaveInfo Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 64)
            throw new SaveValidationException("文件过短，不是有效的以撒存档。");
        if (!data[..Magic.Length].SequenceEqual(Magic))
            throw new SaveValidationException("文件头不正确，请选择 persistentgamedata 存档。");

        var version = data[VersionOffset] switch
        {
            0x7E => IsaacSaveVersion.Repentance,
            0x82 => IsaacSaveVersion.RepentancePlus,
            var value => throw new SaveValidationException($"不支持的存档版本：0x{value:X2}。")
        };

        var storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(data[^4..]);
        var calculatedChecksum = CalculateChecksum(data);
        if (storedChecksum != calculatedChecksum)
            throw new SaveValidationException("存档 CRC 校验失败，文件可能已损坏或正在被游戏修改。");

        var offset = SectionTableOffset;
        var starts = new int[EntryLengths.Length];
        for (var section = 0; section < EntryLengths.Length; section++)
        {
            EnsureRange(data, offset, 12);
            var count = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 8, 4));
            offset = checked(offset + 12);
            starts[section] = offset;
            // The final bestiary section is variable-length despite its legacy table entry size.
            // Its start must be valid, but it is not needed to locate the statistics section.
            if (section == EntryLengths.Length - 1)
            {
                if (offset > data.Length - 4)
                    throw new SaveValidationException("存档区段超出文件边界。");
                break;
            }
            var sectionBytes = checked((long)count * EntryLengths[section]);
            if (sectionBytes > int.MaxValue || offset + sectionBytes > data.Length - 4L)
                throw new SaveValidationException("存档区段超出文件边界。");
            offset = checked(offset + (int)sectionBytes);
        }

        var edenOffset = checked(starts[1] + 0x54);
        EnsureRange(data, edenOffset, 4);
        var tokens = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(edenOffset, 4));
        return new IsaacSaveInfo(version, tokens, edenOffset, data.Length, storedChecksum);
    }

    public byte[] SetEdenTokens(ReadOnlySpan<byte> source, uint tokens)
    {
        var info = Parse(source);
        var result = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(info.EdenOffset, 4), tokens);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(result.Length - 4, 4), CalculateChecksum(result));
        var verified = Parse(result);
        if (verified.EdenTokens != tokens)
            throw new SaveValidationException("修改后的存档验证失败。");
        return result;
    }

    public uint CalculateChecksum(ReadOnlySpan<byte> data)
    {
        if (data.Length < ChecksumStart + 4)
            throw new SaveValidationException("文件过短，无法计算 CRC。");
        var crc = ~ChecksumSeed;
        foreach (var value in data[ChecksumStart..^4])
            crc = CrcTable[(crc & 0xFF) ^ value] ^ (crc >> 8);
        return ~crc;
    }

    private static void EnsureRange(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new SaveValidationException("存档区段超出文件边界。");
    }

    private static uint[] DecodeCrcTable()
    {
        var bytes = Convert.FromBase64String(CrcTableBase64);
        var table = new uint[256];
        for (var i = 0; i < table.Length; i++)
            table[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
        return table;
    }
}
