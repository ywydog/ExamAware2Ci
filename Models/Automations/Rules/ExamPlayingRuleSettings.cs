using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations.Rules;

/// <summary>
/// "考试相关"规则匹配模式
/// </summary>
public enum ExamPlayingMatchMode
{
    /// <summary>正在考试（正在放映或考试已开始未结束）</summary>
    IsExamPlaying = 0,
    /// <summary>正在放映考试</summary>
    IsPresenting = 1,
    /// <summary>考试已开始但未结束（无论是否放映）</summary>
    IsExamActive = 2,
    /// <summary>已与 ExamAware2 建立 IPC 连接</summary>
    IsConnected = 3
}

public partial class ExamPlayingRuleSettings : ObservableRecipient
{
    /// <summary>规则匹配模式</summary>
    [ObservableProperty]
    private ExamPlayingMatchMode _matchMode = ExamPlayingMatchMode.IsExamPlaying;

    /// <summary>是否按考试名筛选</summary>
    [ObservableProperty]
    private bool _filterByExamName = false;

    /// <summary>目标考试名（仅当 <see cref="FilterByExamName"/> = true 时生效）</summary>
    [ObservableProperty]
    private string _examName = "";
}
