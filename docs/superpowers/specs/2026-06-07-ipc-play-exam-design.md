# IPC 联动放映设计文档

## 概述

实现 ExamAware2 与 ExamAware2Ci 的 IPC 联动放映功能。ExamAware2 新增 Named Pipe / Unix Socket IPC 服务器，ExamAware2Ci 新增 IPC 客户端、"放映考试信息"行动和"正在考试时"规则集，支持通过 URL 或本地文件路径触发 ExamAware2 放映考试信息。

学习 SecRandom/SecRandom-CI 的 IPC 通信模式。

## ExamAware2 端

### IPC 服务器

**新增文件**：`packages/desktop/src/main/ipc/ipcServer.ts`

- 使用 Node.js `net` 模块创建 Named Pipe（Windows）或 Unix Socket（Linux）服务器
- IPC 名称：`ExamAware2.examaware2`
  - Windows：`\\.\pipe\ExamAware2.examaware2`
  - Linux：`/tmp/ExamAware2.examaware2.sock`
- 监听客户端连接，接收 JSON 消息，返回 JSON 响应
- 每个连接独立处理，请求-响应模式

### 消息协议

请求格式：
```json
{ "type": "<command>", "payload": { ... } }
```

响应格式：
```json
{ "success": true, "type": "<command>", "result": { ... } }
{ "success": false, "type": "<command>", "error": "错误信息" }
```

支持的命令：

| type | payload | 说明 |
|---|---|---|
| `ping` | `{}` | 连接检测，返回 `pong` |
| `play-from-url` | `{ "url": "https://..." }` | 从 URL 拉取配置并放映 |
| `play-from-file` | `{ "path": "C:\\exams\\math.ea2" }` | 从本地文件打开放映 |
| `stop` | `{}` | 关闭放映窗口 |
| `status` | `{}` | 获取当前放映状态 |

### 命令处理逻辑

- `ping`：返回 `{ success: true, result: "pong" }`
- `play-from-url`：调用已有的 `openPlayerFromUrl(url)`
- `play-from-file`：读取文件内容 → 调用 `openPlayerFromEditor(data)`
- `stop`：通过 `windowManager` 关闭 player 窗口
- `status`：返回 `examEventService.getExamStatus()`

### 设置开关

- 配置路径：`ipc.enabled`，默认 `false`
- 开启时启动 IPC 服务器，关闭时停止
- 在 ExamAware2 设置界面添加"启用外部 IPC"开关

### 集成

- `packages/desktop/src/main/index.ts`：导入 ipcServer，应用启动时根据配置决定是否启动 IPC 服务器，退出时清理

## ExamAware2Ci 端

### IPC 客户端

**新增文件**：`ExamAware2Ci/Shared/ExamAwareIpcClient.cs`

学习 `SecRandomIpcSendUrl` 模式：

- `DefaultIpcName = "ExamAware2.examaware2"`
- `NormalizeIpcName(ipcName)`：规范化 IPC 名称（去除 `\\.\pipe\` 前缀、`.sock` 后缀，替换特殊字符）
- `GetIpcAddress(ipcName)`：返回平台对应的 IPC 地址
- `SendCommandAsync(type, payload, ipcName, timeout)`：发送命令并返回 `JsonObject` 响应
- Windows：`NamedPipeClientStream`
- Linux：`UnixDomainSocketEndPoint` + `Socket`
- 超时处理（默认 5 秒）、异常处理（连接失败返回 `{ success: false, error: "ipc_not_found" }`）
- `ReadLineAsync(stream, maxBytes, ct)`：逐行读取响应

### PlayExamAction 行动

**新增文件**：`ExamAware2Ci/Automations/Actions/PlayExamAction.cs`

```csharp
[ActionInfo("examaware2ci.actions.playExam", "放映考试信息", "\uE7B8")]
public class PlayExamAction : ActionBase<PlayExamActionSettings>
{
    protected override async Task OnInvoke()
    {
        var type = Settings.SourceType == ExamSourceType.Url
            ? "play-from-url" : "play-from-file";
        var payload = Settings.SourceType == ExamSourceType.Url
            ? new { url = Settings.Source }
            : new { path = Settings.Source };
        var result = await ExamAwareIpcClient.SendCommandAsync(type, payload);
        // 日志记录
    }

    protected override async Task OnRevert()
    {
        await ExamAwareIpcClient.SendCommandAsync("stop", new { });
    }
}
```

### 设置模型

**新增文件**：`ExamAware2Ci/Models/Automations/Actions/PlayExamActionSettings.cs`

```csharp
public enum ExamSourceType { Url, File }

