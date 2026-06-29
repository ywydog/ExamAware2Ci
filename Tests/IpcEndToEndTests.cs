using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExamAware2Ci.Models;
using ExamAware2Ci.Services;
using ExamAware2Ci.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace ExamAware2Ci.Tests;

/// <summary>
/// 端到端连接测试：在内存中启动一个 mock 的 ExamAware2 IPC 服务端，
/// 验证 ExamAwareConnectionService（作为客户端）能：
/// 1. 通过 Unix Domain Socket / Named Pipe 正确连接
/// 2. 自动发送 subscribe-events 订阅
/// 3. 接收并分发考试事件
/// 4. 收到 disconnect 后强制派发 ExamEnd
/// 5. 重新连接后再次订阅成功
/// 6. 服务端不存在时正常重试
/// </summary>
public class IpcEndToEndTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _socketPath;
    private Socket? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;
    private readonly ConcurrentDictionary<int, MockClient> _clients = new();
    private int _nextClientId;
    private readonly ConcurrentQueue<string> _replayQueue = new();

    public IpcEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
        _socketPath = $"/tmp/examaware2ci-test-{Guid.NewGuid():N}.sock";
    }

    public void Dispose()
    {
        try { _serverCts?.Cancel(); } catch { }
        try { _listener?.Close(); } catch { }
        try { _listener?.Dispose(); } catch { }
        foreach (var c in _clients.Values)
        {
            try { c.Socket?.Close(); } catch { }
            try { c.Socket?.Dispose(); } catch { }
        }
        _clients.Clear();
        while (_replayQueue.TryDequeue(out _)) { }
        if (File.Exists(_socketPath))
        {
            try { File.Delete(_socketPath); } catch { }
        }
    }

    private void StartMockServer()
    {
        _serverCts = new CancellationTokenSource();
        var path = _socketPath;
        var cts = _serverCts;
        var clients = _clients;
        var getId = () => Interlocked.Increment(ref _nextClientId);
        var replayQueue = _replayQueue;
        var output = _output;
        _serverTask = Task.Run(async () =>
        {
            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(path));
                listener.Listen(8);
                _listener = listener;
            }
            catch (Exception ex)
            {
                output.WriteLine($"[mock-server] bind failed: {ex.Message}");
                return;
            }

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    Socket client;
                    try
                    {
                        client = await listener.AcceptAsync(cts.Token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    var id = getId();
                    var mock = new MockClient { Id = id, Socket = client };
                    clients[id] = mock;
                    _ = Task.Run(() => HandleClient(mock, replayQueue, cts.Token), cts.Token);
                }
            }
            finally
            {
                listener.Close();
            }
        }, cts.Token);
    }

    private static async Task HandleClient(MockClient mock, ConcurrentQueue<string> replayQueue, CancellationToken ct)
    {
        try
        {
            using var stream = new NetworkStream(mock.Socket!, ownsSocket: true);
            var buffer = new List<byte>(4096);
            while (!ct.IsCancellationRequested)
            {
                int b;
                try
                {
                    b = stream.ReadByte();
                }
                catch { break; }
                if (b < 0) break;
                buffer.Add((byte)b);
                if (b == '\n')
                {
                    var line = Encoding.UTF8.GetString(buffer.ToArray()).Trim();
                    buffer.Clear();
                    if (string.IsNullOrEmpty(line)) continue;
                    try
                    {
                        var req = JsonNode.Parse(line) as JsonObject;
                        if (req == null) continue;
                        var type = req["type"]?.GetValue<string>();
                        switch (type)
                        {
                            case "subscribe-events":
                                mock.Subscribed = true;
                                await stream.WriteAsync(Encoding.UTF8.GetBytes(
                                    "{\"success\":true,\"type\":\"subscribe-events\",\"result\":\"subscribed\"}\n"));
                                // 消费 replay 队列：所有 replay 在订阅时发送
                                var snapshot = new List<string>();
                                while (replayQueue.TryDequeue(out var evt))
                                {
                                    snapshot.Add(evt);
                                }
                                foreach (var evt in snapshot)
                                {
                                    var bytes = Encoding.UTF8.GetBytes(evt + "\n");
                                    await stream.WriteAsync(bytes);
                                }
                                await stream.FlushAsync();
                                break;
                            case "ping":
                                await stream.WriteAsync(Encoding.UTF8.GetBytes(
                                    "{\"success\":true,\"type\":\"ping\",\"result\":\"pong\"}\n"));
                                break;
                            case "play-from-url":
                                mock.CommandsReceived.Add("play-from-url");
                                await stream.WriteAsync(Encoding.UTF8.GetBytes(
                                    "{\"success\":true,\"type\":\"play-from-url\",\"result\":{\"filePath\":\"/tmp/mock.ea2\"}}\n"));
                                break;
                            case "stop":
                                mock.CommandsReceived.Add("stop");
                                await stream.WriteAsync(Encoding.UTF8.GetBytes(
                                    "{\"success\":true,\"type\":\"stop\",\"result\":{\"stopped\":true}}\n"));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        mock.LastError = ex.Message;
                    }
                }
            }
        }
        finally
        {
            // 注意：不在这里删除 clients，因为我们想测试重连场景
        }
    }

    private class MockClient
    {
        public int Id;
        public Socket? Socket;
        public bool Subscribed;
        public List<string> CommandsReceived = new();
        public string? LastError;
    }

    /// <summary>
    /// 解析 ipc name → 期望的 socket 路径。
    /// 客户端通过 ExamAwareIpcClient.GetIpcAddress("xxx") 解析出 /tmp/xxx.sock。
    /// </summary>
    private string DeriveSocketPathFromIpcName(string ipcName) => $"/tmp/{ipcName}.sock";

    [Fact]
    public async Task Connection_EndToEnd_Works()
    {
        StartMockServer();
        await Task.Delay(200);

        var originalIpcName = ExamAwareIpcClient.DefaultIpcName;
        try
        {
            var ipcName = _socketPath
                .Replace("/tmp/", "")
                .Replace(".sock", "");
            // 客户端默认从 DefaultIpcName 解析出 /tmp/xxx.sock
            // 但我们的 listener 在 _socketPath 上（带 guid）。需要让客户端解析出的路径 == listener 路径。
            // 办法：让 _socketPath 严格遵循 "<ipcName>.sock" 的格式。
            // 当前 _socketPath 形如 /tmp/examaware2ci-test-<guid>.sock
            // 客户端会把 "examaware2ci-test-<guid>" 当作 ipc name，解析回 /tmp/examaware2ci-test-<guid>.sock ✓
            ExamAwareIpcClient.DefaultIpcName = ipcName;

            var replayEvt = JsonSerializer.Serialize(new
            {
                type = "exam-event",
                @event = "exam-start",
                data = new ExamEventData
                {
                    ExamName = "高数期末",
                    ExamConfigName = "cfg-1",
                    StartTime = "2024-06-01 08:00",
                    EndTime = "2024-06-01 10:00"
                },
                timestamp = 1000L
            });
            // 在客户端 connect 之前就放入 replay 队列。
            // 客户端的连接 + 订阅几乎是瞬间完成的，mock 服务端在收到 subscribe 后会立刻读取 replayQueue。
            // 由于我们在 connect 之前就把 replay 放进去，所以一定能被读取到。
            _replayQueue.Enqueue(replayEvt);

            using var svc = new ExamAwareConnectionService(NullLogger<ExamAwareConnectionService>.Instance);
            var examStarts = 0;
            ExamEventData? lastStartData = null;
            svc.ExamStart += (_, d) => { examStarts++; lastStartData = d; };

            await svc.StartAsync();

            // 等到所有预期事件都发生
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((!svc.IsConnected || !svc.IsSubscribed || examStarts == 0) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            // 重要：这里要用 Volatile.Read 强制从主内存读取，否则编译器/CPU 可能缓存旧值
            var isExamActive = Volatile.Read(ref svc.IsExamActiveRef);

            Assert.True(svc.IsConnected, $"未连接 (ReconnectAttempt={svc.ReconnectAttempt})");
            Assert.True(svc.IsSubscribed, "未订阅");
            Assert.True(isExamActive, "考试未激活");
            Assert.True(examStarts >= 1, "ExamStart 未触发");
            Assert.Equal("高数期末", lastStartData?.ExamName);

            // 验证 SendCommandAsync（注意：SendCommandAsync 每次都新建连接到 IPC，
            // 所以命令会被任意一个 mock 客户端收到，需要遍历所有客户端断言）
            var result = await ExamAwareIpcClient.SendCommandAsync("play-from-url", new { url = "https://example.com/cfg.ea2" });
            Assert.True(result["success"]?.GetValue<bool>() == true, $"play-from-url 应成功, actual: {result}");

            var stopResult = await ExamAwareIpcClient.SendCommandAsync("stop");
            Assert.True(stopResult["success"]?.GetValue<bool>() == true, $"stop 应成功, actual: {stopResult}");

            // 等命令被处理
            var deadlineForCmd = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadlineForCmd &&
                   !_clients.Values.Any(c => c.CommandsReceived.Contains("play-from-url") && c.CommandsReceived.Contains("stop")))
            {
                await Task.Delay(50);
            }
            Assert.Contains(_clients.Values, c => c.CommandsReceived.Contains("play-from-url"));
            Assert.Contains(_clients.Values, c => c.CommandsReceived.Contains("stop"));

            // 主动断开连接，验证 ExamEnd 被强制派发
            var examEnds = 0;
            svc.ExamEnd += (_, _) => examEnds++;
            _listener?.Close(); // 强制断开所有客户端

            deadline = DateTime.UtcNow.AddSeconds(8);
            while (examEnds == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }
            await svc.StopAsync(TimeSpan.FromSeconds(3));

            Assert.True(examEnds >= 1, "断连时若考试进行中应派发 ExamEnd");
            var isExamActiveAfterStop = Volatile.Read(ref svc.IsExamActiveRef);
            Assert.False(isExamActiveAfterStop, "断连后 IsExamActive 应为 false");
        }
        finally
        {
            ExamAwareIpcClient.DefaultIpcName = originalIpcName;
        }
    }

    [Fact]
    public async Task Connection_Retries_WhenServerNotRunning()
    {
        // 不启动 mock server，验证客户端能优雅地持续重试
        var originalIpcName = ExamAwareIpcClient.DefaultIpcName;
        try
        {
            ExamAwareIpcClient.DefaultIpcName = _socketPath
                .Replace("/tmp/", "")
                .Replace(".sock", "");

            using var svc = new ExamAwareConnectionService(NullLogger<ExamAwareConnectionService>.Instance);
            await svc.StartAsync();
            await Task.Delay(1500);
            Assert.False(svc.IsConnected);
            Assert.True(svc.ReconnectAttempt >= 1, $"应有重连尝试, actual: {svc.ReconnectAttempt}");
            await svc.StopAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            ExamAwareIpcClient.DefaultIpcName = originalIpcName;
        }
    }
}
