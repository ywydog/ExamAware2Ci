# IPC 联动放映实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 ExamAware2 IPC 服务器和 ExamAware2Ci IPC 客户端、放映行动、正在考试时规则集

**Architecture:** ExamAware2 端新增 Named Pipe/Unix Socket IPC 服务器（Node.js net 模块），ExamAware2Ci 端新增 IPC 客户端（C# NamedPipeClientStream/UnixDomainSocketEndPoint）、PlayExamAction 行动和 ExamPlayingRule 规则集

**Tech Stack:** Node.js net 模块、C# System.IO.Pipes、ClassIsland 自动化框架（ActionBase、RuleRegistryInfo）、Avalonia UI

---

## 文件结构

| 操作 | 文件 | 职责 |
|---|---|---|
| 新增 | `ExamAware2/packages/desktop/src/main/ipc/ipcServer.ts` | IPC 服务器，监听 Named Pipe/Unix Socket |
| 修改 | `ExamAware2/packages/desktop/src/main/index.ts` | 集成 IPC 服务器启动/停止 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Shared/ExamAwareIpcClient.cs` | IPC 客户端，发送命令到 ExamAware2 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Automations/Actions/PlayExamAction.cs` | 放映考试信息行动 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Models/Automations/Actions/PlayExamActionSettings.cs` | 行动设置模型 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml` | 行动设置控件 UI |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml.cs` | 行动设置控件代码 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Models/Automations/Rules/ExamPlayingRuleSettings.cs` | 规则设置模型 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml` | 规则设置控件 UI |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml.cs` | 规则设置控件代码 |
| 新增 | `ExamAware4Ci/ExamAware2Ci/Services/Automations/RuleHandlerService.cs` | 规则处理服务 |
| 修改 | `ExamAware4Ci/ExamAware2Ci/Plugin.cs` | 注册行动、规则、服务 |

---

### Task 1: ExamAware2 IPC 服务器

**Files:**
- Create: `ExamAware2/packages/desktop/src/main/ipc/ipcServer.ts`

- [ ] **Step 1: 创建 IPC 服务器模块**

创建 `packages/desktop/src/main/ipc/ipcServer.ts`：

```typescript
import * as net from 'net'
import * as fs from 'fs'
import * as path from 'path'
import { appLogger } from '../logging/winstonLogger'

const IPC_NAME = 'ExamAware2.examaware2'

function getIpcAddress(): { address: string; isWindows: boolean } {
  if (process.platform === 'win32') {
    return { address: `\\\\.\\pipe\\${IPC_NAME}`, isWindows: true }
  }
  return { address: `/tmp/${IPC_NAME}.sock`, isWindows: false }
}

interface IpcRequest {
  type: string
  payload: Record<string, any>
}

interface IpcResponse {
  success: boolean
  type: string
  result?: any
  error?: string
}

class IpcServer {
  private server: net.Server | null = null
  private isRunning = false

  // 命令处理器映射
  private handlers: Map<string, (payload: Record<string, any>) => Promise<any>> = new Map()

  registerHandler(type: string, handler: (payload: Record<string, any>) => Promise<any>) {
    this.handlers.set(type, handler)
  }

  async start(): Promise<boolean> {
    if (this.isRunning) {
      appLogger.warn('[ipc] IPC 服务器已在运行中')
      return true
    }

    const { address, isWindows } = getIpcAddress()

    // Linux 下清理旧的 socket 文件
    if (!isWindows) {
      try {
        fs.unlinkSync(address)
      } catch {
        // 文件不存在，忽略
      }
    }

    return new Promise((resolve) => {
      this.server = net.createServer((socket) => {
        this.handleConnection(socket)
      })

      this.server.on('error', (err: any) => {
        appLogger.error(`[ipc] IPC 服务器错误: ${err.message}`)
        this.isRunning = false
        resolve(false)
      })

      this.server.listen(address, () => {
        this.isRunning = true
        appLogger.info(`[ipc] IPC 服务器已启动，监听: ${address}`)
        resolve(true)
      })
    })
  }

  async stop(): Promise<void> {
    if (!this.server) return

    return new Promise((resolve) => {
      this.server!.close(() => {
        this.isRunning = false
        this.server = null
        appLogger.info('[ipc] IPC 服务器已停止')

        // Linux 下清理 socket 文件
        const { address, isWindows } = getIpcAddress()
        if (!isWindows) {
          try {
            fs.unlinkSync(address)
          } catch {
            // 忽略
          }
        }
        resolve()
      })
    })
  }

  private handleConnection(socket: net.Socket) {
    let buffer = ''

    socket.on('data', (data) => {
      buffer += data.toString('utf-8')

      // 按换行符分割消息
      const lines = buffer.split('\n')
      buffer = lines.pop() || '' // 保留未完成的部分

      for (const line of lines) {
        const trimmed = line.trim()
        if (!trimmed) continue

        this.processMessage(trimmed)
          .then((response) => {
            const responseStr = JSON.stringify(response) + '\n'
            socket.write(responseStr, 'utf-8')
          })
          .catch((err) => {
            const errorResponse: IpcResponse = {
              success: false,
              type: 'unknown',
              error: `处理消息时出错: ${err.message}`
            }
            socket.write(JSON.stringify(errorResponse) + '\n', 'utf-8')
          })
      }
    })

    socket.on('error', (err) => {
      appLogger.debug(`[ipc] 客户端连接错误: ${err.message}`)
    })

    socket.on('close', () => {
      // 连接关闭
    })
  }

  private async processMessage(messageStr: string): Promise<IpcResponse> {
    let request: IpcRequest
    try {
      request = JSON.parse(messageStr)
    } catch {
      return { success: false, type: 'unknown', error: '无效的 JSON 格式' }
    }

    const { type, payload } = request
    appLogger.info(`[ipc] 收到命令: ${type}`)

    // ping 命令
    if (type === 'ping') {
      return { success: true, type: 'ping', result: 'pong' }
    }

    // 查找处理器
    const handler = this.handlers.get(type)
    if (!handler) {
      return { success: false, type, error: `未知的命令类型: ${type}` }
    }

    try {
      const result = await handler(payload || {})
      return { success: true, type, result }
    } catch (err: any) {
      appLogger.error(`[ipc] 命令处理失败: ${type} - ${err.message}`)
      return { success: false, type, error: err.message || '命令处理失败' }
    }
  }

  get IsRunning(): boolean {
    return this.isRunning
  }
}

export const ipcServer = new IpcServer()
export { IPC_NAME }
```

