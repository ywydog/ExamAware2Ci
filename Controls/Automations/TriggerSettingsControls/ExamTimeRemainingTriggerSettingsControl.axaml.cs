using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations;

namespace ExamAware2Ci.Controls.Automations.TriggerSettingsControls;

/// <summary>
/// 考试时间剩余触发器设置控件
/// </summary>
public partial class ExamTimeRemainingTriggerSettingsControl : TriggerSettingsControlBase<ExamTimeRemainingTriggerSettings>
{
    public ExamTimeRemainingTriggerSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }
}
