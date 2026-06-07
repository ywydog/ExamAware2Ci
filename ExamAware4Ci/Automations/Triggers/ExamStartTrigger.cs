using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware4Ci.Services;

namespace ExamAware4Ci.Automations.Triggers;

/// <summary>
/// 当考试开始时触发
/// </summary>
[TriggerInfo("examaware4ci.triggers.examStart", "考试开始时", "\uE8DE")]
public class ExamStartTrigger(ExamAwareConnectionService connectionService) : TriggerBase
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;

    public override void Loaded()
    {
        ConnectionService.ExamStart += OnExamStart;
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamStart -= OnExamStart;
    }

    private void OnExamStart(object? sender, Interface.Models.ExamEventData e)
    {
        Trigger();
    }
}