- [ ] **Step 2: 验证文件创建**

确认 `packages/desktop/src/main/ipc/ipcServer.ts` 文件已创建且内容正确。

---

### Task 2: 集成 IPC 服务器到 ExamAware2 主进程

**Files:**
- Modify: `ExamAware2/packages/desktop/src/main/index.ts`

- [ ] **Step 1: 在 index.ts 中集成 IPC 服务器**

在 `packages/desktop/src/main/index.ts` 中添加 IPC 服务器的导入和启动/停止逻辑。

在文件顶部的 import 区域添加：
```typescript
import { ipcServer } from './ipc/ipcServer'
```

在应用启动逻辑中（`app.whenReady()` 回调内，httpApiService 启动之后），添加 IPC 服务器启动：
```typescript
// 启动 IPC 服务器（如果配置启用）
try {
  const ipcEnabled = await getConfig('ipc.enabled')
  if (ipcEnabled) {
    const started = await ipcServer.start()
    if (started) {
      appLogger.info('[app] IPC 服务器已启动')
    }
  }
} catch (err: any) {
  appLogger.warn(`[app] IPC 服务器启动失败: ${err.message}`)
}
```

在应用退出逻辑中（`app.on('before-quit')` 或 `app.on('will-quit')`），添加 IPC 服务器停止：
```typescript
await ipcServer.stop()
```

注意：需要根据 index.ts 的实际结构找到合适的插入位置。IPC 服务器的命令处理器注册需要在 `openPlayerFromUrl` 和 `openPlayerFromEditor` 函数可用之后进行。

- [ ] **Step 2: 注册 IPC 命令处理器**

在 IPC 服务器启动之前，注册命令处理器。这些处理器需要访问 `openPlayerFromUrl`、`openPlayerFromEditor`、`windowManager` 和 `examEventService`。

