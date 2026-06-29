using ExamAware2Ci.Automations.Triggers;
using ExamAware2Ci.Models;
using ExamAware2Ci.Models.Automations.Triggers;
using ExamAware2Ci.Models.Automations.Rules;
using Xunit;

namespace ExamAware2Ci.Tests;

/// <summary>
/// 覆盖：
///  - ExamEventFilterHelper 的纯函数过滤
///  - ExamTimeRemainingTriggerSettings / Triggering 行为
///  - ExamPlayingMatchMode 枚举 + settings 默认值
///  - 各 trigger settings 默认字段存在
/// </summary>
public class TriggerFilterAndRuleSettingsTests
{
    // ---------- ExamEventFilterHelper ----------

    [Fact]
    public void Filter_NullSettings_PassesAll()
    {
        Assert.True(ExamEventFilterHelper.MatchesFilter(null, new ExamEventData { ExamName = "X" }));
    }

    [Fact]
    public void Filter_EmptyFilter_PassesAll()
    {
        var s = new TestFilterSettings { ExamNameFilter = "" };
        Assert.True(ExamEventFilterHelper.MatchesFilter(s, new ExamEventData { ExamName = "Anything" }));
    }

    [Fact]
    public void Filter_WhitespaceFilter_PassesAll()
    {
        var s = new TestFilterSettings { ExamNameFilter = "   " };
        Assert.True(ExamEventFilterHelper.MatchesFilter(s, new ExamEventData { ExamName = "Anything" }));
    }

    [Fact]
    public void Filter_NullData_Fails()
    {
        var s = new TestFilterSettings { ExamNameFilter = "高数" };
        Assert.False(ExamEventFilterHelper.MatchesFilter(s, null));
    }

    [Fact]
    public void Filter_SubstringMatch_Passes()
    {
        var s = new TestFilterSettings { ExamNameFilter = "高数" };
        Assert.True(ExamEventFilterHelper.MatchesFilter(s, new ExamEventData { ExamName = "高数期末考试" }));
    }

    [Fact]
    public void Filter_CaseInsensitive_Passes()
    {
        var s = new TestFilterSettings { ExamNameFilter = "MATH" };
        Assert.True(ExamEventFilterHelper.MatchesFilter(s, new ExamEventData { ExamName = "math final" }));
    }

    [Fact]
    public void Filter_NoMatch_Fails()
    {
        var s = new TestFilterSettings { ExamNameFilter = "高数" };
        Assert.False(ExamEventFilterHelper.MatchesFilter(s, new ExamEventData { ExamName = "英语" }));
    }

    private sealed class TestFilterSettings : IExamEventFilterSettings
    {
        public string ExamNameFilter { get; set; } = "";
    }

    // ---------- ExamStartTriggerSettings 默认值 ----------

    [Fact]
    public void ExamStartTriggerSettings_DefaultsToEmptyFilter()
    {
        var s = new ExamStartTriggerSettings();
        Assert.Equal("", s.ExamNameFilter);
    }

    [Fact]
    public void ExamEndTriggerSettings_DefaultsToEmptyFilter()
    {
        var s = new ExamEndTriggerSettings();
        Assert.Equal("", s.ExamNameFilter);
    }

    [Fact]
    public void ExamPresentationStartTriggerSettings_DefaultsToEmptyFilter()
    {
        var s = new ExamPresentationStartTriggerSettings();
        Assert.Equal("", s.ExamNameFilter);
    }

    [Fact]
    public void ExamStartTriggerSettings_ImplementsFilterInterface()
    {
        Assert.IsAssignableFrom<IExamEventFilterSettings>(new ExamStartTriggerSettings());
    }

    // ---------- ExamTimeRemainingTriggerSettings ----------

    [Fact]
    public void ExamTimeRemainingTriggerSettings_DefaultAlertTime15()
    {
        var s = new ExamTimeRemainingTriggerSettings();
        Assert.Equal(15, s.AlertTimeMinutes);
    }

