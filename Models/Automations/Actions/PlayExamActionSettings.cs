using CommunityToolkit.Mvvm.ComponentModel;

namespace ExamAware2Ci.Models.Automations.Actions;

/// <summary>
/// 行动"放映考试"的数据来源类型。
/// 显式赋值避免日后插入新成员时索引错位（被持久化到配置文件中的旧值会失效）。
/// </summary>
public enum ExamSourceType
{
    /// <summary>远程配置 URL（http/https）。</summary>
    Url = 0,
    /// <summary>本地考试档案（.ea2 / .json）。</summary>
    File = 1
}

public partial class PlayExamActionSettings : ObservableRecipient
{
    [ObservableProperty] private ExamSourceType _sourceType = ExamSourceType.Url;
    [ObservableProperty] private string _source = "";

    /// <summary>
    /// 验证当前 Source 是否可用（File 存在 + 扩展名 / URL 格式）
    /// 返回 null 表示通过；返回错误描述表示失败。
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return "来源为空";
        }

        if (SourceType == ExamSourceType.File)
        {
            if (!System.IO.File.Exists(Source))
            {
                return $"文件不存在: {Source}";
            }
            var ext = System.IO.Path.GetExtension(Source);
            if (!string.Equals(ext, ".ea2", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".json", System.StringComparison.OrdinalIgnoreCase))
            {
                return $"不支持的扩展名 {ext}（仅支持 .ea2 / .json）";
            }
            return null;
        }
        else
        {
            if (!System.Uri.TryCreate(Source, System.UriKind.Absolute, out var uri) ||
                (uri.Scheme != System.Uri.UriSchemeHttp && uri.Scheme != System.Uri.UriSchemeHttps))
            {
                return "URL 格式不正确（仅支持 http/https）";
            }
            return null;
        }
    }
}