在 index.ts 中 ipcServer.start() 之前添加：
```typescript
// 注册 IPC 命令处理器
ipcServer.registerHandler('play-from-url', async (payload) => {
  const { url } = payload
  if (!url || typeof url !== 'string') {
    throw new Error('缺少 url 参数')
  }
  return await openPlayerFromUrl(url)
})

ipcServer.registerHandler('play-from-file', async (payload) => {
  const { path: filePath } = payload
  if (!filePath || typeof filePath !== 'string') {
    throw new Error('缺少 path 参数')
  }
  const fs = await import('fs/promises')
  const data = await fs.readFile(filePath, 'utf-8')
  return await openPlayerFromEditor(data)
})

ipcServer.registerHandler('stop', async () => {
  const { windowManager } = await import('./windows/windowManager')
  windowManager.close('player')
  return { stopped: true }
})

ipcServer.registerHandler('status', async () => {
  const { examEventService } = await import('./exam/examEventService')
  return examEventService.getExamStatus()
})
```

注意：`openPlayerFromUrl` 和 `openPlayerFromEditor` 定义在 `ipcHandlers/index.ts` 中，需要将它们导出或在 index.ts 中重新引用。实际实现时需要根据代码结构调整引用方式。

- [ ] **Step 3: 验证修改**

确认 index.ts 中 IPC 服务器的导入、启动、命令注册和停止逻辑正确。

---

### Task 3: ExamAware2Ci IPC 客户端

**Files:**
- Create: `ExamAware4Ci/ExamAware2Ci/Shared/ExamAwareIpcClient.cs`

- [ ] **Step 1: 创建 IPC 客户端**

创建 `ExamAware2Ci/Shared/ExamAwareIpcClient.cs`，学习 `SecRandomIpcSendUrl` 模式：

```csharp
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamAware2Ci.Shared;

public static class ExamAwareIpcClient
{
    public static string DefaultIpcName = "ExamAware2.examaware2";

    public static string NormalizeIpcName(string ipcName)
    {
        var value = (ipcName ?? string.Empty).Trim();
        if (value.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(@"\\.\pipe\".Length);
        }

        if (value.StartsWith("/tmp/", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(".sock", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("/tmp/".Length);
            value = value.Substring(0, value.Length - ".sock".Length);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = "ipc";
        }

        value = value.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
        return value;
    }

    public static string GetIpcAddress(string ipcName)
    {
        var name = NormalizeIpcName(ipcName);
        if (OperatingSystem.IsWindows())
        {
            return $@"\\.\pipe\{name}";
        }

        return $"/tmp/{name}.sock";
    }

    public static TimeSpan ParseTimeout(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return TimeSpan.FromSeconds(5);
        }

        if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            var msPart = text.Substring(0, text.Length - 2).Trim();
            if (double.TryParse(msPart, out var ms) && ms > 0)
            {
                return TimeSpan.FromMilliseconds(ms);
            }

            return TimeSpan.FromSeconds(5);
        }

        if (double.TryParse(text, out var number) && number > 0)
        {
            if (!text.Contains('.') && number >= 1000)
            {
                return TimeSpan.FromMilliseconds(number);
            }

            return TimeSpan.FromSeconds(number);
        }

        return TimeSpan.FromSeconds(5);
    }

    public static async Task<byte[]?> ReadLineAsync(Stream stream, int maxBytes, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var collected = new List<byte>(4096);

        while (collected.Count < maxBytes)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                var b = buffer[i];
                collected.Add(b);
                if (b == (byte)'\n')
                {
                    return collected.ToArray();
                }

                if (collected.Count >= maxBytes)
                {
                    return collected.ToArray();
                }
            }
        }

        return collected.Count == 0 ? null : collected.ToArray();
    }

    public static async Task<JsonObject> SendCommandAsync(
        string type, object? payload = null, string? ipcName = null, TimeSpan? timeout = null)
    {
        ipcName ??= DefaultIpcName;
        timeout ??= TimeSpan.FromSeconds(5);

        var address = GetIpcAddress(ipcName);
        var messageJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["type"] = type,
                ["payload"] = payload ?? new { }
            },
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
        );
        var requestBytes = Encoding.UTF8.GetBytes(messageJson + "\n");

        using var cts = new CancellationTokenSource(timeout.Value);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var pipeName = NormalizeIpcName(ipcName);
                await using var pipe = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: pipeName,
                    direction: PipeDirection.InOut,
                    options: PipeOptions.Asynchronous
                );

                var connectTimeoutMs = (int)Math.Clamp(timeout.Value.TotalMilliseconds, 1, int.MaxValue);
                await pipe.ConnectAsync(connectTimeoutMs, cts.Token);
                await pipe.WriteAsync(requestBytes.AsMemory(0, requestBytes.Length), cts.Token);
                await pipe.FlushAsync(cts.Token);

                var responseLine = await ReadLineAsync(pipe, maxBytes: 1024 * 1024, cts.Token);
                if (responseLine is null || responseLine.Length == 0)
                {
                    return new JsonObject
                    {
                        ["success"] = false,
                        ["error"] = "empty_response"
                    };
                }

                var responseText = Encoding.UTF8.GetString(responseLine).Trim();
                var node = JsonNode.Parse(responseText) as JsonObject;
                return node ?? new JsonObject { ["success"] = false, ["error"] = "invalid_response" };
            }
            else
            {
                var endPoint = new UnixDomainSocketEndPoint(address);
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                await socket.ConnectAsync(endPoint, cts.Token);

                await using var stream = new NetworkStream(socket, ownsSocket: true);
                await stream.WriteAsync(requestBytes.AsMemory(0, requestBytes.Length), cts.Token);
                await stream.FlushAsync(cts.Token);

                var responseLine = await ReadLineAsync(stream, maxBytes: 1024 * 1024, cts.Token);
                if (responseLine is null || responseLine.Length == 0)
                {
                    return new JsonObject
                    {
                        ["success"] = false,
                        ["error"] = "empty_response"
                    };
                }

                var responseText = Encoding.UTF8.GetString(responseLine).Trim();
                var node = JsonNode.Parse(responseText) as JsonObject;
                return node ?? new JsonObject { ["success"] = false, ["error"] = "invalid_response" };
            }
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            ex is OperationCanceledException ||
            ex is SocketException ||
            ex is IOException
        )
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "ipc_not_found",
                ["detail"] = $"IPC 通道不存在或无法连接: {address}. 请确认 ExamAware2 已运行且已启用外部 IPC。"
            };
        }
    }
}
```

