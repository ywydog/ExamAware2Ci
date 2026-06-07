using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware4Ci.Services;

namespace ExamAware4Ci.Automations.Triggers;

/// <summary>
/// 当考试结束时触发
/// </summary>
[TriggerInfo("examaware4ci.triggers.examEnd", "考试结束时", "\uE894")]
public class ExamEndTrigger(ExamAwareConnectionService connectionService) : TriggerBase
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;

    public override void Loaded()
    {
        ConnectionService.ExamEnd += OnExamEnd;
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamEnd -= OnExamEnd;
    }

    private void OnExamEnd(object? sender, Interface.Models.ExamEventData e)
    {
        Trigger();
    }
}
