using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试结束时触发
/// </summary>
[TriggerInfo("examaware2ci.triggers.examEnd", "考试结束时", "\uE894")]
public class ExamEndTrigger(ExamAwareConnectionService connectionService, ILogger<ExamEndTrigger> logger) : TriggerBase
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;
    private ILogger<ExamEndTrigger> Logger { get; } = logger;

    public override void Loaded()
    {
        ConnectionService.ExamEnd += OnExamEnd;
        Logger.LogDebug("[ExamAware2Ci]触发器已加载: 考试结束时");
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamEnd -= OnExamEnd;
        Logger.LogDebug("[ExamAware2Ci]触发器已卸载: 考试结束时");
    }

    private void OnExamEnd(object? sender, Models.ExamEventData e)
    {
        Logger.LogInformation("[ExamAware2Ci]触发: 考试结束时 - {Name}", e.ExamName);
        Trigger();
    }
}
