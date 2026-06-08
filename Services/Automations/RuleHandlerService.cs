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
        _connectionService.ExamPresentationStart += (sender, e) =>
        {
            _logger.LogTrace("考试放映开始，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        _connectionService.ExamPresentationStop += (sender, e) =>
        {
            _logger.LogTrace("考试放映停止，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        _connectionService.ExamEnd += (sender, e) =>
        {
            _logger.LogTrace("考试结束，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        _connectionService.ConnectionStateChanged += (sender, connected) =>
        {
            _logger.LogTrace("连接状态变化，刷新规则集");
            Dispatcher.UIThread.Invoke(() => _rulesetService.NotifyStatusChanged());
        };

        // 注册规则处理器
        _rulesetService.RegisterRuleHandler("examaware2ci.rules.examPlaying", HandleExamPlaying);
        _logger.LogInformation("规则处理器已注册");
    }

    private bool HandleExamPlaying(object? objectSettings)
    {
        var data = _connectionService.LastEventData;
        if (data == null || !_connectionService.IsConnected)
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
