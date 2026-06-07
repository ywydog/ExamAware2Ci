namespace ExamAware4Ci.Interface.Models;

/// <summary>
/// 考试事件数据
/// </summary>
public class ExamEventData
{
    /// <summary>
    /// 考试名称
    /// </summary>
    public string ExamName { get; set; } = "";

    /// <summary>
    /// 考试配置名称
    /// </summary>
    public string ExamConfigName { get; set; } = "";

    /// <summary>
    /// 开始时间
    /// </summary>
    public string StartTime { get; set; } = "";

    /// <summary>
    /// 结束时间
    /// </summary>
    public string EndTime { get; set; } = "";

    /// <summary>
    /// 剩余分钟数（仅 exam-time-remaining 事件）
    /// </summary>
    public int? RemainingMinutes { get; set; }

    /// <summary>
    /// 提醒时间（仅 exam-time-remaining 事件）
    /// </summary>
    public int? AlertTime { get; set; }
}
