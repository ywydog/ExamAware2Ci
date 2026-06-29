using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ExamAware2Ci.Models.Automations.Rules;
using FluentAvalonia.UI.Controls;

namespace ExamAware2Ci.Controls.Automations.RuleSettingsControls;

public partial class ExamPlayingRuleSettingsControl : RuleSettingsControlBase<ExamPlayingRuleSettings>
{
    public ExamPlayingRuleSettingsControl()
    {
        InitializeComponent();
    }

    private void ButtonShowSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindResource("SettingsDrawer") is not ContentControl cc) return;
        cc.DataContext = this;
        _ = ShowDrawer(cc);
    }

    private async Task ShowDrawer(Control control, bool isOpenInDialog = false)
    {
        if (!isOpenInDialog &&
            this.GetVisualRoot() is Window window &&
            window.GetType().FullName == "ClassIsland.Views.SettingsWindowNew")
        {
            control.Classes.Remove("in-dialog");
            control.Classes.Add("in-drawer");

            if (control is ContentControl cc)
            {
                cc.Padding = new Avalonia.Thickness(16);
            }
            else
            {
                control.Margin = new Avalonia.Thickness(16);
            }

            SettingsPageBase.OpenDrawerCommand.Execute(control);
        }
        else
        {
            control.Classes.Remove("in-drawer");
            control.Classes.Add("in-dialog");

            if (control.Parent is ContentDialog contentDialog)
            {
                contentDialog.Content = null;
            }

            var dialog = new ContentDialog
            {
                Content = control,
                TitleTemplate = new DataTemplate(),
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary,
                DataContext = this
            };

            await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        }
    }
}

/// <summary>
/// 当 <see cref="ExamPlayingRuleSettings.MatchMode"/> != IsConnected 时返回 true，用于隐藏 IsConnected 模式下不相关的 UI 控件。
/// </summary>
public sealed class NotConnectedModeConverter : IValueConverter
{
    public static readonly NotConnectedModeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not ExamPlayingMatchMode.IsConnected;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// ComboBox 用的"模式选项"提供器。
/// </summary>
public sealed class ExamPlayingMatchModeOptionsProvider
{
    public IReadOnlyList<ExamPlayingMatchModeOption> Items { get; } = new[]
    {
        new ExamPlayingMatchModeOption { Value = ExamPlayingMatchMode.IsExamPlaying, Label = "正在考试（放映或已开始未结束）" },
        new ExamPlayingMatchModeOption { Value = ExamPlayingMatchMode.IsPresenting,  Label = "正在放映考试" },
        new ExamPlayingMatchModeOption { Value = ExamPlayingMatchMode.IsExamActive,   Label = "考试已开始但未结束" },
        new ExamPlayingMatchModeOption { Value = ExamPlayingMatchMode.IsConnected,   Label = "已连接 ExamAware2" }
    };
}

public sealed class ExamPlayingMatchModeOption
{
    public ExamPlayingMatchMode Value { get; init; }
    public string Label { get; init; } = "";
}

/// <summary>
/// 把 <see cref="ExamPlayingMatchMode"/> 枚举值转换为对应的 <see cref="ExamPlayingMatchModeOption"/>。
/// </summary>
public sealed class EnumToOptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ExamPlayingMatchMode mode) return null;
        if (parameter is not ExamPlayingMatchModeOptionsProvider p) return null;
        foreach (var opt in p.Items)
        {
            if (opt.Value == mode) return opt;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExamPlayingMatchModeOption opt) return opt.Value;
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// 把 <see cref="ExamPlayingMatchModeOption"/> 转换回 <see cref="ExamPlayingMatchMode"/>，目前未被使用（保留）。
/// </summary>
public sealed class OptionToEnumConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ExamPlayingMatchModeOption opt ? opt.Value : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}
