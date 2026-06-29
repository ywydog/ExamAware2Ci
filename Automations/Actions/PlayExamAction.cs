using System.IO;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Actions;
using ExamAware2Ci.Shared;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Actions;

[ActionInfo(Plugin.ExamAware2CiIds.PlayExamAction, "放映考试信息", "\uE7B8", addDefaultToMenu: false)]
public class PlayExamAction(ILogger<PlayExamAction> logger) : ActionBase<PlayExamActionSettings>
{
    private ILogger<PlayExamAction> Logger { get; } = logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ea2", ".json"
    };

    protected override async Task OnInvoke()
    {
        if (string.IsNullOrWhiteSpace(Settings.Source))
        {
            Logger.LogWarning("放映考试信息：来源为空，跳过执行");
            return;
        }

        // 预校验：避免在错误来源时还往 IPC 发命令
        if (Settings.SourceType == ExamSourceType.File)
        {
            var path = Settings.Source;
            if (!File.Exists(path))
            {
                Logger.LogWarning("放映考试信息：文件不存在 - {Path}", path);
                return;
            }
            var ext = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(ext))
            {
                Logger.LogWarning("放映考试信息：不支持的文件扩展名 {Ext}（仅支持 .ea2 / .json）", ext);
                return;
            }
        }
        else
        {
            var url = Settings.Source;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                Logger.LogWarning("放映考试信息：URL 格式不正确 - {Url}", url);
                return;
            }
        }

        var type = Settings.SourceType == ExamSourceType.Url
            ? "play-from-url" : "play-from-file";
        var payload = Settings.SourceType == ExamSourceType.Url
            ? (object)new { url = Settings.Source }
            : new { path = Settings.Source };

        Logger.LogInformation("放映考试信息：类型={Type}, 来源={Source}", type, Settings.Source);

        var result = await ExamAwareIpcClient.SendCommandAsync(type, payload);

        if (result["success"]?.GetValue<bool>() == true)
        {
            Logger.LogInformation("放映考试信息：执行成功");
        }
        else
        {
            var error = result["error"]?.GetValue<string>() ?? "未知错误";
            Logger.LogWarning("放映考试信息：执行失败 - {Error}", error);
        }
    }

    protected override async Task OnRevert()
    {
        Logger.LogInformation("放映考试信息：停止放映");
        var result = await ExamAwareIpcClient.SendCommandAsync("stop");

        if (result["success"]?.GetValue<bool>() != true)
        {
            var error = result["error"]?.GetValue<string>() ?? "未知错误";
            Logger.LogWarning("放映考试信息：停止失败 - {Error}", error);
        }
    }
}
