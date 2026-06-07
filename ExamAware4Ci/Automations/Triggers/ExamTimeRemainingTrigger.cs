using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware4Ci.Models.Automation;
using ExamAware4Ci.Services;

namespace ExamAware4Ci.Automations.Triggers;

/// <summary>
/// 当考试还有多少时间时触发
/// </summary>
[TriggerInfo("examaware4ci.triggers.examTimeRemaining", "考试时间剩余提醒时", "\uE823")]
public class ExamTimeRemainingTrigger(ExamAwareConnectionService connectionService) : TriggerBase<ExamTimeRemainingTriggerSettings>
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;

    public override void Loaded()
    {
        ConnectionService.ExamTimeRemaining += OnExamTimeRemaining;
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamTimeRemaining -= OnExamTimeRemaining;
    }

    private void OnExamTimeRemaining(object? sender, Interface.Models.ExamEventData e)
    {
        Trigger();
    }
}
