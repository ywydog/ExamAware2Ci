using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Shared;
using ExamAware2Ci.Models;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Automations.Triggers;

/// <summary>
/// 考试事件触发器基类：
///  1. <see cref="Loaded"/> 加载时绑定连接服务事件，并在已连接/已激活时主动补发一次
///  2. <see cref="UnLoaded"/> 卸载时清理订阅，避免 Singleton 服务上的事件处理器泄漏
///  3. 提供可选的考试名 / 配置名过滤（通过 <see cref="IExamEventFilterSettings"/>）
/// </summary>
public abstract class ExamEventTriggerBase<TTrigger, TSettings> : TriggerBase<TSettings>
    where TSettings : class
    where TTrigger : ExamEventTriggerBase<TTrigger, TSettings>
{
    private bool _subscribed;

    /// <summary>将事件 data 与 settings.ExamNameFilter 等是否通过；返回 true 表示放行。</summary>
    protected bool MatchesFilter(ExamEventData data) =>
        ExamEventFilterHelper.MatchesFilter(Settings as IExamEventFilterSettings, data);

    /// <summary>安全取连接服务：未注册时返回 null，子类必须容忍 null。</summary>
    protected static ExamAwareConnectionService? TryGetConnectionService()
    {
        try { return IAppHost.GetService<ExamAwareConnectionService>(); }
        catch { return null; }
    }

    /// <summary>真正订阅 / 重新订阅事件，应由子类覆写。</summary>
    protected abstract void Subscribe(ExamAwareConnectionService service);

    /// <summary>真正取消订阅，应由子类覆写。</summary>
    protected abstract void Unsubscribe(ExamAwareConnectionService service);

    /// <summary>服务是否处于"应当立即补发一次"的状态；由子类按需覆写。</summary>
    protected virtual bool ShouldReplayOnLoad(ExamAwareConnectionService service) => false;

    /// <summary>补发一次（仅当 <see cref="ShouldReplayOnLoad"/> 返回 true 时调用）。</summary>
    protected virtual void ReplayCurrent(ExamAwareConnectionService service) { }

    public override void Loaded()
    {
        var service = TryGetConnectionService();
        if (service == null) return;
        Subscribe(service);
        _subscribed = true;
        if (ShouldReplayOnLoad(service))
        {
            try { ReplayCurrent(service); }
            catch (Exception ex) { SafeLogWarning(ex, "触发器补发时出现异常"); }
        }
    }

    public override void UnLoaded()
    {
        if (!_subscribed) return;
        var service = TryGetConnectionService();
        if (service != null)
        {
            try { Unsubscribe(service); }
            catch (Exception ex) { SafeLogWarning(ex, "触发器取消订阅时出现异常"); }
        }
        _subscribed = false;
    }

    private void SafeLogWarning(Exception ex, string msg)
    {
        try
        {
            IAppHost.TryGetService<ILoggerFactory>()?
                .CreateLogger(GetType().Name)?
                .LogWarning(ex, msg);
        }
        catch
        {
            // 静默：日志系统不可用时不应影响触发器
        }
    }
}
