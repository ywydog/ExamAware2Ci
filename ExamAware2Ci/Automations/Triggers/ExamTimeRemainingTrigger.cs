using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automation;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试还有多少时间时触发
/// </summary>
[TriggerInfo("examaware2ci.triggers.examTimeRemaining", "考试时间剩余提醒时", "\uE823")]
public class ExamTimeRemainingTrigger(ExamAwareConnectionService connectionService, ILogger<ExamTimeRemainingTrigger> logger) : TriggerBase<ExamTimeRemainingTriggerSettings>
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;
    private ILogger<ExamTimeRemainingTrigger> Logger { get; } = logger;

    public override void Loaded()
    {
        ConnectionService.ExamTimeRemaining += OnExamTimeRemaining;
        Logger.LogDebug("[ExamAware2Ci]触发器已加载: 考试时间剩余提醒时");
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamTimeRemaining -= OnExamTimeRemaining;
        Logger.LogDebug("[ExamAware2Ci]触发器已卸载: 考试时间剩余提醒时");
    }

    private void OnExamTimeRemaining(object? sender, Interface.Models.ExamEventData e)
    {
        Logger.LogInformation("[ExamAware2Ci]触发: 考试时间剩余提醒时 - {Name}, 剩余 {Min} 分钟", e.ExamName, e.RemainingMinutes);
        Trigger();
    }
}
