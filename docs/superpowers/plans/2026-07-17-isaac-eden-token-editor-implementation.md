# 伊甸次数修改器实施计划

## 实施原则

- 使用 C#、.NET 8、WPF、MVVM。
- 存档核心库不引用 WPF，所有二进制行为先写测试再实现。
- 不提交真实玩家存档；测试文件由代码生成。
- 每个任务完成后运行对应测试并单独提交。
- 自动模式只能全自动备份并写回；手动模式只能导出。

## 任务 1：建立解决方案与仓库基础

创建文件：

- `IsaacEdenTokenEditor.sln`
- `src/IsaacEdenTokenEditor.Core/IsaacEdenTokenEditor.Core.csproj`
- `src/IsaacEdenTokenEditor.App/IsaacEdenTokenEditor.App.csproj`
- `tests/IsaacEdenTokenEditor.Core.Tests/IsaacEdenTokenEditor.Core.Tests.csproj`
- `tests/IsaacEdenTokenEditor.App.Tests/IsaacEdenTokenEditor.App.Tests.csproj`
- `Directory.Build.props`
- `.editorconfig`
- `.gitignore`
- `global.json`

步骤：

1. 用 `dotnet new sln`、`dotnet new classlib`、`dotnet new wpf` 和 `dotnet new xunit` 建立项目。
2. Core 目标框架为 `net8.0`；App 为 `net8.0-windows` 并启用 WPF；测试目标与被测项目一致。
3. 开启 nullable、隐式 using、警告视为错误和确定性构建。
4. 建立项目引用：App → Core，测试 → 对应项目。
5. 运行 `dotnet restore` 和 `dotnet build -c Release`。
6. 提交：`build: scaffold .NET solution`。

## 任务 2：建立存档领域模型与合成夹具

创建文件：

- `src/IsaacEdenTokenEditor.Core/Saves/IsaacSaveVersion.cs`
- `src/IsaacEdenTokenEditor.Core/Saves/IsaacSaveInfo.cs`
- `src/IsaacEdenTokenEditor.Core/Saves/SaveValidationError.cs`
- `src/IsaacEdenTokenEditor.Core/Saves/SaveValidationException.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Fixtures/SyntheticSaveBuilder.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Saves/SyntheticSaveBuilderTests.cs`

测试先行：

1. 生成 Repentance 和 Repentance+ 两种最小有效字节数组。
2. 夹具允许设置版本、伊甸次数、区段数量和文件尾 CRC。
3. 验证夹具不包含真实用户数据，且每次生成结果确定。

实现：

- `IsaacSaveVersion` 只包含 `Repentance` 与 `RepentancePlus`。
- `IsaacSaveInfo` 保存版本、当前次数、统计区起点、伊甸字段偏移、文件长度和原 CRC。
- 错误类型区分文件过短、文件头错误、不支持版本、区段越界与 CRC 错误。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release`。

提交：`test: add synthetic Isaac save fixtures`。

## 任务 3：实现 CRC 算法

创建文件：

- `src/IsaacEdenTokenEditor.Core/Saves/IsaacCrc32.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Saves/IsaacCrc32Tests.cs`

测试先行：

1. 固定向量得到确定的 CRC。
2. 有效合成存档的计算值等于文件尾值。
3. 任意修改一个被校验字节后结果不同。
4. 空范围、过短数据和非法边界安全失败。

实现：

- 独立实现游戏使用的 CRC 变体与初始值。
- 计算范围严格为偏移 `0x10` 到文件尾 CRC 前。
- 使用 `ReadOnlySpan<byte>`，不产生不必要的数组副本。
- 提供 `Calculate`、`ReadStored`、`WriteStored` 和 `IsValid`。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release --filter IsaacCrc32Tests`。

提交：`feat: implement Isaac save checksum`。

## 任务 4：实现存档解析器

创建文件：

- `src/IsaacEdenTokenEditor.Core/Saves/IsaacSaveParser.cs`
- `src/IsaacEdenTokenEditor.Core/Saves/IsaacSaveLayout.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Saves/IsaacSaveParserTests.cs`

测试先行：

1. 两种受支持版本均能读取正确伊甸次数。
2. 次数 0、100000、最大无符号 32 位整数均可读取。
3. 拒绝错误文件头、不支持版本、损坏 CRC、截断区段表、超大区段数量和任何越界计算。
4. 所有偏移计算使用 checked 算术，恶意输入不会溢出或分配巨量内存。

实现：

