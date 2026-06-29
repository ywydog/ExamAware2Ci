using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations.Triggers;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试剩余时间到达设定的提醒阈值时触发，考试结束时恢复。
/// 三重过滤：
///  - <see cref="ExamTimeRemainingTriggerSettings.AlertTimeMinutes"/> 与事件 <see cref="Models.ExamEventData.AlertTime"/> 严格相等（默认行为）
///  - <see cref="ExamTimeRemainingTriggerSettings.MatchAnyRemaining"/> = true 时忽略 AlertTime，任意剩余时间都触发
///  - 可选按 <see cref="ExamTimeRemainingTriggerSettings.ExamNameFilter"/> 模糊匹配考试名
/// </summary>
[TriggerInfo(Plugin.ExamAware2CiIds.ExamTimeRemainingTrigger, "考试时间剩余提醒时", "\uE823")]
public class ExamTimeRemainingTrigger(
    ExamAwareConnectionService connectionService,
    ILogger<ExamTimeRemainingTrigger> logger)
    : ExamEventTriggerBase<ExamTimeRemainingTrigger, ExamTimeRemainingTriggerSettings>
{
    private readonly ExamAwareConnectionService _connectionService = connectionService;
    private readonly ILogger<ExamTimeRemainingTrigger> _logger = logger;

    protected override void Subscribe(ExamAwareConnectionService service)
    {
        service.ExamTimeRemaining += OnExamTimeRemaining;
        service.ExamEnd += OnExamEnd;
    }

    protected override void Unsubscribe(ExamAwareConnectionService service)
    {
        service.ExamTimeRemaining -= OnExamTimeRemaining;
        service.ExamEnd -= OnExamEnd;
    }

    protected override bool ShouldReplayOnLoad(ExamAwareConnectionService service) =>
        IsMatchingAlert(service.LastEventData);

    protected override void ReplayCurrent(ExamAwareConnectionService service)
    {
        OnExamTimeRemaining(service, service.LastEventData!);
    }

    private void OnExamTimeRemaining(object? sender, Models.ExamEventData e)
    {
        if (!IsMatchingAlert(e))
        {
            _logger.LogDebug("忽略非匹配阈值的考试剩余提醒: 事件阈值 {Event} 分钟, 当前设定 {Settings} 分钟, MatchAny={MatchAny}",
                e.AlertTime, Settings.AlertTimeMinutes, Settings.MatchAnyRemaining);
            return;
        }
        if (!MatchesFilter(e))
        {
            _logger.LogDebug("忽略非匹配考试名的考试剩余提醒: {Name}", e.ExamName);
            return;
        }
        _logger.LogInformation("触发: 考试时间剩余提醒时 - {Name}, 剩余 {Min} 分钟", e.ExamName, e.RemainingMinutes);
        Trigger();
    }

    private void OnExamEnd(object? sender, Models.ExamEventData e)
    {
        _logger.LogInformation("恢复: 考试已结束（时间剩余提醒恢复） - {Name}", e.ExamName);
        TriggerRevert();
    }

    private bool IsMatchingAlert(Models.ExamEventData? data)
    {
        if (data == null) return false;
        // MatchAnyRemaining: 接受任意剩余时间事件（仍然需要 AlertTime 不为 null，说明是剩余时间事件）
        if (Settings.MatchAnyRemaining)
        {
            return data.AlertTime != null;
        }
        return data.AlertTime != null && data.AlertTime.Value == Settings.AlertTimeMinutes;
    }
}
