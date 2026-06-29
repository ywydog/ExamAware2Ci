using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
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
                : "选择本地考试档案（将自动切换为“本地文件”模式）",
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
