using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ExamAware2Ci.Models;
using ExamAware2Ci.Shared;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Services;

/// <summary>
/// ExamAware2 IPC 连接服务 - 通过外部 IPC 长连接接收考试事件
/// ExamAware2 在关键事件发生时主动推送事件给已订阅的 IPC 客户端
/// </summary>
public class ExamAwareConnectionService : IDisposable
{
    private readonly ILogger<ExamAwareConnectionService> _logger;
    private CancellationTokenSource? _cts;
    private Stream? _currentStream;
    private readonly object _streamLock = new();
    private bool _isConnected;
    private bool _isSubscribed;
    private int _reconnectAttempt;

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// 考试放映开始事件
    /// </summary>
    public event EventHandler<ExamEventData>? ExamPresentationStart;

    /// <summary>
    /// 考试放映停止事件（用于触发器恢复）
    /// </summary>
    public event EventHandler<ExamEventData>? ExamPresentationStop;

    /// <summary>
    /// 考试开始事件
    /// </summary>
    public event EventHandler<ExamEventData>? ExamStart;

    /// <summary>
    /// 考试时间剩余事件
    /// </summary>
    public event EventHandler<ExamEventData>? ExamTimeRemaining;

    /// <summary>
    /// 考试结束事件（用于触发器恢复考试开始）
    /// </summary>
    public event EventHandler<ExamEventData>? ExamEnd;

    /// <summary>
    /// 最近一次考试事件数据
    /// </summary>
    public ExamEventData? LastEventData { get; private set; }

    /// <summary>
    /// 当前考试状态
    /// </summary>
    public ExamStatusData? CurrentStatus { get; private set; }

    /// <summary>
    /// 是否正在放映考试信息
    /// </summary>
    public bool IsPresentationActive { get; private set; }

