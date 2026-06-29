using System.Reflection;
using ExamAware2Ci.Services;

namespace ExamAware2Ci.Tests;

/// <summary>
/// 测试辅助类：通过反射调用 ExamAwareConnectionService 的私有方法，
/// 在不实际建立 IPC 连接的情况下验证事件分发、断连恢复、时间戳去重等逻辑。
/// </summary>
internal static class ExamAwareConnectionServiceTestAccess
{
    public static void InjectMessage(this ExamAwareConnectionService svc, string json)
    {
        var method = typeof(ExamAwareConnectionService)
            .GetMethod("ProcessMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("未找到 ProcessMessage");
        method.Invoke(svc, new object[] { json });
    }

    /// <summary>
    /// 模拟连接断开：不修改 IsExamActive/IsPresentationActive 的当前值，
    /// 只把 _isConnected 设为 true 再调用 SetConnected(false)，让 SetConnected
    /// 内部读取到的 "wasActive" 反映插件服务的真实状态。
    /// </summary>
    public static void SimulateDisconnect(this ExamAwareConnectionService svc)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var isConnectedField = typeof(ExamAwareConnectionService).GetField("_isConnected", flags)
            ?? throw new InvalidOperationException("未找到 _isConnected");
        isConnectedField.SetValue(svc, true);

        var setConnected = typeof(ExamAwareConnectionService).GetMethod("SetConnected", flags)
            ?? throw new InvalidOperationException("未找到 SetConnected");
        setConnected.Invoke(svc, new object[] { false });
    }
}
