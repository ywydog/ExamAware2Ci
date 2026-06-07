using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ExamAware2Ci.Automations.Triggers;
using ExamAware2Ci.Controls.Automations.TriggerSettingsControls;
using ExamAware2Ci.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci;

[PluginEntrance]
public class Plugin : PluginBase
{
    private ILogger<Plugin>? _logger;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddLogging();

        // 注册 ExamAware 连接服务
        services.AddSingleton<ExamAwareConnectionService>();

        // 注册自动化触发器
        services.AddTrigger<ExamPresentationStartTrigger>();
        services.AddTrigger<ExamStartTrigger>();
        services.AddTrigger<ExamTimeRemainingTrigger, ExamTimeRemainingTriggerSettingsControl>();
        services.AddTrigger<ExamEndTrigger>();

        // 应用启动后启动连接
        AppBase.Current.AppStarted += (sender, args) =>
        {
            _logger = IAppHost.GetService<ILogger<Plugin>>();
            _logger?.LogInformation("[ExamAware2Ci]插件正在启动...");

            var connectionService = IAppHost.GetService<ExamAwareConnectionService>();

            // 订阅连接状态变化事件
            connectionService.ConnectionStateChanged += (s, connected) =>
            {
                if (connected)
                {
                    _logger?.LogInformation("[ExamAware2Ci]已连接 ExamAware2");
                }
                else
                {
                    _logger?.LogWarning("[ExamAware2Ci]与 ExamAware2 的连接已断开");
                }
            };

            _ = connectionService.StartAsync();
            _logger?.LogInformation("[ExamAware2Ci]插件启动完成");
        };

        // 应用停止时断开连接
        AppBase.Current.AppStopping += (sender, args) =>
        {
            _logger?.LogInformation("[ExamAware2Ci]插件正在关闭，断开 ExamAware2 连接...");
            var connectionService = IAppHost.GetService<ExamAwareConnectionService>();
            connectionService.Dispose();
            _logger?.LogInformation("[ExamAware2Ci]插件已关闭");
        };
    }
}
