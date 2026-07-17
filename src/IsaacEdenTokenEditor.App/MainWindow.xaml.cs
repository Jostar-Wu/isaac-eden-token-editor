using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using IsaacEdenTokenEditor.Core;

namespace IsaacEdenTokenEditor.App;

public partial class MainWindow : Window
{
    private readonly IsaacSaveCodec _codec = new();
    private readonly SaveDiscoveryService _discovery;
    private readonly SaveFileService _files;
    private SelectedSave? _selected;
    private string? _resultDirectory;

    public MainWindow()
    {
        InitializeComponent();
        _discovery = new SaveDiscoveryService(_codec);
        _files = new SaveFileService(_codec);
        RefreshGameState();
    }

    private void RefreshGameState()
    {
        var running = Process.GetProcessesByName("isaac-ng").Length > 0;
        GameStateText.Text = running ? "● 游戏正在运行" : "● 游戏未运行";
        GameStateText.Foreground = running ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.LightGreen;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "正在扫描 Steam 存档…");
        try
        {
            var results = await Task.Run(() => _discovery.Discover());
            SaveGrid.ItemsSource = results.Select(x => new SaveRow(x)).ToArray();
            StatusText.Text = results.Count == 0
                ? "没有发现有效存档。请使用“手动选择 .dat 文件”，或确认 Steam 已安装并运行过游戏。"
                : $"找到 {results.Count} 个有效存档，请选择要修改的存档位。";
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); RefreshGameState(); }
    }

    private void ManualButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择以撒 persistentgamedata 存档",
            Filter = "以撒存档 (*.dat)|*.dat|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true) LoadManual(dialog.FileName);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            LoadManual(files[0]);
    }

    private void LoadManual(string path)
    {
        try
        {
            var info = _codec.Parse(File.ReadAllBytes(path));
            _selected = new SelectedSave(path, info, true);
            SaveGrid.SelectedItem = null;
            UpdateSelection();
            StatusText.Text = "手动存档已验证。程序只会导出新文件，不会覆盖源文件。";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SaveGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SaveGrid.SelectedItem is not SaveRow row) return;
        _selected = new SelectedSave(row.Path, row.Source.Info, false);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_selected is null) return;
        var version = _selected.Info.Version == IsaacSaveVersion.RepentancePlus ? "Repentance+" : "Repentance";
        SelectedFileText.Text = $"{version} · 当前次数 {_selected.Info.EdenTokens}\n{_selected.Path}";
        ExecuteButton.Content = _selected.IsManual ? "仅导出新文件" : "备份并写回";
        ExecuteButton.IsEnabled = true;
        OpenResultButton.IsEnabled = false;
        _resultDirectory = null;
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e) => TokenTextBox.Text = "100000";

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        if (!uint.TryParse(TokenTextBox.Text.Trim(), out var tokens))
        {
            MessageBox.Show("请输入 0 到 4,294,967,295 之间的整数。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBusy(true, _selected.IsManual ? "正在生成新存档…" : "正在备份、写回并验证…");
        try
        {
            if (_selected.IsManual)
            {
                var dialog = new OpenFolderDialog { Title = "选择新存档的导出文件夹" };
                if (dialog.ShowDialog() != true) return;
                var target = await Task.Run(() => _files.Export(_selected.Path, dialog.FolderName, tokens));
                _resultDirectory = Path.GetDirectoryName(target);
                StatusText.Text = $"完成：新文件已导出到 {target}。关闭游戏后，用它替换 Steam 存档目录中的同名文件。";
            }
            else
            {
                var confirm = MessageBox.Show("程序将先备份原文件，再把修改后的存档写回 Steam。请确认游戏已完全关闭。", "确认写回", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.OK) return;
                var result = await Task.Run(() => _files.BackupAndWrite(_selected.Path, tokens));
                _resultDirectory = Path.GetDirectoryName(result.BackupPath);
                _selected = _selected with { Info = result.Info };
                UpdateSelection();
                StatusText.Text = $"完成：伊甸次数已修改为 {tokens}，并验证通过。备份：{result.BackupPath}";
            }
            OpenResultButton.IsEnabled = true;
            MessageBox.Show(StatusText.Text, "修改完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); RefreshGameState(); }
    }

    private void OpenResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_resultDirectory is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", _resultDirectory) { UseShellExecute = true });
    }

    private void SetBusy(bool busy, string? message = null)
    {
        ScanButton.IsEnabled = !busy;
        ExecuteButton.IsEnabled = !busy && _selected is not null;
        if (message is not null) StatusText.Text = message;
    }

    private void ShowError(Exception ex)
    {
        StatusText.Text = "失败：" + ex.Message;
        MessageBox.Show(ex.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private sealed record SelectedSave(string Path, IsaacSaveInfo Info, bool IsManual);

    private sealed class SaveRow
    {
        public SaveRow(DiscoveredSave source) => Source = source;
        public DiscoveredSave Source { get; }
        public string SteamUserId => Source.SteamUserId;
        public int Slot => Source.Slot;
        public string VersionText => Source.Info.Version == IsaacSaveVersion.RepentancePlus ? "Repentance+" : "Repentance";
        public uint Tokens => Source.Info.EdenTokens;
        public string Path => Source.Path;
    }
}
