namespace ExamAware4Ci.Models;

/// <summary>
/// 考试状态数据
/// </summary>
public class ExamStatusData
{
    public bool IsPlaying { get; set; }
    public CurrentExamInfo? CurrentExam { get; set; }
    public List<ExamListItem> ExamList { get; set; } = [];
    public string ExamConfigName { get; set; } = "";
}

public class CurrentExamInfo
{
    public string Name { get; set; } = "";
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public int AlertTime { get; set; }
    public string Status { get; set; } = "";
    public long RemainingMs { get; set; }
}

public class ExamListItem
{
    public string Name { get; set; } = "";
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public string Status { get; set; } = "";
}
