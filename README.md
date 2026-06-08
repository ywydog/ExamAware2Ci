# ExamAware2Ci

ExamAware2 与 ClassIsland 联动插件，提供考试事件自动化触发器、放映行动和规则集，让 ClassIsland 能够实时响应 ExamAware2 的考试状态变化。

## 功能

### 自动化触发器

| 触发器 | 说明 | 恢复条件 |
|--------|------|----------|
| **进入考试放映时** | 当 ExamAware2 打开播放器窗口开始放映考试信息时触发 | 放映停止时恢复 |
| **考试开始时** | 当考试正式开始时触发 | 考试结束时恢复 |
| **考试时间剩余提醒时** | 当考试剩余时间到达提醒时间时触发 | 考试结束时恢复 |
| **考试结束时** | 当考试结束时触发 | 无恢复（终态） |

### 自动化行动

| 行动 | 说明 |
|------|------|
| **放映考试信息** | 通过 IPC 控制 ExamAware2 放映考试信息，支持 URL 链接或本地文件路径 |

### 规则集

| 规则 | 说明 |
|------|------|
| **正在考试时** | 当考试正在进行时满足条件，支持按考试名称筛选 |

## 通信方式

ExamAware2Ci 使用双通道与 ExamAware2 通信：

- **WebSocket**（`ws://127.0.0.1:31234/api/v1/ws`）：订阅考试事件，实时接收考试状态变化
- **IPC**（Named Pipe / Unix Socket）：发送控制命令（放映、停止等）

### WebSocket 消息协议

客户端发送订阅请求：
```json
{ "type": "subscribe", "channel": "exam-events" }
```

服务端推送考试事件：
```json
{
  "type": "exam-event",
  "event": "exam-presentation-start | exam-start | exam-time-remaining | exam-end",
  "data": {
    "examName": "语文",
    "examConfigName": "期末考试",
    "startTime": "2025-01-01T08:00:00",
    "endTime": "2025-01-01T09:30:00",
    "remainingMinutes": 15,
    "alertTime": 15
  },
  "timestamp": 1704067200000
}
```

### IPC 协议

通过 Named Pipe（Windows: `\\.\pipe\ExamAware2.examaware2`）或 Unix Socket（Linux: `/tmp/ExamAware2.examaware2.sock`）发送 JSON 命令：

| 命令 | 说明 |
|------|------|
| `ping` | 检测 IPC 连接 |
| `play-from-url` | 通过 URL 放映考试信息 |
| `play-from-file` | 通过本地文件放映考试信息 |
| `stop` | 停止放映 |
| `status` | 获取当前状态 |

## 前置要求

- [ClassIsland](https://github.com/ClassIsland/ClassIsland) >= 2.0
- [ExamAware2](https://github.com/ExamAware/ExamAware2) 运行中并开启了 HTTP API 服务和外部 IPC

## 安装

1. 下载最新版本的插件包
2. 将插件放入 ClassIsland 的插件目录
3. 重启 ClassIsland

或通过 ClassIsland 的插件市场搜索 "ExamAware" 安装。

## 使用

1. 确保 ExamAware2 已启动并开启了 HTTP API 和外部 IPC
2. 启动 ClassIsland，插件将自动连接 ExamAware2
3. 在 ClassIsland 的 **自动化** 设置中添加规则
4. 选择 ExamAware2Ci 提供的触发器、行动或规则集
5. 配置对应的动作

### 示例：考试开始时显示通知

1. 新建自动化规则
2. 触发器选择 **"考试开始时"**
3. 动作选择 **"显示通知"**
4. 通知内容填写：`{examName} 考试已开始`

### 示例：自动放映考试信息

1. 新建自动化规则
2. 触发器选择 **"考试开始时"**
3. 动作选择 **"放映考试信息"**
4. 配置来源类型为 URL，填入考试信息链接

## 项目结构

```
ExamAware2Ci/
├── ExamAware2Ci.sln
├── ExamAware2Ci.csproj
├── Plugin.cs                               # 插件入口
├── manifest.yml                            # 插件清单
├── Automations/
│   ├── Actions/
│   │   └── PlayExamAction.cs               # 放映考试信息行动
│   └── Triggers/
│       ├── ExamPresentationStartTrigger.cs  # 进入考试放映触发器
│       ├── ExamStartTrigger.cs             # 考试开始触发器
│       ├── ExamTimeRemainingTrigger.cs     # 考试时间剩余触发器
│       └── ExamEndTrigger.cs              # 考试结束触发器
├── Controls/
│   └── Automations/
│       ├── ActionSettingsControls/
│       │   └── PlayExamActionSettingsControl.axaml
│       ├── RuleSettingsControls/
│       │   └── ExamPlayingRuleSettingsControl.axaml
│       └── TriggerSettingsControls/
│           └── ExamTimeRemainingTriggerSettingsControl.axaml
├── Models/
│   ├── ExamEventData.cs                    # 考试事件数据模型
│   ├── ExamEventMessage.cs                 # WebSocket 事件消息模型
│   ├── ExamStatusData.cs                   # 考试状态模型
│   ├── Automation/
│   │   └── ExamTimeRemainingTriggerSettings.cs
│   └── Automations/
│       ├── Actions/
│       │   └── PlayExamActionSettings.cs
│       └── Rules/
│           └── ExamPlayingRuleSettings.cs
├── Services/
│   ├── ExamAwareConnectionService.cs       # WebSocket 连接服务
│   └── Automations/
│       └── RuleHandlerService.cs           # 规则处理服务
└── Shared/
    └── ExamAwareIpcClient.cs              # IPC 客户端
```

## 开发

### 环境要求

- .NET 8.0 SDK
- ClassIsland PluginSdk 2.0.0

### 构建

```bash
dotnet build
```

### 调试

将构建输出目录指向 ClassIsland 的插件目录，启动 ClassIsland 即可加载插件。

## 相关项目

- [ExamAware2](https://github.com/ExamAware/ExamAware2) - 考试放映与管理工具
- [ClassIsland](https://github.com/ClassIsland/ClassIsland) - 班级信息显示工具

## 许可证

本项目遵循 ExamAware2 的许可证。
