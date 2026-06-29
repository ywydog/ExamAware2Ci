using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamAware2Ci.Shared;

/// <summary>
/// 与 ExamAware2 通信的 IPC 客户端（Named Pipe / Unix Domain Socket）
/// </summary>
public static class ExamAwareIpcClient
{
    public static string DefaultIpcName { get; set; } = "ExamAware2.examaware2";

    private const int MaxLineBytes = 1 * 1024 * 1024; // 1MB 单行上限

    /// <summary>
    /// 从流中读取一行（以 \n 结尾），并把行内多余字节保留在 <paramref name="carry"/> 中以备下次调用。
    /// </summary>
    /// <remarks>
    /// ⚠️ 关键实现要点：底层 <see cref="NetworkStream"/> / <see cref="NamedPipeClientStream"/>
    /// 没有用户态行缓冲——一次 <c>ReadAsync</c> 可能吞下 N 条消息的字节。如果调用方每次只
    /// "读到 \n 就 return"，则剩下那部分字节会**永远丢失**（再调用 ReadAsync 时
    /// 会因对端没有新数据而阻塞）。本方法在调用方提供的 <paramref name="carry"/> 中
    /// 保留残余字节，下一次调用先消费 carry、再读流，从而保证行解析严格按 \n 切分。
    /// </remarks>
    public static async Task<(byte[]? line, int carryLen)> ReadLineAsync(
        Stream stream, int maxBytes, byte[]? carry, int carryLen, CancellationToken ct)
    {
        maxBytes = Math.Min(maxBytes, MaxLineBytes);
        var collected = new List<byte>(256);

        // 先把上次的 carry 拷过来
        if (carry != null && carryLen > 0)
        {
            for (var i = 0; i < carryLen; i++)
            {
                collected.Add(carry[i]);
                if (carry[i] == (byte)'\n')
                {
                    // 把 carry 剩余部分（如果有）保留在返回的 carry 中
                    var remaining = carryLen - 1 - i;
                    if (remaining > 0)
                    {
                        Array.Copy(carry, i + 1, carry!, 0, remaining);
                    }
                    return (collected.ToArray(), remaining);
                }
            }
        }

        var buffer = new byte[4096];
        while (collected.Count < maxBytes)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                collected.Add(buffer[i]);
                if (buffer[i] == (byte)'\n')
                {
                    // 把 buffer 中剩余字节（i+1..read-1）写回 carry
                    var remaining = read - 1 - i;
                    if (remaining > 0 && carry != null)
                    {
                        Array.Copy(buffer, i + 1, carry, 0, remaining);
                    }
                    return (collected.ToArray(), remaining);
                }
            }
        }

        return collected.Count == 0 ? (null, 0) : (collected.ToArray(), 0);
    }

    /// <summary>
    /// 兼容旧调用：忽略 carry 信息的便利重载（每次调用间不保留残余字节，
    /// 仅适用于单次读一行的场景，例如 <see cref="SendCommandAsync"/> 等待服务端的单行响应）。
    /// </summary>
    public static async Task<byte[]?> ReadLineAsync(Stream stream, int maxBytes, CancellationToken ct)
    {
        var carry = new byte[MaxLineBytes];
        var (line, _) = await ReadLineAsync(stream, maxBytes, carry, 0, ct).ConfigureAwait(false);
        return line;
    }

    /// <summary>
    /// 把 IPC name 规整成 raw name（去掉平台前缀/后缀）。
    /// 服务端在拼装地址前也会走同样的逻辑，因此客户端和服务端必须保持一致。
    /// </summary>
    public static string NormalizeIpcName(string ipcName)
    {
        if (string.IsNullOrEmpty(ipcName)) return ipcName;

        // 去掉 Windows 命名管道前缀
        if (ipcName.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
        {
            ipcName = ipcName.Substring(@"\\.\pipe\".Length);
        }

        // 把跨平台的 raw name 规整：替换反斜杠、斜杠、空格、冒号为下划线
        var invalid = new[] { '\\', '/', ' ', ':' };
        foreach (var c in invalid)
        {
            ipcName = ipcName.Replace(c, '_');
        }

        return ipcName;
    }

    /// <summary>
    /// 把 raw IPC name 解析为平台相关的地址。
    /// </summary>
    public static string GetIpcAddress(string ipcName)
    {
        var name = NormalizeIpcName(ipcName);
        if (OperatingSystem.IsWindows())
        {
            return $@"\\.\pipe\{name}";
        }
        return $"/tmp/{name}.sock";
    }

    /// <summary>
    /// 创建到 IPC 端点的连接流。
    /// </summary>
    public static async Task<Stream> ConnectAsync(string ipcName, int timeoutMs, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            var pipeName = NormalizeIpcName(ipcName);
            var pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutMs, ct).ConfigureAwait(false);
            return pipe;
        }
        else
        {
            var address = GetIpcAddress(ipcName);
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endPoint = new UnixDomainSocketEndPoint(address);
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await socket.ConnectAsync(endPoint, linked.Token).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
    }

    /// <summary>
    /// 发送一条 JSON 命令并读取一条 JSON 响应。
    /// </summary>
    public static async Task<JsonObject> SendCommandAsync(string command, object? payload = null, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            using var stream = await ConnectAsync(DefaultIpcName, timeoutMs, cts.Token).ConfigureAwait(false);

            var req = new JsonObject
            {
                ["type"] = command,
                ["payload"] = payload != null ? JsonSerializer.SerializeToNode(payload) : new JsonObject()
            };
            var bytes = Encoding.UTF8.GetBytes(req.ToJsonString() + "\n");
            await stream.WriteAsync(bytes, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);

            // 服务端只发回单行响应，没有跨包合并问题
            var respBytes = await ReadLineAsync(stream, MaxLineBytes, cts.Token).ConfigureAwait(false);
            if (respBytes == null || respBytes.Length == 0)
            {
                return new JsonObject { ["success"] = false, ["error"] = "empty response" };
            }
            var respStr = Encoding.UTF8.GetString(respBytes).Trim();
            try
            {
                return JsonNode.Parse(respStr) as JsonObject ?? new JsonObject { ["success"] = false, ["error"] = "invalid response" };
            }
            catch (JsonException ex)
            {
                return new JsonObject { ["success"] = false, ["error"] = $"parse error: {ex.Message}" };
            }
        }
        catch (Exception ex)
        {
            return new JsonObject { ["success"] = false, ["error"] = ex.Message };
        }
    }
}
