using System;
using Cysharp.Text;
using Match3.Core.Interfaces;

namespace Match3.Core.Logic;

public class ConsoleGameLogger : IGameLogger
{
    public void LogInfo(string message) => Log(ConsoleColor.White, "INFO", message);
    public void LogInfo<T>(string template, T arg1) => Log(ConsoleColor.White, "INFO", template, arg1);
    public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2) => Log(ConsoleColor.White, "INFO", template, arg1, arg2);
    public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3) => Log(ConsoleColor.White, "INFO", template, arg1, arg2, arg3);

    public void LogWarning(string message) => Log(ConsoleColor.Yellow, "WARN", message);
    public void LogWarning<T>(string template, T arg1) => Log(ConsoleColor.Yellow, "WARN", template, arg1);

    public void LogError(string message, Exception? ex = null)
    {
        Log(ConsoleColor.Red, "ERROR", message);
        if (ex != null)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private void Log(ConsoleColor color, string level, string message)
    {
        Console.ForegroundColor = color;
        // Using ZString to format on stack if possible, though Console.WriteLine will eventually alloc.
        // But here we avoid the input string allocation from the caller.
        Console.WriteLine(ZString.Format("[{0}] {1}", level, message));
        Console.ResetColor();
    }

    private void Log<T>(ConsoleColor color, string level, string template, T arg1)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(ZString.Format("[{0}] " + template, level, arg1));
        Console.ResetColor();
    }

    private void Log<T1, T2>(ConsoleColor color, string level, string template, T1 arg1, T2 arg2)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(ZString.Format("[{0}] " + template, level, arg1, arg2));
        Console.ResetColor();
    }

    private void Log<T1, T2, T3>(ConsoleColor color, string level, string template, T1 arg1, T2 arg2, T3 arg3)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(ZString.Format("[{0}] " + template, level, arg1, arg2, arg3));
        Console.ResetColor();
    }
}
