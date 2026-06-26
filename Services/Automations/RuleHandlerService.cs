using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ExamAware2Ci.Models.Automations.Rules;
using ExamAware2Ci.Services;
using Microsoft.Extensions.Logging;

namespace ExamAware2Ci.Services.Automations;

public class RuleHandlerService
{
    private readonly ILogger<RuleHandlerService> _logger;
    private readonly IRulesetService _rulesetService;
    private readonly ExamAwareConnectionService _connectionService;

    public RuleHandlerService(
        ILogger<RuleHandlerService> logger,
        IRulesetService rulesetService,
        ExamAwareConnectionService connectionService)
    {
        _logger = logger;
        _rulesetService = rulesetService;
        _connectionService = connectionService;
    }

    public void Register()
    {
        // 订阅考试事件，刷新规则状态
        _connectionService.ExamPresentationStart += OnExamPresentationStart;
        _connectionService.ExamPresentationStop += OnExamPresentationStop;
        _connectionService.ExamStart += OnExamStart;
        _connectionService.ExamEnd += OnExamEnd;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        // 注册规则处理器
        _rulesetService.RegisterRuleHandler(Plugin.ExamAware2CiIds.ExamPlayingRule, HandleExamPlaying);
        _logger.LogInformation("规则处理器已注册");
    }

    public void Unregister()
    {
        _connectionService.ExamPresentationStart -= OnExamPresentationStart;
        _connectionService.ExamPresentationStop -= OnExamPresentationStop;
        _connectionService.ExamStart -= OnExamStart;
        _connectionService.ExamEnd -= OnExamEnd;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;

        // 注意：IRulesetService 未提供注销规则处理器的 API，这里仅取消事件订阅
        _logger.LogInformation("规则处理器事件订阅已注销");
    }

    private void OnExamPresentationStart(object? sender, Models.ExamEventData e)
    {
        _logger.LogTrace("考试放映开始，刷新规则集");
        Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
    }

    private void OnExamPresentationStop(object? sender, Models.ExamEventData e)
    {
        _logger.LogTrace("考试放映停止，刷新规则集");
        Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
    }

    private void OnExamStart(object? sender, Models.ExamEventData e)
    {
        _logger.LogTrace("考试开始，刷新规则集");
        Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
    }

    private void OnExamEnd(object? sender, Models.ExamEventData e)
    {
        _logger.LogTrace("考试结束，刷新规则集");
        Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        _logger.LogTrace("连接状态变化，刷新规则集");
        Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
    }

    private bool HandleExamPlaying(object? objectSettings)
    {
        if (!_connectionService.IsConnected)
        {
            return false;
        }

        var data = _connectionService.LastEventData;
        if (data == null)
        {
            return false;
        }

        // 检查是否有正在进行的考试（放映中或考试已开始未结束）
        var isExamPlaying = _connectionService.IsPresentationActive || _connectionService.IsExamActive;
        if (!isExamPlaying)
        {
            return false;
        }

        if (objectSettings is ExamPlayingRuleSettings settings && settings.FilterByExamName)
        {
            return data.ExamName == settings.ExamName;
        }

        return true;
    }
}
