using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations.Actions;

public enum ExamSourceType
{
    Url,
    File
}

public partial class PlayExamActionSettings : ObservableRecipient
{
    [ObservableProperty] private ExamSourceType _sourceType = ExamSourceType.Url;
    [ObservableProperty] private string _source = "";
}
