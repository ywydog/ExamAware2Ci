using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations.Rules;

public partial class ExamPlayingRuleSettings : ObservableRecipient
{
    [ObservableProperty] private bool _filterByExamName = false;
    [ObservableProperty] private string _examName = "";
}
