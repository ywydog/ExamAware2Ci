using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试开始时触发
/// </summary>
[TriggerInfo("examaware2ci.triggers.examStart", "考试开始时", "\uE8DE")]
public class ExamStartTrigger(ExamAwareConnectionService connectionService, ILogger<ExamStartTrigger> logger) : TriggerBase
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;
    private ILogger<ExamStartTrigger> Logger { get; } = logger;

    public override void Loaded()
    {
        ConnectionService.ExamStart += OnExamStart;
        Logger.LogDebug("[ExamAware2Ci]触发器已加载: 考试开始时");
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamStart -= OnExamStart;
        Logger.LogDebug("[ExamAware2Ci]触发器已卸载: 考试开始时");
    }

    private void OnExamStart(object? sender, Interface.Models.ExamEventData e)
    {
        Logger.LogInformation("[ExamAware2Ci]触发: 考试开始时 - {Name}", e.ExamName);
        Trigger();
    }
}
