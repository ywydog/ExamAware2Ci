using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Triggers;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试开始时触发，考试结束时恢复。
/// 可选按考试名（包含）过滤，避免多个考试场景下误触。
/// </summary>
[TriggerInfo(Plugin.ExamAware2CiIds.ExamStartTrigger, "考试开始时", "\uE8DE")]
public class ExamStartTrigger(
    ExamAwareConnectionService connectionService,
    ILogger<ExamStartTrigger> logger)
    : ExamEventTriggerBase<ExamStartTrigger, ExamStartTriggerSettings>
{
    private readonly ExamAwareConnectionService _connectionService = connectionService;
    private readonly ILogger<ExamStartTrigger> _logger = logger;

    protected override void Subscribe(ExamAwareConnectionService service)
    {
        service.ExamStart += OnExamStart;
        service.ExamEnd += OnExamEnd;
    }

    protected override void Unsubscribe(ExamAwareConnectionService service)
    {
        service.ExamStart -= OnExamStart;
        service.ExamEnd -= OnExamEnd;
    }

    protected override bool ShouldReplayOnLoad(ExamAwareConnectionService service) =>
        service.IsExamActive && service.LastEventData != null;

    protected override void ReplayCurrent(ExamAwareConnectionService service)
    {
        // 触发器加载时若已处于考试中，补发一次"开始"事件，避免错过。
        OnExamStart(service, service.LastEventData!);
    }

    private void OnExamStart(object? sender, Models.ExamEventData e)
    {
        if (!MatchesFilter(e))
        {
            _logger.LogDebug("考试开始触发器：过滤掉不匹配的事件 {Name}", e.ExamName);
            return;
        }
        _logger.LogInformation("触发: 考试开始时 - {Name}", e.ExamName);
        Trigger();
    }

    private void OnExamEnd(object? sender, Models.ExamEventData e)
    {
        _logger.LogInformation("恢复: 考试已结束 - {Name}", e.ExamName);
        TriggerRevert();
    }
}
