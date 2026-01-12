using System;
using Match3.Core.Utility;

namespace Match3.ConfigTool;

public class ConsoleGameLogger : IGameLogger
{
    public void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
    public void LogInfo<T>(string template, T arg1) => Console.WriteLine($"[INFO] {template} | {arg1}");
    public void LogInfo<T1, T2>(string template, T1 arg1, T2 arg2) => Console.WriteLine($"[INFO] {template} | {arg1}, {arg2}");
    public void LogInfo<T1, T2, T3>(string template, T1 arg1, T2 arg2, T3 arg3) => Console.WriteLine($"[INFO] {template} | {arg1}, {arg2}, {arg3}");

    public void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
    public void LogWarning<T>(string template, T arg1) => Console.WriteLine($"[WARN] {template} | {arg1}");

    public void LogError(string message, Exception? ex = null)
    {
        Console.WriteLine($"[ERROR] {message}");
        if (ex != null)
        {
            Console.WriteLine(ex);
        }
    }
}