- 验证 `ISAACNGSAVE` 头与受支持版本标识。
- 从 `0x14` 开始解析区段描述，按照版本布局计算区段起点。
- 统计区为第二个区段，伊甸字段为统计区相对偏移 `0x54`。
- 严格验证 CRC 后才返回 `IsaacSaveInfo`。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release --filter IsaacSaveParserTests`。

提交：`feat: parse supported Isaac save formats`。

## 任务 5：实现纯内存伊甸次数修改

创建文件：

- `src/IsaacEdenTokenEditor.Core/Saves/EdenTokenEditor.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Saves/EdenTokenEditorTests.cs`

测试先行：

1. 修改两种版本的目标字段并更新 CRC。
2. 修改后再次解析得到目标值。
3. 除四字节目标字段和四字节 CRC 外，其他字节完全不变。
4. 输入字节数组不被修改；返回新的数组。
5. 源 CRC 无效时拒绝修改。

实现：

- 先调用解析器得到经过验证的字段偏移。
- 克隆数据，使用小端无符号整数写入目标值。
- 更新 CRC，再调用解析器复核输出。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release --filter EdenTokenEditorTests`。

提交：`feat: edit Eden tokens in memory`。

## 任务 6：实现 Steam 存档发现

创建文件：

- `src/IsaacEdenTokenEditor.Core/Discovery/ISteamPathProvider.cs`
- `src/IsaacEdenTokenEditor.Core/Discovery/WindowsSteamPathProvider.cs`
- `src/IsaacEdenTokenEditor.Core/Discovery/SaveDiscoveryService.cs`
- `src/IsaacEdenTokenEditor.Core/Discovery/DiscoveredSave.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Discovery/SaveDiscoveryServiceTests.cs`

测试先行：

1. 默认与自定义 Steam 根目录均能发现 `userdata/*/250900/remote`。
2. 同时识别 `rep_` 与 `rep+` 文件名及三个存档位。
3. 多 userdata ID 分组返回，重复路径去重。
4. 无目录、无权限、无存档或无效存档返回可显示的诊断，不导致整体崩溃。

实现：

- 注册表读取隔离在 `WindowsSteamPathProvider`，测试使用假的路径提供器。
- 发现服务只读文件，并调用解析器生成当前次数与版本。
- 不把历史日期备份误认为活动存档。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release --filter SaveDiscoveryServiceTests`。

提交：`feat: discover Steam Isaac saves`。

## 任务 7：实现备份、导出与原子写回

创建文件：

- `src/IsaacEdenTokenEditor.Core/IO/IFileSystem.cs`
- `src/IsaacEdenTokenEditor.Core/IO/PhysicalFileSystem.cs`
- `src/IsaacEdenTokenEditor.Core/IO/BackupService.cs`
- `src/IsaacEdenTokenEditor.Core/IO/SaveExportService.cs`
- `src/IsaacEdenTokenEditor.Core/IO/AtomicSaveWriter.cs`
- `src/IsaacEdenTokenEditor.Core/IO/WriteResult.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/IO/BackupServiceTests.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/IO/SaveExportServiceTests.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/IO/AtomicSaveWriterTests.cs`

测试先行：

1. 备份目录使用 `Documents/IsaacEdenEditorBackups/yyyyMMdd-HHmmss`，保持原文件名。
2. 备份后 SHA-256 与源文件一致，否则停止。
3. 导出不改变源文件，输出保持准确游戏文件名。
4. 写回先写同目录临时文件、验证、再原子替换。
5. 模拟备份失败、临时写入失败、文件锁定和替换失败时，源文件保持不变。
6. 写回后重新读取并验证目标值、版本、长度和 CRC。

实现：

- `IFileSystem` 使失败路径可确定地测试。
- 自动模式调用 Backup → Edit → Temp write → Parse verify → Replace → Disk verify。
- 手动模式只调用 Edit → Export，不允许传入源路径作为导出目标。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release --filter IO`。

提交：`feat: add verified backup and save writing`。

## 任务 8：实现游戏进程守卫与自动工作流

创建文件：

- `src/IsaacEdenTokenEditor.Core/Processes/IGameProcessGuard.cs`
- `src/IsaacEdenTokenEditor.Core/Processes/WindowsGameProcessGuard.cs`
- `src/IsaacEdenTokenEditor.Core/Workflows/AutomaticEditWorkflow.cs`
- `src/IsaacEdenTokenEditor.Core/Workflows/ManualExportWorkflow.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Workflows/AutomaticEditWorkflowTests.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Workflows/ManualExportWorkflowTests.cs`

测试先行：

1. `isaac-ng.exe` 运行时，自动写回在备份前停止。
2. 扫描与手动导出不受游戏进程限制。
3. 扫描后源文件变化时自动流程要求重新扫描。
4. 自动流程严格按安全步骤执行并返回备份路径。
5. 手动流程永不调用备份服务或原子写回服务。

命令：`dotnet test tests/IsaacEdenTokenEditor.Core.Tests -c Release --filter Workflow`。

提交：`feat: coordinate automatic and manual workflows`。

## 任务 9：实现 WPF MVVM 界面

创建文件：

