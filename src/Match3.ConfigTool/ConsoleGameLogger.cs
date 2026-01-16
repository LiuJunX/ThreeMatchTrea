using System;
using System.IO;
using Match3.Core.Utility;

namespace Match3.ConfigTool;

/// <summary>
/// IGameLogger implementation for command-line tools.
/// Uses TextWriter abstraction for testability while defaulting to Console output.
/// Note: This is a platform-specific implementation where Console output is intentional.
/// </summary>
public class ConsoleGameLogger : IGameLogger
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ConsoleGameLogger() : this(Console.Out, Console.Error)
    {
    }

    public ConsoleGameLogger(TextWriter output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public void LogInfo(string message)
    {
        _output.Write("[INFO] ");
        _output.WriteLine(message);
    }

    public void LogInfo<T>(string template, T arg1)
    {
        _output.Write("[INFO] ");
        _output.Write(template);
        _output.Write(" | ");
        _output.WriteLine(arg1?.ToString() ?? "null");
    }

    public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2)
    {
        _output.Write("[INFO] ");
        _output.Write(template);
        _output.Write(" | ");
        _output.Write(arg1?.ToString() ?? "null");
        _output.Write(", ");
        _output.WriteLine(arg2?.ToString() ?? "null");
    }

    public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3)
    {
        _output.Write("[INFO] ");
        _output.Write(template);
        _output.Write(" | ");
        _output.Write(arg1?.ToString() ?? "null");
        _output.Write(", ");
        _output.Write(arg2?.ToString() ?? "null");
        _output.Write(", ");
        _output.WriteLine(arg3?.ToString() ?? "null");
    }

    public void LogWarning(string message)
    {
        _output.Write("[WARN] ");
        _output.WriteLine(message);
    }

    public void LogWarning<T>(string template, T arg1)
    {
        _output.Write("[WARN] ");
        _output.Write(template);
        _output.Write(" | ");
        _output.WriteLine(arg1?.ToString() ?? "null");
    }

    public void LogError(string message, Exception? ex = null)
    {
        _error.Write("[ERROR] ");
        _error.WriteLine(message);
        if (ex != null)
        {
            _error.WriteLine(ex.ToString());
        }
    }
}
