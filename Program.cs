using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    // Change this to your Unity-built exe path, or pass it as the first argument
    private static readonly string DefaultUnityExePath =
        @"C:\Ritual\LaserRitual.exe";

    static int Main(string[] args)
    {
        try
        {
            // 1) Determine which exe to start
            string unityExePath = args.Length > 0 ? args[0] : DefaultUnityExePath;

            if (string.IsNullOrWhiteSpace(unityExePath))
            {
                Console.WriteLine("ERROR: No Unity exe path specified.");
                Console.WriteLine("Usage: UnityLauncher.exe \"C:\\path\\to\\MyGame.exe\"");
                return 1;
            }

            if (!File.Exists(unityExePath))
            {
                Console.WriteLine($"ERROR: Unity exe not found at:\n  {unityExePath}");
                return 1;
            }

            // 2) Optional: avoid starting if already running
            // (Uses the exe file name without extension as process name)
            string processName = Path.GetFileNameWithoutExtension(unityExePath);
            bool alreadyRunning = Process.GetProcessesByName(processName).Any();

            if (alreadyRunning)
            {
                Console.WriteLine($"INFO: \"{processName}\" is already running. Not starting another instance.");
                return 0;
            }

            // 3) Build arguments (any extra args passed to this launcher)
            string arguments = "";
            if (args.Length > 1)
            {
                // Everything after the first arg will be passed to Unity
                var extraArgs = args.Skip(1)
                                    .Select(a => QuoteIfNeeded(a));
                arguments = string.Join(" ", extraArgs);
            }

            // 4) Configure process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = unityExePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(unityExePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false, // good default for services / scripts
                CreateNoWindow = false   // set to true if you don't want a console window
            };

            Console.WriteLine($"Starting Unity app:\n  {unityExePath}");
            if (!string.IsNullOrEmpty(arguments))
                Console.WriteLine($"With arguments:\n  {arguments}");

            Process? proc = Process.Start(startInfo);

            if (proc == null)
            {
                Console.WriteLine("ERROR: Failed to start process (Process.Start returned null).");
                return 1;
            }

            Console.WriteLine($"Unity process started, PID: {proc.Id}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: Exception while starting Unity app:");
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static string QuoteIfNeeded(string arg)
    {
        // Adds quotes if the arg has spaces, simple helper
        if (string.IsNullOrWhiteSpace(arg))
            return "\"\"";

        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }
}
