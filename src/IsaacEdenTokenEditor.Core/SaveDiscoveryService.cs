using Microsoft.Win32;
using System.Runtime.Versioning;

namespace IsaacEdenTokenEditor.Core;

public sealed record DiscoveredSave(string SteamUserId, int Slot, string Path, IsaacSaveInfo Info)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

public sealed class SaveDiscoveryService(IsaacSaveCodec codec)
{
    private static readonly string[] Names =
    [
        "rep_persistentgamedata1.dat", "rep_persistentgamedata2.dat", "rep_persistentgamedata3.dat",
        "rep+persistentgamedata1.dat", "rep+persistentgamedata2.dat", "rep+persistentgamedata3.dat"
    ];

    public IReadOnlyList<DiscoveredSave> Discover(string? steamRoot = null)
    {
        if (!OperatingSystem.IsWindows()) return [];
        steamRoot ??= ReadSteamRoot();
        if (string.IsNullOrWhiteSpace(steamRoot)) return [];
        var userdata = System.IO.Path.Combine(steamRoot.Replace('/', System.IO.Path.DirectorySeparatorChar), "userdata");
        if (!Directory.Exists(userdata)) return [];

        var found = new List<DiscoveredSave>();
        foreach (var userDir in Directory.EnumerateDirectories(userdata))
        {
            var remote = System.IO.Path.Combine(userDir, "250900", "remote");
            if (!Directory.Exists(remote)) continue;
            foreach (var name in Names)
            {
                var path = System.IO.Path.Combine(remote, name);
                if (!File.Exists(path)) continue;
                try
                {
                    var info = codec.Parse(File.ReadAllBytes(path));
                    var slot = name[^5] - '0';
                    found.Add(new DiscoveredSave(System.IO.Path.GetFileName(userDir), slot, path, info));
                }
                catch (SaveValidationException)
                {
                    // Invalid candidates are intentionally excluded from writable results.
                }
            }
        }
        return found.OrderBy(x => x.SteamUserId).ThenBy(x => x.Info.Version).ThenBy(x => x.Slot).ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadSteamRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string;
    }
}
