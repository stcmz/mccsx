﻿using System;

namespace mccsx.Helpers;

internal static class Logger
{
    private static readonly object lockObj = new();

    public static void Info(string message)
    {
        lock (lockObj)
        {
            Console.WriteLine(message);
        }
    }

    public static void Warning(string message)
    {
        lock (lockObj)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine($"WARN: {message}");
            Console.ResetColor();
        }
    }

    public static void Error(string message)
    {
        lock (lockObj)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"ERROR: {message}");
            Console.ResetColor();
        }
    }
}
