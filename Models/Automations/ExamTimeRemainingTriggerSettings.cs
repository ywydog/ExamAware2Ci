using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations;

/// <summary>
/// 考试时间剩余触发器设置
/// </summary>
public partial class ExamTimeRemainingTriggerSettings : ObservableRecipient
{
    /// <summary>
    /// 提醒时间（分钟）
    /// </summary>
    [ObservableProperty]
    private int _alertTimeMinutes = 15;
}
