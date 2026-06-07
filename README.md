# ExamAware2Ci

ExamAware2 与 ClassIsland 联动插件，提供考试事件自动化触发器，让 ClassIsland 能够实时响应 ExamAware2 的考试状态变化。

## 功能

提供 4 个 ClassIsland 自动化触发器：

| 触发器 | 说明 |
|--------|------|
| **进入考试放映时** | 当 ExamAware2 打开播放器窗口开始放映考试信息时触发 |
| **考试开始时** | 当考试正式开始时触发 |
| **考试时间剩余提醒时** | 当考试剩余时间到达提醒时间时触发 |
| **考试结束时** | 当考试结束时触发 |

## 通信方式

ExamAware2Ci 通过 WebSocket 连接 ExamAware2 的 HTTP API 服务（默认 `ws://127.0.0.1:31234/api/v1/ws`），订阅考试事件频道，实时接收考试状态变化。

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

### HTTP API 端点

| 端点 | 说明 |
|------|------|
| `GET /api/v1/exam/status` | 获取当前考试完整状态 |
| `GET /api/v1/exam/current` | 获取当前进行中的考试 |
| `GET /api/v1/exam/list` | 获取考试列表 |

## 前置要求

- [ClassIsland](https://github.com/ClassIsland/ClassIsland) >= 2.0
- [ExamAware2](https://github.com/ExamAware/ExamAware2) 运行中并开启了 HTTP API 服务

## 安装

1. 下载最新版本的插件包
2. 将插件放入 ClassIsland 的插件目录
3. 重启 ClassIsland

或通过 ClassIsland 的插件市场搜索 "ExamAware" 安装。

## 使用

1. 确保 ExamAware2 已启动并开启了 HTTP API
2. 启动 ClassIsland，插件将自动连接 ExamAware2
3. 在 ClassIsland 的 **自动化** 设置中添加规则
4. 选择 ExamAware2Ci 提供的触发器
5. 配置对应的动作（如显示通知、播放提示音等）

### 示例：考试开始时显示通知

1. 新建自动化规则
2. 触发器选择 **"考试开始时"**
3. 动作选择 **"显示通知"**
4. 通知内容填写：`{examName} 考试已开始`

## 项目结构

```
ExamAware2Ci/
├── ExamAware2Ci.sln
├── ExamAware2Ci/
│   ├── ExamAware2Ci.csproj
│   ├── Plugin.cs                          # 插件入口
│   ├── manifest.yml                       # 插件清单
│   ├── Services/
│   │   └── ExamAwareConnectionService.cs  # WebSocket 连接服务
│   ├── Models/
│   │   ├── ExamEventMessage.cs            # 事件消息模型
│   │   ├── ExamStatusData.cs             # 考试状态模型
│   │   └── Automation/
│   │       └── ExamTimeRemainingTriggerSettings.cs
│   ├── Automations/
│   │   └── Triggers/
│   │       ├── ExamPresentationStartTrigger.cs
│   │       ├── ExamStartTrigger.cs
│   │       ├── ExamTimeRemainingTrigger.cs
│   │       └── ExamEndTrigger.cs
│   └── Controls/
│       └── Automations/
│           └── TriggerSettingsControls/
│               └── ExamTimeRemainingTriggerSettingsControl.axaml
└── ExamAware2Ci.Interface/
    ├── ExamAware2Ci.Interface.csproj
    └── Models/
        └── ExamEventData.cs               # 共享事件数据模型
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
