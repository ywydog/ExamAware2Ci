using ExamAware2Ci.Interface.Models;

namespace ExamAware2Ci.Models;

/// <summary>
/// ExamAware2 WebSocket 考试事件消息
/// </summary>
public class ExamEventMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// 事件类型
    /// </summary>
    public string Event { get; set; } = "";

    /// <summary>
    /// 事件数据
    /// </summary>
    public ExamEventData Data { get; set; } = new();

    /// <summary>
    /// 时间戳
    /// </summary>
    public long Timestamp { get; set; }
}
