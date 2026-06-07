using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ExamAware4Ci.Interface.Models;
using ExamAware4Ci.Models;
using Microsoft.Extensions.Logging;

namespace ExamAware4Ci.Services;

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
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _ = RunConnectionLoop(_cts.Token);
    }

    /// <summary>
    /// 停止连接
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None);
            }
            catch { /* ignore */ }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        _cts?.Dispose();
        _cts = null;
        SetConnected(false);
    }

    private async Task RunConnectionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                _logger.LogInformation("正在连接 ExamAware2: {Url}", _serverUrl);

                await _webSocket.ConnectAsync(new Uri(_serverUrl), ct);
                SetConnected(true);
                _logger.LogInformation("已连接 ExamAware2");

                // 订阅考试事件
                await SubscribeAsync(ct);

                // 接收消息循环
                await ReceiveMessagesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ExamAware2 连接断开，5秒后重连");
                SetConnected(false);
            }

            if (!ct.IsCancellationRequested)
            {
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
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        var subscribeMsg = JsonSerializer.Serialize(new
        {
            type = "subscribe",
            channel = "exam-events"
        });
        var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        _logger.LogInformation("已发送考试事件订阅请求");
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
                    _logger.LogInformation("ExamAware2 关闭了连接");
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
                    if (eventMsg?.Data == null) return;

                    LastEventData = eventMsg.Data;

                    switch (eventMsg.Event)
                    {
                        case "exam-presentation-start":
                            _logger.LogInformation("考试放映开始: {Name}", eventMsg.Data.ExamName);
                            ExamPresentationStart?.Invoke(this, eventMsg.Data);
                            break;
                        case "exam-start":
                            _logger.LogInformation("考试开始: {Name}", eventMsg.Data.ExamName);
                            ExamStart?.Invoke(this, eventMsg.Data);
                            break;
                        case "exam-time-remaining":
                            _logger.LogInformation("考试时间剩余: {Name}, 剩余 {Min} 分钟", eventMsg.Data.ExamName, eventMsg.Data.RemainingMinutes);
                            ExamTimeRemaining?.Invoke(this, eventMsg.Data);
                            break;
                        case "exam-end":
                            _logger.LogInformation("考试结束: {Name}", eventMsg.Data.ExamName);
                            ExamEnd?.Invoke(this, eventMsg.Data);
                            break;
                    }
                    break;

                case "exam-status":
                    if (root.TryGetProperty("data", out var dataElement))
                    {
                        CurrentStatus = JsonSerializer.Deserialize<ExamStatusData>(dataElement.GetRawText());
                    }
                    break;

                case "subscribed":
                    _isSubscribed = true;
                    _logger.LogInformation("已成功订阅考试事件");
                    break;

                case "welcome":
                    _logger.LogInformation("收到 ExamAware2 欢迎消息");
                    break;

                case "pong":
                    // 心跳响应，忽略
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理 ExamAware2 消息失败: {Json}", json);
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
        _ = StopAsync();
    }
}
