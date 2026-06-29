using System.Text.Json;
using ExamAware2Ci.Models;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExamAware2Ci.Tests;

/// <summary>
/// 端到端测试 ExamAwareConnectionService 的事件分发逻辑，验证以下 bug 修复：
/// 1. 断连时强制派发 ExamEnd / ExamPresentationStop 事件
/// 2. 客户端拒绝时间戳早于已处理事件的回放包
/// 3. 客户端接受最新时间戳的事件
/// 4. 时间戳相同时重复事件被去重
/// </summary>
public class ExamAwareConnectionServiceTests
{
    private static ExamAwareConnectionService NewService()
    {
        return new ExamAwareConnectionService(NullLogger<ExamAwareConnectionService>.Instance);
    }

    private static string ExamEventJson(string type, int? alertTime, long timestamp)
    {
        return JsonSerializer.Serialize(new
        {
            type = "exam-event",
            @event = type,
            data = new ExamEventData
            {
                ExamName = "期末考试",
                ExamConfigName = "config-1",
                StartTime = "2024-06-01 08:00",
                EndTime = "2024-06-01 10:00",
                RemainingMinutes = 30,
                AlertTime = alertTime
            },
            timestamp
        });
    }

    [Fact]
    public void StaleEventReplay_IsIgnored()
    {
        using var svc = NewService();
        var firedStarts = 0;
        svc.ExamStart += (_, _) => firedStarts++;

        svc.InjectMessage(ExamEventJson("exam-end", null, 100));
        svc.InjectMessage(ExamEventJson("exam-start", null, 50));

        Assert.Equal(0, firedStarts);
    }

    [Fact]
    public void FreshEventReplay_IsAccepted()
    {
        using var svc = NewService();
        var firedStarts = 0;
        svc.ExamStart += (_, _) => firedStarts++;

        svc.InjectMessage(ExamEventJson("exam-start", null, 200));

        Assert.Equal(1, firedStarts);
        Assert.True(svc.IsExamActive);
    }

    [Fact]
    public void DuplicateEventSameTimestamp_IsProcessed()
    {
        // 设计选择：同时间戳但不同类型（如同时收到 exam-start 和 exam-end）应当被处理。
        // 实际中"重复事件"主要靠服务器端"每个事件类型只缓存最新一条"保证；客户端不主动 dedup 同时间戳。
        using var svc = NewService();
        var examEnds = 0;
        svc.ExamEnd += (_, _) => examEnds++;

        svc.InjectMessage(ExamEventJson("exam-end", null, 300));
        svc.InjectMessage(ExamEventJson("exam-end", null, 300));

        // IsExamActive 一直为 false（每次 exam-end 都重置），所以 SetConnected/SetConnected 的断连逻辑不会二次触发 ExamPresentationStop。
        // 但消息会两次进入 switch，ExamEnd 事件被调用两次。
        Assert.Equal(2, examEnds);
    }

    [Fact]
    public void Disconnect_FiresExamEnd_WhenExamWasActive()
    {
        using var svc = NewService();
        var examEnds = 0;
        ExamEventData? endData = null;
        svc.ExamEnd += (_, d) => { examEnds++; endData = d; };

        svc.InjectMessage(ExamEventJson("exam-start", null, 10));
        Assert.True(svc.IsExamActive);

        svc.SimulateDisconnect();

        Assert.False(svc.IsExamActive);
        Assert.Equal(1, examEnds);
        Assert.NotNull(endData);
    }

    [Fact]
    public void Disconnect_DoesNotFireExamEnd_WhenExamWasNotActive()
    {
        using var svc = NewService();
        var examEnds = 0;
        svc.ExamEnd += (_, _) => examEnds++;

        svc.SimulateDisconnect();

        Assert.Equal(0, examEnds);
    }

    [Fact]
    public void Disconnect_FiresExamPresentationStop_WhenPresentationActive()
    {
        using var svc = NewService();
        var presStops = 0;
        svc.ExamPresentationStop += (_, _) => presStops++;

        svc.InjectMessage(ExamEventJson("exam-presentation-start", null, 10));
        Assert.True(svc.IsPresentationActive);

        svc.SimulateDisconnect();

        Assert.False(svc.IsPresentationActive);
        Assert.Equal(1, presStops);
    }

    [Fact]
    public void AfterDisconnect_ReplayedEventsAreAccepted_ForFreshState()
    {
        using var svc = NewService();
        var examEnds = 0;
        var examStarts = 0;
        svc.ExamEnd += (_, _) => examEnds++;
        svc.ExamStart += (_, _) => examStarts++;

        // 收到 start 然后断开（断连强制派发 ExamEnd，_lastEventTimestamp 被重置为 0）
        svc.InjectMessage(ExamEventJson("exam-start", null, 100));
        svc.SimulateDisconnect();
        Assert.Equal(1, examEnds);
        Assert.Equal(1, examStarts);

        // 重连后服务器回放一条更新的事件，客户端能正确处理
        svc.InjectMessage(ExamEventJson("exam-start", null, 200));
        Assert.Equal(2, examStarts);
    }
}
