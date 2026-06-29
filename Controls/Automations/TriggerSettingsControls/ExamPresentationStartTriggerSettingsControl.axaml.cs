using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations.Triggers;

namespace ExamAware2Ci.Controls.Automations.TriggerSettingsControls;

public partial class ExamPresentationStartTriggerSettingsControl : TriggerSettingsControlBase<ExamPresentationStartTriggerSettings>
{
    public ExamPresentationStartTriggerSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }
}
