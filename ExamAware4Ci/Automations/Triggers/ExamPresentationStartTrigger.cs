using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware4Ci.Services;

namespace ExamAware4Ci.Automations.Triggers;

/// <summary>
/// 当进入考试放映时触发
/// </summary>
[TriggerInfo("examaware4ci.triggers.examPresentationStart", "进入考试放映时", "\uE7B8")]
public class ExamPresentationStartTrigger(ExamAwareConnectionService connectionService) : TriggerBase
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;

    public override void Loaded()
    {
        ConnectionService.ExamPresentationStart += OnExamPresentationStart;
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamPresentationStart -= OnExamPresentationStart;
    }

    private void OnExamPresentationStart(object? sender, Interface.Models.ExamEventData e)
    {
        Trigger();
    }
}