    /// <summary>
    /// 是否正在考试进行中
    /// </summary>
    public bool IsExamActive { get; private set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// 是否已订阅考试事件
    /// </summary>
    public bool IsSubscribed => _isSubscribed;

    /// <summary>
    /// 重连尝试次数
    /// </summary>
    public int ReconnectAttempt => _reconnectAttempt;

    public ExamAwareConnectionService(ILogger<ExamAwareConnectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 启动连接
    /// </summary>
    public async Task StartAsync()
    {
        if (_cts != null)
        {
            _logger.LogWarning("连接服务已在运行中");
            return;
        }

        _logger.LogInformation("正在启动 IPC 连接服务");
        _cts = new CancellationTokenSource();
        _ = RunConnectionLoop(_cts.Token);
    }

    /// <summary>
    /// 停止连接
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("正在停止 IPC 连接服务...");

        _cts?.Cancel();
        CleanupStream();
        _cts?.Dispose();
        _cts = null;
        SetConnected(false);
        _isSubscribed = false;
        await Task.CompletedTask;
    }

    private void CleanupStream()
    {
        lock (_streamLock)
        {
            if (_currentStream != null)
            {
                try
                {
                    _currentStream.Close();
                    _currentStream.Dispose();
                }
                catch { /* ignore */ }
                _currentStream = null;
            }
        }
    }

    private async Task RunConnectionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _reconnectAttempt++;
                _isSubscribed = false;
                var ipcAddress = ExamAwareIpcClient.GetIpcAddress(ExamAwareIpcClient.DefaultIpcName);

                _logger.LogInformation("正在连接 ExamAware2 IPC (第 {Attempt} 次): {Address}", _reconnectAttempt, ipcAddress);

                Stream stream;
                if (OperatingSystem.IsWindows())
                {
                    var pipeName = ExamAwareIpcClient.NormalizeIpcName(ExamAwareIpcClient.DefaultIpcName);
                    var pipe = new NamedPipeClientStream(
                        serverName: ".",
                        pipeName: pipeName,
                        direction: PipeDirection.InOut,
                        options: PipeOptions.Asynchronous
                    );
                    await pipe.ConnectAsync(5000, ct);
                    stream = pipe;
                }
                else
                {
                    var address = ExamAwareIpcClient.GetIpcAddress(ExamAwareIpcClient.DefaultIpcName);
                    var endPoint = new UnixDomainSocketEndPoint(address);
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(endPoint, ct);
                    stream = new NetworkStream(socket, ownsSocket: true);
                }

                lock (_streamLock)
                {
                    _currentStream = stream;
                }
                _reconnectAttempt = 0;
                SetConnected(true);
                _logger.LogInformation("已成功连接 ExamAware2 IPC");

                // 发送订阅考试事件命令
                await SubscribeAsync(stream, ct);

                // 接收消息循环（同时处理服务端推送的事件和命令响应）
                await ReceiveMessagesAsync(stream, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation("连接循环已取消");
                break;
            }
            catch (Exception ex) when (ex is TimeoutException or SocketException or IOException or FileNotFoundException)
            {
                _logger.LogWarning("无法连接 ExamAware2 IPC (服务可能未启动或未启用外部 IPC): {Error}", ex.Message);
                SetConnected(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ExamAware2 IPC 连接断开，5秒后重连 (第 {Attempt} 次)", _reconnectAttempt + 1);
                SetConnected(false);
            }
            finally
            {
                CleanupStream();
            }

            if (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("等待 5 秒后重连...");
                try
                {
                    await Task.Delay(5000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("连接循环已退出");
    }

    private async Task SubscribeAsync(Stream stream, CancellationToken ct)
    {
        var subscribeMsg = JsonSerializer.Serialize(new
        {
            type = "subscribe-events",
            payload = new { }
        });
        var bytes = Encoding.UTF8.GetBytes(subscribeMsg + "\n");
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
        _logger.LogInformation("已发送考试事件订阅请求");
    }

    private async Task ReceiveMessagesAsync(Stream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var lineBytes = await ExamAwareIpcClient.ReadLineAsync(stream, maxBytes: 1024 * 1024, ct);
                if (lineBytes == null || lineBytes.Length == 0)
                {
                    _logger.LogWarning("IPC 连接已关闭（收到空数据）");
                    return;
                }

                var json = Encoding.UTF8.GetString(lineBytes).Trim();
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogTrace("收到空行，跳过");
                    continue;
                }

                _logger.LogTrace("收到 IPC 消息: {Json}", json.Length > 200 ? json[..200] + "..." : json);
                ProcessMessage(json);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPC 接收消息异常，连接可能已断开");
                return;
            }
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                _logger.LogWarning("收到的 IPC 消息缺少 type 字段: {Json}", json[..Math.Min(200, json.Length)]);
                return;
            }

            var type = typeElement.GetString();
            if (string.IsNullOrEmpty(type))
            {
                _logger.LogWarning("收到的 IPC 消息 type 为空");
                return;
            }

            // 服务端推送的考试事件
            if (type == "exam-event")
            {
                var eventMsg = JsonSerializer.Deserialize<ExamEventMessage>(json, _jsonOptions);
                if (eventMsg?.Data == null)
                {
                    _logger.LogWarning("收到无效的考试事件消息: {Json}", json[..Math.Min(200, json.Length)]);
                    return;
                }

                LastEventData = eventMsg.Data;

                switch (eventMsg.Event)
                {
                    case "exam-presentation-start":
                        IsPresentationActive = true;
                        _logger.LogInformation("考试放映开始: {Name} (配置: {Config})", eventMsg.Data.ExamName, eventMsg.Data.ExamConfigName);
                        ExamPresentationStart?.Invoke(this, eventMsg.Data);
                        break;
                    case "exam-presentation-stop":
                        IsPresentationActive = false;
                        _logger.LogInformation("考试放映停止: {Name}", eventMsg.Data.ExamName);
                        ExamPresentationStop?.Invoke(this, eventMsg.Data);
                        break;
                    case "exam-start":
                        IsExamActive = true;
                        _logger.LogInformation("考试开始: {Name} (开始: {Start}, 结束: {End})", eventMsg.Data.ExamName, eventMsg.Data.StartTime, eventMsg.Data.EndTime);
                        ExamStart?.Invoke(this, eventMsg.Data);
                        break;
                    case "exam-time-remaining":
                        _logger.LogInformation("考试时间剩余: {Name}, 剩余 {Min} 分钟 (提醒时间: {Alert} 分钟)", eventMsg.Data.ExamName, eventMsg.Data.RemainingMinutes, eventMsg.Data.AlertTime);
                        ExamTimeRemaining?.Invoke(this, eventMsg.Data);
                        break;
                    case "exam-end":
                        IsExamActive = false;
                        _logger.LogInformation("考试结束: {Name}", eventMsg.Data.ExamName);
                        ExamEnd?.Invoke(this, eventMsg.Data);
                        break;
                    default:
                        _logger.LogDebug("收到未知考试事件: {Event}", eventMsg.Event);
                        break;
                }
                return;
            }

            // 命令响应
            switch (type)
            {
                case "subscribe-events":
                    _isSubscribed = true;
                    _logger.LogInformation("已成功订阅考试事件");
                    break;

                case "ping":
                    _logger.LogTrace("收到 ping 响应");
                    break;

                case "status":
                    if (root.TryGetProperty("result", out var resultElement))
                    {
                        CurrentStatus = JsonSerializer.Deserialize<ExamStatusData>(resultElement.GetRawText(), _jsonOptions);
                        _logger.LogDebug("收到考试状态: 正在放映={IsPlaying}", CurrentStatus?.IsPlaying);
                    }
                    break;

                default:
                    _logger.LogDebug("收到 IPC 响应: {Type}", type);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "解析 ExamAware2 消息失败 (JSON 格式错误): {Json}", json.Length > 200 ? json[..200] + "..." : json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理 ExamAware2 消息失败: {Json}", json.Length > 200 ? json[..200] + "..." : json);
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            if (!connected)
            {
                IsExamActive = false;
                IsPresentationActive = false;
                _isSubscribed = false;
            }
            ConnectionStateChanged?.Invoke(this, connected);
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("正在释放连接服务资源...");
        _ = StopAsync();
    }
}
