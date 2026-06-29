namespace ExamAware2Ci.Models.Automations.Rules;

/// <summary>
/// 纯函数式的"考试相关"规则匹配判定，便于单元测试。
/// </summary>
public static class ExamPlayingRuleEvaluator
{
    /// <summary>
    /// 根据连接状态、最近事件数据和 settings 判定规则是否匹配。
    /// </summary>
    /// <param name="settings">规则设置（可为 null）</param>
    /// <param name="isConnected">ExamAware2 是否已连接</param>
    /// <param name="isExamActive">是否有未结束的考试</param>
    /// <param name="isPresentationActive">是否正在放映考试</param>
    /// <param name="lastEventExamName">最近事件携带的考试名（可为 null）</param>
    public static bool IsMatch(
        ExamPlayingRuleSettings? settings,
        bool isConnected,
        bool isExamActive,
        bool isPresentationActive,
        string? lastEventExamName)
    {
        var mode = settings?.MatchMode ?? ExamPlayingMatchMode.IsExamPlaying;

        bool baseMatch = mode switch
        {
            ExamPlayingMatchMode.IsConnected => isConnected,
            ExamPlayingMatchMode.IsPresenting => isConnected && isPresentationActive,
            ExamPlayingMatchMode.IsExamActive => isConnected && isExamActive,
            _ => // IsExamPlaying（默认）
                isConnected && (isPresentationActive || isExamActive)
        };

        if (!baseMatch) return false;

        // IsConnected 模式不应用考试名过滤
        if (mode == ExamPlayingMatchMode.IsConnected) return true;

        if (settings is { FilterByExamName: true })
        {
            if (lastEventExamName == null) return false;
            return lastEventExamName == settings.ExamName;
        }

        return true;
    }
}
