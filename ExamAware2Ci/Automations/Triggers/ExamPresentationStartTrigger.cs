using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当进入考试放映时触发
/// </summary>
[TriggerInfo("examaware2ci.triggers.examPresentationStart", "进入考试放映时", "\uE7B8")]
public class ExamPresentationStartTrigger(ExamAwareConnectionService connectionService, ILogger<ExamPresentationStartTrigger> logger) : TriggerBase
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;
    private ILogger<ExamPresentationStartTrigger> Logger { get; } = logger;

    public override void Loaded()
    {
        ConnectionService.ExamPresentationStart += OnExamPresentationStart;
        Logger.LogDebug("[ExamAware2Ci]触发器已加载: 进入考试放映时");
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamPresentationStart -= OnExamPresentationStart;
        Logger.LogDebug("[ExamAware2Ci]触发器已卸载: 进入考试放映时");
    }

    private void OnExamPresentationStart(object? sender, Interface.Models.ExamEventData e)
    {
        Logger.LogInformation("[ExamAware2Ci]触发: 进入考试放映时 - {Name}", e.ExamName);
        Trigger();
    }
}
