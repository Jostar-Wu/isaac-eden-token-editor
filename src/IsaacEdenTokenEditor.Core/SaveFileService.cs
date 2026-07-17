using System.Diagnostics;
using System.Security.Cryptography;

namespace IsaacEdenTokenEditor.Core;

public sealed record AutomaticWriteResult(string SavePath, string BackupPath, IsaacSaveInfo Info);

public sealed class SaveFileService(IsaacSaveCodec codec)
{
    public string Export(string sourcePath, string outputDirectory, uint tokens)
    {
        var source = File.ReadAllBytes(sourcePath);
        var modified = codec.SetEdenTokens(source, tokens);
        Directory.CreateDirectory(outputDirectory);
        var target = Path.Combine(outputDirectory, Path.GetFileName(sourcePath));
        if (Path.GetFullPath(target).Equals(Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
            throw new IOException("手动导出不能覆盖源文件，请选择其他文件夹。");
        File.WriteAllBytes(target, modified);
        codec.Parse(File.ReadAllBytes(target));
        return target;
    }

    public AutomaticWriteResult BackupAndWrite(string sourcePath, uint tokens)
    {
        if (Process.GetProcessesByName("isaac-ng").Length > 0)
            throw new IOException("游戏正在运行。请完全关闭《以撒的结合》后再写回存档。");

        var source = File.ReadAllBytes(sourcePath);
        codec.Parse(source);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "IsaacEdenEditorBackups", stamp);
        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(backupDir, Path.GetFileName(sourcePath));
        File.WriteAllBytes(backupPath, source);
        if (!SHA256.HashData(source).SequenceEqual(SHA256.HashData(File.ReadAllBytes(backupPath))))
            throw new IOException("备份验证失败，未修改原存档。");

        var modified = codec.SetEdenTokens(source, tokens);
        var tempPath = sourcePath + ".eden-editor.tmp";
        var replaced = false;
        try
        {
            File.WriteAllBytes(tempPath, modified);
            codec.Parse(File.ReadAllBytes(tempPath));
            if (!SHA256.HashData(source).SequenceEqual(SHA256.HashData(File.ReadAllBytes(sourcePath))))
                throw new IOException("存档在扫描后发生了变化。请重新扫描后再操作。");
            File.Move(tempPath, sourcePath, true);
            replaced = true;
            var verifiedBytes = File.ReadAllBytes(sourcePath);
            var verified = codec.Parse(verifiedBytes);
            if (verified.EdenTokens != tokens || verifiedBytes.Length != source.Length)
                throw new IOException("写回后的存档验证失败，请恢复备份。");
            return new AutomaticWriteResult(sourcePath, backupPath, verified);
        }
        catch
        {
            if (replaced)
                File.Copy(backupPath, sourcePath, true);
            throw;
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
