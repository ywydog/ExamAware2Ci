using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Triggers;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试结束时触发。
/// 可选按考试名（包含）过滤。
/// </summary>
[TriggerInfo(Plugin.ExamAware2CiIds.ExamEndTrigger, "考试结束时", "\uE894")]
public class ExamEndTrigger(
    ExamAwareConnectionService connectionService,
    ILogger<ExamEndTrigger> logger)
    : ExamEventTriggerBase<ExamEndTrigger, ExamEndTriggerSettings>
{
    private readonly ExamAwareConnectionService _connectionService = connectionService;
    private readonly ILogger<ExamEndTrigger> _logger = logger;

    protected override void Subscribe(ExamAwareConnectionService service)
    {
        service.ExamEnd += OnExamEnd;
    }

    protected override void Unsubscribe(ExamAwareConnectionService service)
    {
        service.ExamEnd -= OnExamEnd;
    }

    private void OnExamEnd(object? sender, Models.ExamEventData e)
    {
        if (!MatchesFilter(e))
        {
            _logger.LogDebug("考试结束触发器：过滤掉不匹配的事件 {Name}", e.ExamName);
            return;
        }
        _logger.LogInformation("触发: 考试结束时 - {Name}", e.ExamName);
        Trigger();
    }
}
