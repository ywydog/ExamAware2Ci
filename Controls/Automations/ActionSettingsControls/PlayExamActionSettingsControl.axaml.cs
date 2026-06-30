using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations.Actions;

namespace ExamAware2Ci.Controls.Automations.ActionSettingsControls;

public partial class PlayExamActionSettingsControl : ActionSettingsControlBase<PlayExamActionSettings>
{
    public PlayExamActionSettingsControl()
    {
        InitializeComponent();
    }

    private static readonly IReadOnlyList<FilePickerFileType> FilePickerTypes = new List<FilePickerFileType>
    {
        new("考试档案文件 (.ea2)") { Patterns = ["*.ea2"] },
        new("JSON 配置 (.json)") { Patterns = ["*.json"] },
        new("所有文件") { Patterns = ["*.*"] }
    };

    private async void BtnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;

        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = Settings.SourceType == ExamSourceType.File
                ? "选择考试档案文件"
                : "选择本地考试档案（将自动切换为"本地文件"模式）",
            FileTypeFilter = FilePickerTypes
        };

        var files = await window.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        // 不论当前模式是 URL 还是 File，选了本地文件就切到 File 模式
        Settings.SourceType = ExamSourceType.File;
        Settings.Source = path;
        SetStatus($"已选择：{path}", isError: false);
    }

    private async void BtnTest_Click(object? sender, RoutedEventArgs e)
    {
        // 强制让 TextBox 把当前内容提交到绑定源，避免刚粘贴/输入完没失焦时取到旧值。
        SourceBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

        SetStatus("正在检查…", isError: false, busy: true);
        try
        {
            // 让 UI 先刷新
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var error = await Task.Run(() => Settings.Validate());
            if (error == null)
            {
                var kind = Settings.SourceType == ExamSourceType.Url ? "URL" : "文件";
                SetStatus($"✓ {kind} 看起来可用：{Settings.Source}", isError: false);
            }
            else
            {
                SetStatus($"✗ {error}", isError: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"✗ 校验异常：{ex.Message}", isError: true);
        }
    }

    private void BtnClear_Click(object? sender, RoutedEventArgs e)
    {
        Settings.Source = string.Empty;
        // 清掉焦点，避免 TextBox 残留上一次输入但没提交
        FocusManager.Instance?.Focus(null);
        SetStatus(null, isError: false);
    }

    private void SetStatus(string? text, bool isError, bool busy = false)
    {
        if (StatusText == null) return;
        if (text == null)
        {
            StatusText.IsVisible = false;
            StatusText.Text = string.Empty;
            return;
        }
        StatusText.IsVisible = true;
        StatusText.Text = text;
        StatusText.Foreground = busy
            ? Brushes.Gray
            : (isError ? Brushes.IndianRed : Brushes.MediumSeaGreen);
    }
}

/// <summary>
/// ComboBox 显示用的"来源类型"选项。
/// </summary>
public sealed class ExamSourceTypeOption
{
    public ExamSourceType Value { get; init; }
    public string Label { get; init; } = "";
    public string Glyph { get; init; } = "";
}

/// <summary>
/// ComboBox 用的"来源类型"提供器。
/// </summary>
public sealed class ExamSourceTypeOptionsProvider
{
    public IReadOnlyList<ExamSourceTypeOption> Items { get; } = new[]
    {
        new ExamSourceTypeOption { Value = ExamSourceType.Url,  Label = "URL 链接",  Glyph = "\uE71B" },
        new ExamSourceTypeOption { Value = ExamSourceType.File, Label = "本地文件",  Glyph = "\uE8E5" }
    };

    public ExamSourceTypeOption? Find(ExamSourceType value)
    {
        foreach (var opt in Items)
        {
            if (opt.Value == value) return opt;
        }
        return null;
    }
}

/// <summary>
/// 把 <see cref="ExamSourceType"/> 转换为对应的 <see cref="ExamSourceTypeOption"/>。
/// </summary>
public sealed class ExamSourceTypeEnumToOptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ExamSourceType t) return null;
        if (parameter is ExamSourceTypeOptionsProvider p) return p.Find(t);
        // 兜底：避免 XAML 里忘了带 ConverterParameter 时整列变空
        return t == ExamSourceType.Url
            ? new ExamSourceTypeOption { Value = ExamSourceType.Url,  Label = "URL 链接",  Glyph = "\uE71B" }
            : new ExamSourceTypeOption { Value = ExamSourceType.File, Label = "本地文件",  Glyph = "\uE8E5" };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExamSourceTypeOption opt) return opt.Value;
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// 把 <see cref="ExamSourceTypeOption"/> 转换回 <see cref="ExamSourceType"/>，目前未被使用（保留以备绑定反向场景）。
/// </summary>
public sealed class ExamSourceTypeOptionToEnumConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ExamSourceTypeOption opt ? opt.Value : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