- [ ] **Step 2: 验证文件创建**

确认 `ExamAware2Ci/Shared/ExamAwareIpcClient.cs` 文件已创建。

---

### Task 4: PlayExamAction 行动

**Files:**
- Create: `ExamAware4Ci/ExamAware2Ci/Models/Automations/Actions/PlayExamActionSettings.cs`
- Create: `ExamAware4Ci/ExamAware2Ci/Automations/Actions/PlayExamAction.cs`

- [ ] **Step 1: 创建行动设置模型**

创建 `ExamAware2Ci/Models/Automations/Actions/PlayExamActionSettings.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations.Actions;

public enum ExamSourceType
{
    Url,
    File
}

public partial class PlayExamActionSettings : ObservableRecipient
{
    [ObservableProperty] private ExamSourceType _sourceType = ExamSourceType.Url;
    [ObservableProperty] private string _source = "";
}
```

- [ ] **Step 2: 创建行动类**

创建 `ExamAware2Ci/Automations/Actions/PlayExamAction.cs`：

```csharp
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Actions;
using ExamAware2Ci.Shared;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Actions;

[ActionInfo("examaware2ci.actions.playExam", "放映考试信息", "\uE7B8")]
public class PlayExamAction(ILogger<PlayExamAction> logger) : ActionBase<PlayExamActionSettings>
{
    private ILogger<PlayExamAction> Logger { get; } = logger;

    protected override async Task OnInvoke()
    {
        if (string.IsNullOrWhiteSpace(Settings.Source))
        {
            Logger.LogWarning("[ExamAware2Ci]放映考试信息：来源为空，跳过执行");
            return;
        }

        var type = Settings.SourceType == ExamSourceType.Url
            ? "play-from-url" : "play-from-file";
        var payload = Settings.SourceType == ExamSourceType.Url
            ? (object)new { url = Settings.Source }
            : new { path = Settings.Source };

        Logger.LogInformation("[ExamAware2Ci]放映考试信息：类型={Type}, 来源={Source}", type, Settings.Source);

        var result = await ExamAwareIpcClient.SendCommandAsync(type, payload);

        if (result["success"]?.GetValue<bool>() == true)
        {
            Logger.LogInformation("[ExamAware2Ci]放映考试信息：执行成功");
        }
        else
        {
            var error = result["error"]?.GetValue<string>() ?? "未知错误";
            Logger.LogWarning("[ExamAware2Ci]放映考试信息：执行失败 - {Error}", error);
        }
    }

    protected override async Task OnRevert()
    {
        Logger.LogInformation("[ExamAware2Ci]放映考试信息：停止放映");
        var result = await ExamAwareIpcClient.SendCommandAsync("stop");

        if (result["success"]?.GetValue<bool>() != true)
        {
            var error = result["error"]?.GetValue<string>() ?? "未知错误";
            Logger.LogWarning("[ExamAware2Ci]放映考试信息：停止失败 - {Error}", error);
        }
    }
}
```

