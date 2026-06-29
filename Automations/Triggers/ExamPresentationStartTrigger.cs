using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Triggers;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当进入考试放映时触发，放映停止时恢复。
/// 可选按考试名（包含）过滤。
/// </summary>
[TriggerInfo(Plugin.ExamAware2CiIds.ExamPresentationStartTrigger, "进入考试放映时", "\uE7B8")]
public class ExamPresentationStartTrigger(
    ExamAwareConnectionService connectionService,
    ILogger<ExamPresentationStartTrigger> logger)
    : ExamEventTriggerBase<ExamPresentationStartTrigger, ExamPresentationStartTriggerSettings>
{
    private readonly ExamAwareConnectionService _connectionService = connectionService;
    private readonly ILogger<ExamPresentationStartTrigger> _logger = logger;

    protected override void Subscribe(ExamAwareConnectionService service)
    {
        service.ExamPresentationStart += OnExamPresentationStart;
        service.ExamPresentationStop += OnExamPresentationStop;
    }

    protected override void Unsubscribe(ExamAwareConnectionService service)
    {
        service.ExamPresentationStart -= OnExamPresentationStart;
        service.ExamPresentationStop -= OnExamPresentationStop;
    }

    protected override bool ShouldReplayOnLoad(ExamAwareConnectionService service) =>
        service.IsPresentationActive && service.LastEventData != null;

    protected override void ReplayCurrent(ExamAwareConnectionService service)
    {
        OnExamPresentationStart(service, service.LastEventData!);
    }

    private void OnExamPresentationStart(object? sender, Models.ExamEventData e)
    {
        if (!MatchesFilter(e))
        {
            _logger.LogDebug("考试放映开始触发器：过滤掉不匹配的事件 {Name}", e.ExamName);
            return;
        }
        _logger.LogInformation("触发: 进入考试放映时 - {Name}", e.ExamName);
        Trigger();
    }

    private void OnExamPresentationStop(object? sender, Models.ExamEventData e)
    {
        _logger.LogInformation("恢复: 考试放映已停止 - {Name}", e.ExamName);
        TriggerRevert();
    }
}