    [Fact]
    public void ExamTimeRemainingTriggerSettings_DefaultMatchAnyFalse()
    {
        var s = new ExamTimeRemainingTriggerSettings();
        Assert.False(s.MatchAnyRemaining);
    }

    [Fact]
    public void ExamTimeRemainingTriggerSettings_FilterFieldExists()
    {
        var s = new ExamTimeRemainingTriggerSettings();
        Assert.Equal("", s.ExamNameFilter);
        Assert.IsAssignableFrom<IExamEventFilterSettings>(s);
    }

    // ---------- ExamPlayingRuleSettings 默认 ----------

    [Fact]
    public void ExamPlayingRuleSettings_DefaultModeIsExamPlaying()
    {
        var s = new ExamPlayingRuleSettings();
        Assert.Equal(ExamPlayingMatchMode.IsExamPlaying, s.MatchMode);
        Assert.False(s.FilterByExamName);
        Assert.Equal("", s.ExamName);
    }

    [Fact]
    public void ExamPlayingMatchMode_HasAllExpectedModes()
    {
        var values = (ExamPlayingMatchMode[])Enum.GetValues(typeof(ExamPlayingMatchMode));
        Assert.Contains(ExamPlayingMatchMode.IsExamPlaying, values);
        Assert.Contains(ExamPlayingMatchMode.IsPresenting, values);
        Assert.Contains(ExamPlayingMatchMode.IsExamActive, values);
        Assert.Contains(ExamPlayingMatchMode.IsConnected, values);
        Assert.Equal(4, values.Length);
    }

    // ---------- ExamPlayingRuleEvaluator 纯函数测试 ----------

    [Fact]
    public void Evaluator_NullSettings_DefaultsToIsExamPlaying()
    {
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(null, true, true, false, "X"));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(null, false, true, false, "X"));
    }

    [Fact]
    public void Evaluator_IsConnected_OnlyChecksConnection()
    {
        var s = new ExamPlayingRuleSettings { MatchMode = ExamPlayingMatchMode.IsConnected };
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, false, false, null));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(s, false, true, true, "X"));
    }

    [Fact]
    public void Evaluator_IsConnected_IgnoresExamNameFilter()
    {
        var s = new ExamPlayingRuleSettings
        {
            MatchMode = ExamPlayingMatchMode.IsConnected,
            FilterByExamName = true,
            ExamName = "未匹配考试"
        };
        // 即使考试名不匹配，IsConnected 模式也应当放行
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, false, false, "其他考试"));
    }

    [Fact]
    public void Evaluator_IsPresenting_RequiresPresenting()
    {
        var s = new ExamPlayingRuleSettings { MatchMode = ExamPlayingMatchMode.IsPresenting };
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, true, true, "X"));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, "X"));
    }

    [Fact]
    public void Evaluator_IsExamActive_RequiresActive()
    {
        var s = new ExamPlayingRuleSettings { MatchMode = ExamPlayingMatchMode.IsExamActive };
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, "X"));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(s, true, false, true, "X"));
    }

    [Fact]
    public void Evaluator_IsExamPlaying_DefaultMode()
    {
        var s = new ExamPlayingRuleSettings(); // 默认 IsExamPlaying
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, "X"));
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, false, true, "X"));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(s, true, false, false, "X"));
    }

    [Fact]
    public void Evaluator_FilterByExamName_ExactMatch()
    {
        var s = new ExamPlayingRuleSettings
        {
            MatchMode = ExamPlayingMatchMode.IsExamActive,
            FilterByExamName = true,
            ExamName = "高数"
        };
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, "高数"));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, "高数期末"));
        Assert.False(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, null));
    }

    [Fact]
    public void Evaluator_FilterDisabled_PassesAll()
    {
        var s = new ExamPlayingRuleSettings
        {
            MatchMode = ExamPlayingMatchMode.IsExamActive,
            FilterByExamName = false,
            ExamName = "不匹配"
        };
        Assert.True(ExamPlayingRuleEvaluator.IsMatch(s, true, true, false, "任何考试"));
    }
}