- [ ] **Step 3: 验证文件创建**

确认两个文件已创建。

---

### Task 5: PlayExamAction 设置控件

**Files:**
- Create: `ExamAware4Ci/ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml`
- Create: `ExamAware4Ci/ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml.cs`

- [ ] **Step 1: 创建设置控件 AXAML**

创建 `ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml`：

```xml
<UserControl x:Class="ExamAware2Ci.Controls.Automations.ActionSettingsControls.PlayExamActionSettingsControl"
             xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:ci="http://classisland.tech/schemas/xaml/core"
             xmlns:local="using:ExamAware2Ci.Controls.Automations.ActionSettingsControls"
             mc:Ignorable="d">
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Spacing="4"
                DataContext="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=local:PlayExamActionSettingsControl}}">
        <TextBlock VerticalAlignment="Center">放映</TextBlock>
        <ComboBox VerticalAlignment="Center"
                  SelectedIndex="{Binding Settings.SourceType}">
            <ComboBoxItem>
                <ci:IconText Glyph="&#xE71B;" Text="URL 链接"/>
            </ComboBoxItem>
            <ComboBoxItem>
                <ci:IconText Glyph="&#xE8E5;" Text="本地文件"/>
            </ComboBoxItem>
        </ComboBox>
        <TextBox VerticalAlignment="Center"
                 Width="300"
                 Watermark="输入 URL 或本地文件路径"
                 Text="{Binding Settings.Source}" />
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: 创建设置控件代码**

创建 `ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml.cs`：

```csharp
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations.Actions;

namespace ExamAware2Ci.Controls.Automations.ActionSettingsControls;