public partial class PlayExamActionSettings : ObservableRecipient
{
    [ObservableProperty] private ExamSourceType _sourceType = ExamSourceType.Url;
    [ObservableProperty] private string _source = "";
}
```

### 设置控件

**新增文件**：`ExamAware2Ci/Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml(.cs)`

- ComboBox：选择来源类型（URL / 本地文件）
- TextBox：输入 URL 或本地路径

### "正在考试时"规则集

**新增文件**：
- `ExamAware2Ci/Models/Automations/Rules/ExamPlayingRuleSettings.cs`
- `ExamAware2Ci/Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml(.cs)`
- `ExamAware2Ci/Services/Automations/RuleHandlerService.cs`

规则 ID：`examaware2ci.rules.examPlaying`
规则名称：正在考试时
规则图标：`\uE7B8`

规则逻辑：判断 ExamAware2 是否正在放映考试信息。当 `ExamAwareConnectionService.LastEventData` 不为空且 `ExamAwareConnectionService.IsConnected` 为 true 时，规则为满足状态。

规则设置：
```csharp
public partial class ExamPlayingRuleSettings : ObservableRecipient
{
    [ObservableProperty] private bool _filterByExamName = false;
    [ObservableProperty] private string _examName = "";
}
```

规则处理逻辑（在 RuleHandlerService 中注册）：
```csharp
private bool HandleExamPlaying(object? objectSettings)
{
    var data = ConnectionService.LastEventData;
    if (data == null || !ConnectionService.IsConnected) return false;

    if (objectSettings is ExamPlayingRuleSettings settings && settings.FilterByExamName)
    {
        return data.ExamName == settings.ExamName;
    }
    return true;
}
```

事件驱动刷新：订阅 `ExamPresentationStart`、`ExamPresentationStop`、`ExamEnd` 事件，调用 `RulesetService.NotifyStatusChanged()` 刷新规则状态。

规则注册（Plugin.cs）：
```csharp
services.AddRule<ExamPlayingRuleSettings, ExamPlayingRuleSettingsControl>(
    "examaware2ci.rules.examPlaying", "正在考试时", "\uE7B8");
```

### Plugin.cs 注册

```csharp
services.AddAction<PlayExamAction, PlayExamActionSettingsControl>();
services.AddRule<ExamPlayingRuleSettings, ExamPlayingRuleSettingsControl>(
    "examaware2ci.rules.examPlaying", "正在考试时", "\uE7B8");
services.AddSingleton<RuleHandlerService>();
```

## 现有触发器检查结果

### ExamPresentationStartTrigger ✅
- Loaded/UnLoaded 正确订阅/取消订阅事件
- TriggerRevert() 通过 ExamPresentationStop 事件触发 ✅
- 日志完整

### ExamStartTrigger ✅
- Loaded/UnLoaded 正确订阅/取消订阅事件
- TriggerRevert() 通过 ExamEnd 事件触发 ✅
- 日志完整

### ExamTimeRemainingTrigger ✅
- Loaded/UnLoaded 正确订阅/取消订阅事件
- TriggerRevert() 通过 ExamEnd 事件触发 ✅
- 使用 TriggerBase<ExamTimeRemainingTriggerSettings> ✅
- 注意：AlertTimeMinutes 设置项存在但未在触发逻辑中使用（当前由 ExamAware2 端控制提醒时间，插件端接收即可）

### ExamEndTrigger ✅
- Loaded/UnLoaded 正确订阅/取消订阅事件
- 无需 TriggerRevert()（终态触发器） ✅
- 日志完整

### ExamAwareConnectionService ✅
- WebSocket 连接/重连逻辑正确
- CleanupWebSocket() 在重连前清理旧实例 ✅
- _isSubscribed 在重连前重置 ✅
- 事件正确触发
- 消息分帧处理正确（do-while 循环拼接）
- 日志系统完整

## 文件清单

| 项目 | 操作 | 文件 |
|---|---|---|
| ExamAware2 | 新增 | `packages/desktop/src/main/ipc/ipcServer.ts` |
| ExamAware2 | 修改 | `packages/desktop/src/main/index.ts` |
| ExamAware2Ci | 新增 | `Shared/ExamAwareIpcClient.cs` |
| ExamAware2Ci | 新增 | `Automations/Actions/PlayExamAction.cs` |
| ExamAware2Ci | 新增 | `Models/Automations/Actions/PlayExamActionSettings.cs` |
| ExamAware2Ci | 新增 | `Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml` |
| ExamAware2Ci | 新增 | `Controls/Automations/ActionSettingsControls/PlayExamActionSettingsControl.axaml.cs` |
| ExamAware2Ci | 新增 | `Models/Automations/Rules/ExamPlayingRuleSettings.cs` |
| ExamAware2Ci | 新增 | `Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml` |
| ExamAware2Ci | 新增 | `Controls/Automations/RuleSettingsControls/ExamPlayingRuleSettingsControl.axaml.cs` |
| ExamAware2Ci | 新增 | `Services/Automations/RuleHandlerService.cs` |
| ExamAware2Ci | 修改 | `Plugin.cs` |

## 错误处理

- IPC 服务器未启动：客户端返回 `{ success: false, error: "ipc_not_found" }`
- URL 格式不正确：服务端返回 `{ success: false, error: "URL 格式不正确" }`
- 文件不存在：服务端返回 `{ success: false, error: "文件不存在" }`
- 超时：客户端返回 `{ success: false, error: "ipc_timeout" }`
- 行动 OnInvoke 失败时记录日志，不抛出异常（避免影响自动化流程）
