using System;
using Match3.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Match3.Web.Services;

public class MicrosoftGameLogger : IGameLogger
{
    private readonly ILogger _logger;

    public MicrosoftGameLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void LogInfo(string message) => _logger.LogInformation(message);
    public void LogInfo<T>(string template, T arg1) => _logger.LogInformation(template, arg1);
    public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2) => _logger.LogInformation(template, arg1, arg2);
    public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3) => _logger.LogInformation(template, arg1, arg2, arg3);

    public void LogWarning(string message) => _logger.LogWarning(message);
    public void LogWarning<T>(string template, T arg1) => _logger.LogWarning(template, arg1);

    public void LogError(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            _logger.LogError(ex, message);
        }
        else
        {
            _logger.LogError(message);
        }
    }
}