public partial class PlayExamActionSettingsControl : ActionSettingsControlBase<PlayExamActionSettings>
{
    public PlayExamActionSettingsControl()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: 验证文件创建**

确认两个文件已创建。

---

### Task 6: ExamPlayingRule 规则集

**Files:**
- Create: `ExamAware4Ci/ExamAware2Ci/Models/Automations/Rules/ExamPlayingRuleSettings.cs`
- Create: `ExamAware4Ci/ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml`
- Create: `ExamAware4Ci/ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml.cs`
- Create: `ExamAware4Ci/ExamAware2Ci/Services/Automations/RuleHandlerService.cs`

- [ ] **Step 1: 创建规则设置模型**

创建 `ExamAware2Ci/Models/Automations/Rules/ExamPlayingRuleSettings.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations.Rules;

public partial class ExamPlayingRuleSettings : ObservableRecipient
{
    [ObservableProperty] private bool _filterByExamName = false;
    [ObservableProperty] private string _examName = "";
}
```

- [ ] **Step 2: 创建规则设置控件 AXAML**

创建 `ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml`：

```xml
<ci:RuleSettingsControlBase x:Class="ExamAware2Ci.Controls.Automations.RuleSettingsControls.ExamPlayingRuleSettingsControl"
                            x:TypeArguments="ruleSettings:ExamPlayingRuleSettings"
                            xmlns="https://github.com/avaloniaui"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                            xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                            xmlns:ci="http://classisland.tech/schemas/xaml/core"
                            xmlns:local="using:ExamAware2Ci.Controls.Automations.RuleSettingsControls"
                            xmlns:ruleSettings="using:ExamAware2Ci.Models.Automations.Rules"
                            mc:Ignorable="d">
    <ci:RuleSettingsControlBase.Resources>
        <ScrollViewer x:Key="SettingsDrawer" x:Shared="False" Width="400"
                      d:DataContext="{d:DesignInstance local:ExamPlayingRuleSettingsControl}">
            <StackPanel Classes="animated-intro" Spacing="8">
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <CheckBox IsChecked="{Binding Settings.FilterByExamName}" VerticalAlignment="Center">
                        按考试名称筛选
                    </CheckBox>
                    <TextBox Text="{Binding Settings.ExamName}"
                             IsVisible="{Binding Settings.FilterByExamName}"
                             Watermark="输入考试名称"
                             Width="200" />
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </ci:RuleSettingsControlBase.Resources>

    <StackPanel Orientation="Horizontal" VerticalAlignment="Center"
                DataContext="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=local:ExamPlayingRuleSettingsControl}}">
        <Button VerticalAlignment="Center" Theme="{StaticResource TransparentButton}"
                Click="ButtonShowSettings_OnClick">
            <ci:IconText Glyph="&#xEF27;" Text="打开设置…"
                         Foreground="{DynamicResource AccentTextFillColorPrimaryBrush}" />
        </Button>
    </StackPanel>
</ci:RuleSettingsControlBase>
```

- [ ] **Step 3: 创建规则设置控件代码**

创建 `ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using FluentAvalonia.UI.Controls;
using ExamAware2Ci.Models.Automations.Rules;

namespace ExamAware2Ci.Controls.Automations.RuleSettingsControls;

public partial class ExamPlayingRuleSettingsControl : RuleSettingsControlBase<ExamPlayingRuleSettings>
{
    public ExamPlayingRuleSettingsControl()
    {
        InitializeComponent();
    }

    private void ButtonShowSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindResource("SettingsDrawer") is not ContentControl cc) return;
        cc.DataContext = this;
        _ = ShowDrawer(cc);
    }

    private async Task ShowDrawer(Control control, bool isOpenInDialog = false)
    {
        if (!isOpenInDialog &&
            this.GetVisualRoot() is Window window &&
            window.GetType().FullName == "ClassIsland.Views.SettingsWindowNew")
        {
            control.Classes.Remove("in-dialog");
            control.Classes.Add("in-drawer");

            if (control is ContentControl cc)
            {
                cc.Padding = new Avalonia.Thickness(16);
            }
            else
            {
                control.Margin = new Avalonia.Thickness(16);
            }

            SettingsPageBase.OpenDrawerCommand.Execute(control);
        }
        else
        {
            control.Classes.Remove("in-drawer");
            control.Classes.Add("in-dialog");

            if (control.Parent is ContentDialog contentDialog)
            {
                contentDialog.Content = null;
            }

            var dialog = new ContentDialog
            {
                Content = control,
                TitleTemplate = new DataTemplate(),
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary,
                DataContext = this
            };

            await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        }
    }
}
```

- [ ] **Step 4: 创建规则处理服务**

创建 `ExamAware2Ci/Services/Automations/RuleHandlerService.cs`：

```csharp
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ExamAware2Ci.Models.Automations.Rules;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Services.Automations;

public class RuleHandlerService
{
    private readonly ILogger<RuleHandlerService> _logger;
    private readonly IRulesetService _rulesetService;
    private readonly ExamAwareConnectionService _connectionService;

    public RuleHandlerService(
        ILogger<RuleHandlerService> logger,
        IRulesetService rulesetService,
        ExamAwareConnectionService connectionService)
    {
        _logger = logger;
        _rulesetService = rulesetService;
        _connectionService = connectionService;
    }

    public void Register()
    {
        // 订阅考试事件，刷新规则状态
        _connectionService.ExamPresentationStart += (sender, e) =>
        {
            _logger.LogTrace("[ExamAware2Ci]考试放映开始，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        _connectionService.ExamPresentationStop += (sender, e) =>
        {
            _logger.LogTrace("[ExamAware2Ci]考试放映停止，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        _connectionService.ExamEnd += (sender, e) =>
        {
            _logger.LogTrace("[ExamAware2Ci]考试结束，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        _connectionService.ConnectionStateChanged += (sender, connected) =>
        {
            _logger.LogTrace("[ExamAware2Ci]连接状态变化，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        // 注册规则处理器
        _rulesetService.RegisterRuleHandler("examaware2ci.rules.examPlaying", HandleExamPlaying);
        _logger.LogInformation("[ExamAware2Ci]规则处理器已注册");
    }

    private bool HandleExamPlaying(object? objectSettings)
    {
        var data = _connectionService.LastEventData;
        if (data == null || !_connectionService.IsConnected)
        {
            return false;
        }

        if (objectSettings is ExamPlayingRuleSettings settings && settings.FilterByExamName)
        {
            return data.ExamName == settings.ExamName;
        }

        return true;
    }
}
```

- [ ] **Step 5: 验证文件创建**

确认 4 个文件已创建。

---

### Task 7: 更新 Plugin.cs 注册

**Files:**
- Modify: `ExamAware4Ci/ExamAware2Ci/Plugin.cs`

- [ ] **Step 1: 更新 Plugin.cs**

在 `Plugin.cs` 中添加行动、规则和服务的注册。修改后的完整文件：

```csharp
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ExamAware2Ci.Automations.Actions;
using ExamAware2Ci.Automations.Triggers;
using ExamAware2Ci.Controls.Automations.ActionSettingsControls;
using ExamAware2Ci.Controls.Automations.RuleSettingsControls;
using ExamAware2Ci.Controls.Automations.TriggerSettingsControls;
using ExamAware2Ci.Models.Automations.Rules;
using ExamAware2Ci.Services;
using ExamAware2Ci.Services.Automations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci;

[PluginEntrance]
public class Plugin : PluginBase
{
    private ILogger<Plugin>? _logger;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddLogging();

        // 注册 ExamAware 连接服务
        services.AddSingleton<ExamAwareConnectionService>();

        // 注册自动化触发器
        services.AddTrigger<ExamPresentationStartTrigger>();
        services.AddTrigger<ExamStartTrigger>();
        services.AddTrigger<ExamTimeRemainingTrigger, ExamTimeRemainingTriggerSettingsControl>();
        services.AddTrigger<ExamEndTrigger>();

        // 注册自动化行动
        services.AddAction<PlayExamAction, PlayExamActionSettingsControl>();

        // 注册规则集
        services.AddRule<ExamPlayingRuleSettings, ExamPlayingRuleSettingsControl>(
            "examaware2ci.rules.examPlaying", "正在考试时", "\uE7B8");

        // 注册规则处理服务
        services.AddSingleton<RuleHandlerService>();

        // 应用启动后启动连接
        AppBase.Current.AppStarted += (sender, args) =>
        {
            _logger = IAppHost.GetService<ILogger<Plugin>>();
            _logger?.LogInformation("[ExamAware2Ci]插件正在启动...");

            var connectionService = IAppHost.GetService<ExamAwareConnectionService>();

            // 订阅连接状态变化事件
            connectionService.ConnectionStateChanged += (s, connected) =>
            {
                if (connected)
                {
                    _logger?.LogInformation("[ExamAware2Ci]已连接 ExamAware2");
                }
                else
                {
                    _logger?.LogWarning("[ExamAware2Ci]与 ExamAware2 的连接已断开");
                }
            };

            _ = connectionService.StartAsync();

            // 初始化规则处理服务
            var ruleHandlerService = IAppHost.GetService<RuleHandlerService>();
            ruleHandlerService.Register();

            _logger?.LogInformation("[ExamAware2Ci]插件启动完成");
        };

        // 应用停止时断开连接
        AppBase.Current.AppStopping += (sender, args) =>
        {
            _logger?.LogInformation("[ExamAware2Ci]插件正在关闭，断开 ExamAware2 连接...");
            var connectionService = IAppHost.GetService<ExamAwareConnectionService>();
            connectionService.Dispose();
            _logger?.LogInformation("[ExamAware2Ci]插件已关闭");
        };
    }
}
```

- [ ] **Step 2: 验证修改**

确认 Plugin.cs 中新增的 import 和注册逻辑正确。

---

### Task 8: 检查所有修改代码

- [ ] **Step 1: 检查 ExamAware2 端修改**

确认 `packages/desktop/src/main/ipc/ipcServer.ts` 和 `packages/desktop/src/main/index.ts` 的修改正确，无遗漏。

- [ ] **Step 2: 检查 ExamAware2Ci 端所有新增文件**

逐一确认以下文件内容正确：
- `Shared/ExamAwareIpcClient.cs`
- `Automations/Actions/PlayExamAction.cs`
- `Models/Automations/Actions/PlayExamActionSettings.cs`
- `Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml(.cs)`
- `Models/Automations/Rules/ExamPlayingRuleSettings.cs`
- `Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml(.cs)`
- `Services/Automations/RuleHandlerService.cs`
- `Plugin.cs`（修改）

- [ ] **Step 3: 检查现有触发器联动**

确认 4 个触发器（ExamPresentationStartTrigger、ExamStartTrigger、ExamTimeRemainingTrigger、ExamEndTrigger）和 ExamAwareConnectionService 无需修改，联动逻辑正确。

- [ ] **Step 4: 检查 UI 设计**

确认 PlayExamActionSettingsControl 和 ExamPlayingRuleSettingsControl 的 Avalonia UI 设计符合 ClassIsland 风格，参考 SecRandom-CI 的控件模式。
