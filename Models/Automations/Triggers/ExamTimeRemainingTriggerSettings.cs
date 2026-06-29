using CommunityToolkit.Mvvm.ComponentModel;
using ExamAware2Ci.Automations.Triggers;

namespace ExamAware2Ci.Models.Automations.Triggers;

/// <summary>
/// 考试时间剩余触发器设置：
///  - AlertTimeMinutes：阈值（分钟）
///  - MatchAnyRemaining：true 时忽略阈值，任意剩余时间事件都触发
///  - ExamNameFilter：可选模糊匹配考试名（包含）
/// </summary>
public partial class ExamTimeRemainingTriggerSettings : ObservableRecipient, IExamEventFilterSettings
{
    [ObservableProperty] private int _alertTimeMinutes = 15;
    [ObservableProperty] private bool _matchAnyRemaining = false;
    [ObservableProperty] private string _examNameFilter = string.Empty;
}
