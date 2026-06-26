using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExamAware2Ci.Shared;

public static class ExamAwareIpcClient
{
    // 注意：此名称必须与 ExamAware2 服务端 ipcServer.ts 中的 IPC_NAME 保持一致
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
