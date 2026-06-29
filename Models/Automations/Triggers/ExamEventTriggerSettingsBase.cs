using CommunityToolkit.Mvvm.ComponentModel;
using ExamAware2Ci.Automations.Triggers;

namespace ExamAware2Ci.Models.Automations.Triggers;

/// <summary>
/// 通用考试事件触发器设置：可选的考试名（包含）过滤。
/// </summary>
public abstract partial class ExamEventTriggerSettingsBase : ObservableRecipient, IExamEventFilterSettings
{
    /// <summary>
    /// 仅在考试名（包含）匹配时触发，区分大小写。留空表示不按考试名过滤。
    /// </summary>
    [ObservableProperty]
    private string _examNameFilter = string.Empty;
}

/// <summary>考试开始触发器设置</summary>
public partial class ExamStartTriggerSettings : ExamEventTriggerSettingsBase
{
}

/// <summary>考试结束触发器设置</summary>
public partial class ExamEndTriggerSettings : ExamEventTriggerSettingsBase
{
}

/// <summary>考试放映开始触发器设置</summary>
public partial class ExamPresentationStartTriggerSettings : ExamEventTriggerSettingsBase
{
}
