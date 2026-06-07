using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware4Ci.Models.Automation;

/// <summary>
/// 考试时间剩余触发器设置
/// </summary>
public class ExamTimeRemainingTriggerSettings : ObservableRecipient
{
    private int _alertTimeMinutes = 15;

    /// <summary>
    /// 提醒时间（分钟）
    /// </summary>
    public int AlertTimeMinutes
    {
        get => _alertTimeMinutes;
        set
        {
            if (value == _alertTimeMinutes) return;
            _alertTimeMinutes = value;
            OnPropertyChanged();
        }
    }
}
