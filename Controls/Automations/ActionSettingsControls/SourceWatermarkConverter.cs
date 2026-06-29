using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ExamAware2Ci.Models.Automations.Actions;

namespace ExamAware2Ci.Controls.Automations.ActionSettingsControls;

/// <summary>
/// 根据 <see cref="ExamSourceType"/> 返回对应的输入框 Watermark。
/// </summary>
public sealed class SourceWatermarkConverter : IValueConverter
{
    public static readonly SourceWatermarkConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExamSourceType t)
        {
            return t == ExamSourceType.Url
                ? "输入 http(s):// 配置 URL"
                : "输入本地 .ea2 / .json 路径（或点右侧浏览）";
        }
        return "输入 URL 或本地文件路径";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