- `src/IsaacEdenTokenEditor.App/App.xaml`
- `src/IsaacEdenTokenEditor.App/App.xaml.cs`
- `src/IsaacEdenTokenEditor.App/MainWindow.xaml`
- `src/IsaacEdenTokenEditor.App/MainWindow.xaml.cs`
- `src/IsaacEdenTokenEditor.App/ViewModels/MainViewModel.cs`
- `src/IsaacEdenTokenEditor.App/ViewModels/DiscoveredSaveViewModel.cs`
- `src/IsaacEdenTokenEditor.App/ViewModels/OperationState.cs`
- `src/IsaacEdenTokenEditor.App/Commands/AsyncRelayCommand.cs`
- `src/IsaacEdenTokenEditor.App/Services/FileDialogService.cs`
- `src/IsaacEdenTokenEditor.App/Services/FolderLauncher.cs`
- `tests/IsaacEdenTokenEditor.App.Tests/ViewModels/MainViewModelTests.cs`

测试先行：

1. 初始页显示自动扫描和手动选择。
2. 扫描结果按 userdata ID 与存档位展示。
3. 自动发现项只提供“备份并写回”；手动项只提供“仅导出”。
4. 输入验证覆盖空值、非数字、负数、超范围和合法边界。
5. 操作期间命令禁用，成功与失败均进入明确状态。
6. 错误信息映射为中文解决建议，不显示堆栈。

实现：

- 使用系统控件与轻量资源字典，不引入大型 UI 框架。
- 界面实现已批准的单窗口布局。
- 支持按钮选择文件与文件拖放。
- 文件、目录对话框封装为接口，ViewModel 测试不打开 GUI。

命令：`dotnet test tests/IsaacEdenTokenEditor.App.Tests -c Release`。

提交：`feat: add Chinese WPF user interface`。

## 任务 10：帮助内容、隐私与诊断

创建文件：

- `src/IsaacEdenTokenEditor.App/Views/HelpWindow.xaml`
- `src/IsaacEdenTokenEditor.App/ViewModels/HelpViewModel.cs`
- `src/IsaacEdenTokenEditor.App/Diagnostics/DiagnosticSummary.cs`
- `tests/IsaacEdenTokenEditor.App.Tests/Diagnostics/DiagnosticSummaryTests.cs`

内容：

- 默认 Steam 路径、手动替换步骤、备份恢复方法、云冲突处理。
- 离线与隐私说明。
- Windows SmartScreen 与 SHA-256 核对说明。
- 与游戏官方无关联的声明。

测试：诊断摘要不包含完整用户名、Steam ID、文件内容或堆栈；路径必须脱敏。

提交：`docs: add in-app help and privacy guidance`。

## 任务 11：端到端测试与健壮性验证

创建文件：

- `tests/IsaacEdenTokenEditor.Core.Tests/Integration/AutomaticWorkflowIntegrationTests.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Integration/ManualWorkflowIntegrationTests.cs`
- `tests/IsaacEdenTokenEditor.Core.Tests/Fuzz/ParserRobustnessTests.cs`

验证：

1. 在临时目录完成 Repentance 与 Repentance+ 的自动备份、修改、写回与复核。
2. 手动导出结果可再次解析，源文件哈希不变。
3. 对合成文件执行确定性随机截断、位翻转和区段数量突变，解析器只能成功或返回受控错误，不能崩溃、挂起或巨量分配。
4. 全量运行 `dotnet test -c Release`。

提交：`test: cover end-to-end save editing workflows`。

## 任务 12：GitHub 文档、CI 与发布打包

创建文件：

- `README.md`
- `SECURITY.md`
- `LICENSE`
- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `assets/screenshots/` 中的中文界面截图

步骤：

1. README 说明功能、支持范围、下载、自动模式、手动模式、备份恢复、云冲突、SmartScreen、隐私和免责声明。
2. 使用 MIT License。
3. CI 在 Windows 上执行 restore、build、test。
4. Release 工作流对标签构建 `win-x64` 自包含单文件：
   `dotnet publish src/IsaacEdenTokenEditor.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`。
5. 为 `.exe` 生成 SHA-256 并与程序一起上传 Release。
6. 在本机执行相同发布命令，启动程序并用合成夹具完成冒烟测试。
7. 提交：`build: add GitHub release pipeline and documentation`。

## 任务 13：发布前审查与 GitHub 发布

检查清单：

- `git status` 干净。
- Release 构建与全部测试通过。
- 仓库历史中不存在真实存档、用户名、Steam ID 或绝对用户路径。
- README 截图与当前界面一致。
- `.exe` SHA-256 与发布说明一致。
- 自动模式在游戏运行时被阻止。
- 备份恢复步骤实际演练通过。

用户确认仓库名称和公开可见性后，通过已安装的 GitHub 连接创建仓库、推送 `main`、创建首个版本标签与 Release。外部发布是最终单独授权步骤；在用户明确确认前不执行。
