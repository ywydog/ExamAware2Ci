using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Actions;
using ExamAware2Ci.Shared;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Actions;

[ActionInfo("examaware2ci.actions.playExam", "放映考试信息", "\uE7B8")]
public class PlayExamAction(ILogger<PlayExamAction> logger) : ActionBase<PlayExamActionSettings>
{
    private ILogger<PlayExamAction> Logger { get; } = logger;

    protected override async Task OnInvoke()
    {
        if (string.IsNullOrWhiteSpace(Settings.Source))
        {
            Logger.LogWarning("放映考试信息：来源为空，跳过执行");
            return;
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
