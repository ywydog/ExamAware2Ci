using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ExamAware4Ci.Automations.Triggers;
using ExamAware4Ci.Controls.Automations.TriggerSettingsControls;
using ExamAware4Ci.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExamAware4Ci;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
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
            var connectionService = IAppHost.GetService<ExamAwareConnectionService>();
            _ = connectionService.StartAsync();
        };
    }
}
