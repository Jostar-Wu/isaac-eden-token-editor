# 伊甸次数修改器

一个离线、开源的 Windows 桌面工具，用于修改《以撒的结合》Repentance / Repentance+ 存档中的伊甸使用次数（Eden Tokens）。

> 本项目与 Nicalis、Edmund McMillen 或《以撒的结合》官方无关联。修改前请关闭游戏，并妥善保存备份。

## 特点

- 自动寻找 Steam `userdata/<SteamID>/250900/remote` 中的三个存档位。
- 同时支持 Repentance（`rep_`）和 Repentance+（`rep+`）。
- 显示存档版本、存档位、当前次数和完整路径。
- 自动模式先备份，再写回，最后重新读取并验证 CRC。
- 手动模式支持选择或拖入 `.dat`，只导出新文件，不覆盖源文件。
- 完全离线，不上传存档，不收集遥测。

## 下载

从 GitHub Releases 下载最新的 `IsaacEdenTokenEditor.exe`。程序为免安装单文件，双击即可运行。

项目暂未购买代码签名证书，Windows SmartScreen 可能显示“未知发布者”。请只从本仓库 Release 下载，并核对 Release 中公布的 SHA-256；不要因此关闭杀毒软件。

## 自动模式

1. 完全关闭《以撒的结合》。
2. 启动程序，点击“自动扫描 Steam 存档”。
3. 选择要修改的 Steam 用户、版本和存档位。
4. 输入次数（可直接使用 `100000` 快捷值）。
5. 点击“备份并写回”。
6. 原文件会先备份到 `文档\IsaacEdenEditorBackups\日期-时间`。
7. 程序写回后会重新验证文件；只有验证通过才显示成功。

## 手动模式

1. 点击“手动选择 .dat 文件”，也可以把文件拖进窗口。
2. 输入目标次数并点击“仅导出新文件”。
3. 选择一个与源文件不同的导出文件夹。
4. 程序会保留游戏要求的准确文件名。
5. 关闭游戏，备份 Steam 存档目录中的原同名文件，再用导出文件覆盖它。

常见 Windows 存档位置：

```text
<Steam目录>\userdata\<Steam用户ID>\250900\remote
```

## Steam 云冲突

如果启动 Steam 或游戏时出现云存档冲突，请先确认备份存在，再选择修改时间较新的本地文件。不确定时应取消同步并恢复备份。

## 支持范围

| 版本 | 支持 |
|---|---|
| Repentance | 是 |
| Repentance+ | 是 |
| Afterbirth+ / Rebirth | 否 |
| 主机版 | 否 |

程序只修改伊甸次数和文件尾 CRC，不修改成就、角色标记或其他统计数据。可输入范围为 `0` 到 `4,294,967,295`。

## 从源码构建

需要 Windows 与 .NET 8 SDK：

```powershell
dotnet restore
dotnet test -c Release
dotnet publish src/IsaacEdenTokenEditor.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 隐私与安全

软件没有联网后端。存档字节只在本机内存和用户选择的磁盘位置处理。请勿在 Issue 中上传真实存档；问题报告请参阅 [SECURITY.md](SECURITY.md)。

## 许可证

[MIT](LICENSE)
