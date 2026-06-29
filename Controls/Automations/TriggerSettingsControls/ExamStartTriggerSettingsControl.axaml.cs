using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations.Triggers;

namespace ExamAware2Ci.Controls.Automations.TriggerSettingsControls;

public partial class ExamStartTriggerSettingsControl : TriggerSettingsControlBase<ExamStartTriggerSettings>
{
    public ExamStartTriggerSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }
}
