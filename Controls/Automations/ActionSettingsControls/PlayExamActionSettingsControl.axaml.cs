using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations.Actions;

namespace ExamAware2Ci.Controls.Automations.ActionSettingsControls;

public partial class PlayExamActionSettingsControl : ActionSettingsControlBase<PlayExamActionSettings>
{
    public PlayExamActionSettingsControl()
    {
        InitializeComponent();
    }

    private async void BtnBrowse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;

        if (Settings.SourceType == ExamSourceType.File)
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "选择考试档案文件",
                FileTypeFilter =
                [
                    new FilePickerFileType("考试档案文件") { Patterns = ["*.ea2"] },
                    new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
                ]
            });
            if (files.Count > 0)
            {
                Settings.Source = files[0].Path.LocalPath;
            }
        }
        else
        {
            // URL 模式下也可以选择本地文件，自动切换类型
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "选择考试档案文件",
                FileTypeFilter =
                [
                    new FilePickerFileType("考试档案文件") { Patterns = ["*.ea2"] },
                    new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
                ]
            });
            if (files.Count > 0)
            {
                Settings.SourceType = ExamSourceType.File;
                Settings.Source = files[0].Path.LocalPath;
            }
        }
    }
}
