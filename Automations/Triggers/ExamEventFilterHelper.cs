using ExamAware2Ci.Models;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>触发器设置可选实现的"考试名过滤"契约。</summary>
public interface IExamEventFilterSettings
{
    string ExamNameFilter { get; set; }
}

/// <summary>
/// 纯函数式的过滤器工具：便于单元测试，且不依赖 ClassIsland 桌面运行时。
/// 与 <see cref="ExamEventTriggerBase{TTrigger, TSettings}"/> 配合使用。
/// </summary>
public static class ExamEventFilterHelper
{
    /// <summary>
    /// 判定事件数据 <paramref name="data"/> 是否能通过 <paramref name="settings"/> 的过滤规则。
    ///  - settings 为 null：放行
    ///  - ExamNameFilter 为空/空白：放行
    ///  - data 为 null：拒绝
    ///  - data.ExamName 包含 ExamNameFilter（不区分大小写）：放行
    /// </summary>
    public static bool MatchesFilter(IExamEventFilterSettings? settings, ExamEventData? data)
    {
        if (settings == null) return true;
        if (string.IsNullOrWhiteSpace(settings.ExamNameFilter)) return true;
        if (data == null) return false;
        return data.ExamName != null &&
               data.ExamName.Contains(settings.ExamNameFilter, StringComparison.OrdinalIgnoreCase);
    }
}
