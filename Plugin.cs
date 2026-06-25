using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using ExamAware2Ci.Automations.Actions;
using ExamAware2Ci.Automations.Triggers;
using ExamAware2Ci.Controls.Automations.ActionSettingsControls;
using ExamAware2Ci.Controls.Automations.RuleSettingsControls;
using ExamAware2Ci.Controls.Automations.TriggerSettingsControls;
using ExamAware2Ci.Models.Automations.Rules;
using ExamAware2Ci.Services;
using ExamAware2Ci.Services.Automations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci;

[PluginEntrance]
public class Plugin : PluginBase
{
    private ILogger<Plugin>? _logger;

    /// <summary>
    /// ExamAware2 联动功能相关标识符
    /// </summary>
    public static class ExamAware2CiIds
    {
        // 触发器
        public const string ExamPresentationStartTrigger = "examaware2ci.triggers.examPresentationStart";
        public const string ExamStartTrigger = "examaware2ci.triggers.examStart";
        public const string ExamTimeRemainingTrigger = "examaware2ci.triggers.examTimeRemaining";
        public const string ExamEndTrigger = "examaware2ci.triggers.examEnd";

        // 行动
        public const string PlayExamAction = "examaware2ci.actions.playExam";

        // 规则集
        public const string ExamPlayingRule = "examaware2ci.rules.examPlaying";
    }

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddLogging();

        // 注册 ExamAware2 联动功能：触发器、行动、规则集及相关服务
        RegisterExamAware2CiFeatures(services);

        // 应用启动后初始化联动功能
        AppBase.Current.AppStarted += OnAppStarted;

        // 应用停止时释放联动功能
        AppBase.Current.AppStopping += OnAppStopping;
    }

    #region ExamAware2 联动功能注册

    /// <summary>
    /// 集中注册 ExamAware2 联动相关的服务、触发器、行动和规则集
    /// </summary>
    private void RegisterExamAware2CiFeatures(IServiceCollection services)
    {
        // 连接服务：通过 IPC 长连接接收 ExamAware2 推送的考试事件
        services.AddSingleton<ExamAwareConnectionService>();

        // 规则处理服务：提供"正在考试时"规则集的判断逻辑
        services.AddSingleton<RuleHandlerService>();

        // 触发器
        services.AddTrigger<ExamPresentationStartTrigger>();
        services.AddTrigger<ExamStartTrigger>();
        services.AddTrigger<ExamTimeRemainingTrigger, ExamTimeRemainingTriggerSettingsControl>();
        services.AddTrigger<ExamEndTrigger>();

        // 行动
        services.AddAction<PlayExamAction, PlayExamActionSettingsControl>();

        // 规则集
        services.AddRule<ExamPlayingRuleSettings, ExamPlayingRuleSettingsControl>(
            ExamAware2CiIds.ExamPlayingRule, "ExamAware2 - 正在考试时", "\uE7B8");
    }

    /// <summary>
    /// 应用启动后初始化 ExamAware2 联动功能
    /// </summary>
    private void OnAppStarted(object? sender, EventArgs args)
    {
        _logger = IAppHost.GetService<ILogger<Plugin>>();
        _logger?.LogInformation("插件正在启动...");

        var connectionService = IAppHost.GetService<ExamAwareConnectionService>();

        // 订阅连接状态变化事件
        connectionService.ConnectionStateChanged += (s, connected) =>
        {
            if (connected)
            {
                _logger?.LogInformation("已连接 ExamAware2");
            }
            else
            {
                _logger?.LogWarning("与 ExamAware2 的连接已断开");
            }
        };

        _logger?.LogInformation("正在启动 ExamAware2 IPC 连接...");
        _ = connectionService.StartAsync();

        // 初始化规则处理服务
        var ruleHandlerService = IAppHost.GetService<RuleHandlerService>();
        ruleHandlerService.Register();

        _logger?.LogInformation("插件启动完成");
    }

    /// <summary>
    /// 应用停止时释放 ExamAware2 联动功能
    /// </summary>
    private void OnAppStopping(object? sender, EventArgs args)
    {
        _logger?.LogInformation("插件正在关闭，断开 ExamAware2 连接...");
        var connectionService = IAppHost.GetService<ExamAwareConnectionService>();
        connectionService.Dispose();
        _logger?.LogInformation("插件已关闭");
    }

    #endregion
}
