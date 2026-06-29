using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ExamAware2Ci.Models.Automations;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 当考试剩余时间到达设定的提醒阈值时触发，考试结束时恢复。
/// 仅当 <see cref="ExamTimeRemainingTriggerSettings.AlertTimeMinutes"/> 与事件携带的
/// <see cref="Models.ExamEventData.AlertTime"/> 一致时才会触发，避免与其它阈值的提醒重叠。
/// </summary>
[TriggerInfo(Plugin.ExamAware2CiIds.ExamTimeRemainingTrigger, "考试时间剩余提醒时", "\uE823")]
public class ExamTimeRemainingTrigger(ExamAwareConnectionService connectionService, ILogger<ExamTimeRemainingTrigger> logger) : TriggerBase<ExamTimeRemainingTriggerSettings>
{
    private ExamAwareConnectionService ConnectionService { get; } = connectionService;
    private ILogger<ExamTimeRemainingTrigger> Logger { get; } = logger;

    public override void Loaded()
    {
        ConnectionService.ExamTimeRemaining += OnExamTimeRemaining;
        ConnectionService.ExamEnd += OnExamEnd;
        Logger.LogInformation("触发器已加载: 考试时间剩余提醒时 (提醒阈值 {Minutes} 分钟)", Settings.AlertTimeMinutes);

        // 若加载时已经收到过匹配阈值的 exam-time-remaining，立即补触发，避免错过
        if (ConnectionService.IsConnected && IsMatchingAlert(ConnectionService.LastEventData))
        {
            Logger.LogInformation("加载时发现已有匹配的考试剩余提醒，立即触发");
            OnExamTimeRemaining(this, ConnectionService.LastEventData!);
        }
    }

    public override void UnLoaded()
    {
        ConnectionService.ExamTimeRemaining -= OnExamTimeRemaining;
        ConnectionService.ExamEnd -= OnExamEnd;
        Logger.LogDebug("触发器已卸载: 考试时间剩余提醒时");
    }

    private void OnExamTimeRemaining(object? sender, Models.ExamEventData e)
    {
        if (!IsMatchingAlert(e))
        {
            Logger.LogDebug("忽略非匹配阈值的考试剩余提醒: 事件阈值 {Event} 分钟, 当前设定 {Settings} 分钟",
                e.AlertTime, Settings.AlertTimeMinutes);
            return;
        }

        Logger.LogInformation("触发: 考试时间剩余提醒时 - {Name}, 剩余 {Min} 分钟", e.ExamName, e.RemainingMinutes);
        Trigger();
    }

    private void OnExamEnd(object? sender, Models.ExamEventData e)
    {
        Logger.LogInformation("恢复: 考试已结束（时间剩余提醒恢复） - {Name}", e.ExamName);
        TriggerRevert();
    }

    private bool IsMatchingAlert(Models.ExamEventData? data)
    {
        // 只有 exam-time-remaining 事件才会携带 AlertTime；其它事件的 AlertTime 为 null
        return data is { AlertTime: not null } && data.AlertTime.Value == Settings.AlertTimeMinutes;
    }
}
