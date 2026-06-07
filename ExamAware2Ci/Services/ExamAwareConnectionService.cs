using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ExamAware2Ci.Interface.Models;
using ExamAware2Ci.Models;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Services;

/// <summary>
/// ExamAware2 WebSocket 连接服务
/// </summary>
public class ExamAwareConnectionService : IDisposable
{
    private readonly ILogger<ExamAwareConnectionService> _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private readonly string _serverUrl;
    private bool _isConnected;
    private bool _isSubscribed;
    private int _reconnectAttempt;
    private DateTime _lastConnectedAt;

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// 考试放映开始事件
    /// </summary>
    public event EventHandler<ExamEventData>? ExamPresentationStart;

    /// <summary>
    /// 考试开始事件
    /// </summary>
    public event EventHandler<ExamEventData>? ExamStart;

    /// <summary>
    /// 考试时间剩余事件
    /// </summary>
    public event EventHandler<ExamEventData>? ExamTimeRemaining;

    /// <summary>
    /// 考试结束事件
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
        _serverUrl = "ws://127.0.0.1:31234/api/v1/ws";
    }

    /// <summary>
    /// 启动连接
    /// </summary>
    public async Task StartAsync()
    {
        if (_cts != null)
        {
            _logger.LogWarning("[ExamAware2Ci]连接服务已在运行中");
            return;
        }

        _logger.LogInformation("[ExamAware2Ci]正在启动 WebSocket 连接服务，目标: {Url}", _serverUrl);
        _cts = new CancellationTokenSource();
        _ = RunConnectionLoop(_cts.Token);
    }

    /// <summary>
    /// 停止连接
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("[ExamAware2Ci]正在停止 WebSocket 连接服务...");

        _cts?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None);
                _logger.LogInformation("[ExamAware2Ci]WebSocket 连接已正常关闭");
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[ExamAware2Ci]关闭 WebSocket 连接时出错: {Error}", ex.Message);
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        _cts?.Dispose();
        _cts = null;
        SetConnected(false);
        _isSubscribed = false;
    }

    private async Task RunConnectionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _reconnectAttempt++;
                _webSocket = new ClientWebSocket();
                _logger.LogInformation("[ExamAware2Ci]正在连接 ExamAware2 (第 {Attempt} 次): {Url}", _reconnectAttempt, _serverUrl);

                await _webSocket.ConnectAsync(new Uri(_serverUrl), ct);
                _lastConnectedAt = DateTime.Now;
                _reconnectAttempt = 0;
                SetConnected(true);
                _logger.LogInformation("[ExamAware2Ci]已成功连接 ExamAware2，WebSocket 状态: {State}", _webSocket.State);

                // 订阅考试事件
                await SubscribeAsync(ct);

                // 接收消息循环
                await ReceiveMessagesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation("[ExamAware2Ci]连接循环已取消");
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning("[ExamAware2Ci]WebSocket 连接异常 (第 {Attempt} 次重连): {Error}", _reconnectAttempt + 1, ex.Message);
                SetConnected(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("[ExamAware2Ci]无法连接 ExamAware2 (服务可能未启动): {Error}", ex.Message);
                SetConnected(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ExamAware2Ci]ExamAware2 连接断开，5秒后重连 (第 {Attempt} 次)", _reconnectAttempt + 1);
                SetConnected(false);
            }

            if (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("[ExamAware2Ci]等待 5 秒后重连...");
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

        _logger.LogInformation("[ExamAware2Ci]连接循环已退出");
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            _logger.LogWarning("[ExamAware2Ci]无法订阅考试事件：WebSocket 未连接");
            return;
        }

        var subscribeMsg = JsonSerializer.Serialize(new
        {
            type = "subscribe",
            channel = "exam-events"
        });
        var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        _logger.LogInformation("[ExamAware2Ci]已发送考试事件订阅请求");
    }

    private async Task ReceiveMessagesAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        while (_webSocket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            messageBuilder.Clear();

            do
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("[ExamAware2Ci]ExamAware2 服务端关闭了连接 (关闭码: {CloseCode})", result.CloseStatus);
                    return;
                }

                var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(chunk);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = messageBuilder.ToString();
                ProcessMessage(json);
            }
        }

        if (_webSocket?.State != WebSocketState.Open)
        {
            _logger.LogWarning("[ExamAware2Ci]WebSocket 连接已断开，状态: {State}", _webSocket?.State);
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "exam-event":
                    var eventMsg = JsonSerializer.Deserialize<ExamEventMessage>(json);
                    if (eventMsg?.Data == null)
                    {
                        _logger.LogWarning("[ExamAware2Ci]收到无效的考试事件消息");
                        return;
                    }

                    LastEventData = eventMsg.Data;

                    switch (eventMsg.Event)
                    {
                        case "exam-presentation-start":
                            _logger.LogInformation("[ExamAware2Ci]考试放映开始: {Name} (配置: {Config})", eventMsg.Data.ExamName, eventMsg.Data.ExamConfigName);
                            ExamPresentationStart?.Invoke(this, eventMsg.Data);
                            break;
                        case "exam-start":
                            _logger.LogInformation("[ExamAware2Ci]考试开始: {Name} (开始: {Start}, 结束: {End})", eventMsg.Data.ExamName, eventMsg.Data.StartTime, eventMsg.Data.EndTime);
                            ExamStart?.Invoke(this, eventMsg.Data);
                            break;
                        case "exam-time-remaining":
                            _logger.LogInformation("[ExamAware2Ci]考试时间剩余: {Name}, 剩余 {Min} 分钟 (提醒时间: {Alert} 分钟)", eventMsg.Data.ExamName, eventMsg.Data.RemainingMinutes, eventMsg.Data.AlertTime);
                            ExamTimeRemaining?.Invoke(this, eventMsg.Data);
                            break;
                        case "exam-end":
                            _logger.LogInformation("[ExamAware2Ci]考试结束: {Name}", eventMsg.Data.ExamName);
                            ExamEnd?.Invoke(this, eventMsg.Data);
                            break;
                        default:
                            _logger.LogDebug("[ExamAware2Ci]收到未知考试事件: {Event}", eventMsg.Event);
                            break;
                    }
                    break;

                case "exam-status":
                    if (root.TryGetProperty("data", out var dataElement))
                    {
                        CurrentStatus = JsonSerializer.Deserialize<ExamStatusData>(dataElement.GetRawText());
                        _logger.LogDebug("[ExamAware2Ci]收到考试状态更新: 正在放映={IsPlaying}, 当前考试={Exam}", CurrentStatus?.IsPlaying, CurrentStatus?.CurrentExam?.Name);
                    }
                    break;

                case "subscribed":
                    _isSubscribed = true;
                    _logger.LogInformation("[ExamAware2Ci]已成功订阅考试事件频道");
                    break;

                case "welcome":
                    _logger.LogInformation("[ExamAware2Ci]收到 ExamAware2 欢迎消息");
                    break;

                case "pong":
                    _logger.LogTrace("[ExamAware2Ci]收到心跳响应");
                    break;

                default:
                    _logger.LogDebug("[ExamAware2Ci]收到未知消息类型: {Type}", type);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[ExamAware2Ci]解析 ExamAware2 消息失败 (JSON 格式错误): {Json}", json.Length > 200 ? json[..200] + "..." : json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ExamAware2Ci]处理 ExamAware2 消息失败: {Json}", json.Length > 200 ? json[..200] + "..." : json);
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            ConnectionStateChanged?.Invoke(this, connected);
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("[ExamAware2Ci]正在释放连接服务资源...");
        _ = StopAsync();
    }
}
