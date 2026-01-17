using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

class Program
{
    // HTTP endpoint where this launcher will listen
    // Example: http://localhost:5000/startprogram
    private const string HttpPrefix = "http://+:5000/";

    // Default Unity exe path (can be overridden by first command-line argument)
    private static readonly string DefaultUnityExePath =
        @"C:\Ritual\LaserRitual.exe";

    // This is the path that will actually be used (default or from args)
    private static string UnityExePath = DefaultUnityExePath;

    static int Main(string[] args)
    {
        try
        {
            // Allow overriding the exe path from command line
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                UnityExePath = args[0];
                Console.WriteLine($"[INFO] Unity exe path overridden from args: {UnityExePath}");
            }
            else
            {
                Console.WriteLine($"[INFO] Using default Unity exe path: {UnityExePath}");
            }

            // ---- HTTP SERVER SETUP ----
            var listener = new HttpListener();
            listener.Prefixes.Add(HttpPrefix);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine("ERROR: Could not start HttpListener.");
                Console.WriteLine("You probably need to reserve the URL with netsh, for example:");
                Console.WriteLine(@"  netsh http add urlacl url=http://+:5000/ user=DOMAIN\\User");
                Console.WriteLine();
                Console.WriteLine(ex);
                return 1;
            }

            Console.WriteLine("====================================");
            Console.WriteLine(" Unity Launcher HTTP");
            Console.WriteLine("====================================");
            Console.WriteLine($"Listening on: {HttpPrefix}");
            Console.WriteLine("Available endpoints:");
            Console.WriteLine("  GET /startprogram  -> start Unity game");
            Console.WriteLine("  GET /health        -> health check (returns OK)");
            Console.WriteLine("====================================");
            Console.WriteLine("Press Ctrl+C to exit.");
            Console.WriteLine();

            // ---- MAIN LOOP: handle requests forever ----
            while (true)
            {
                HttpListenerContext? context = null;
                try
                {
                    // Blocking call, waits for a request
                    context = listener.GetContext();
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine("Listener stopped or error occurred:");
                    Console.WriteLine(ex);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener closed, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unexpected error accepting HTTP request:");
                    Console.WriteLine(ex);
                    continue;
                }

                // Handle the request in the same thread (simple, enough for this use case)
                HandleRequest(context);
            }

            listener.Close();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FATAL ERROR in UnityLauncher:");
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        string path = request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
        string method = request.HttpMethod.ToUpperInvariant();

        Console.WriteLine($"[{DateTime.Now:O}] {method} {path}");

        try
        {
            if (method == "GET" && path == "/startprogram")
            {
                // Call our Unity starter
                StartUnity(out string message, out int statusCode);

                response.StatusCode = statusCode;
                WriteString(response, message);
            }
            else if (method == "GET" && path == "/health")
            {
                response.StatusCode = 200;
                WriteString(response, "OK");
            }
            // Add a method that receives /execute and then the path to the specific program to execute and runs it
            else if (method == "GET" && path.StartsWith("/execute"))
            {
                string programPath = path.Substring("/execute".Length).TrimStart('/');
                programPath = Path.Combine(@"c:\Ritual\", programPath);

                if (string.IsNullOrWhiteSpace(programPath))
                {
                    response.StatusCode = 400;
                    WriteString(response, "No program specified.");
                }
                else if (!File.Exists(programPath))
                {
                    response.StatusCode = 404;
                    WriteString(response, $"Program not found: {programPath}");
                }
                else
                {
                    try
                    {
                        Process.Start(programPath);
                        response.StatusCode = 200;
                        WriteString(response, $"Started program: {programPath}");
                    }
                    catch (Exception ex)
                    {
                        response.StatusCode = 500;
                        WriteString(response, $"Error starting program: {ex.Message}");
                    }
                }
            }
            else
            {
                response.StatusCode = 404;
                WriteString(response, "Not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR handling request:");
            Console.WriteLine(ex);
            response.StatusCode = 500;
            WriteString(response, "Internal server error");
        }
        finally
        {
            response.Close();
        }
    }

    private static void WriteString(HttpListenerResponse response, string content)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = buffer.Length;

        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
    }

    // ---- Your original Unity-start logic, adapted into a method ----
    private static void StartUnity(out string message, out int httpStatusCode)
    {
        try
        {
            string unityExePath = UnityExePath;

            if (string.IsNullOrWhiteSpace(unityExePath))
            {
                message = "ERROR: No Unity exe path specified.";
                httpStatusCode = 500;
                return;
            }

            if (!File.Exists(unityExePath))
            {
                message = $"ERROR: Unity exe not found at:\n  {unityExePath}";
                httpStatusCode = 500;
                return;
            }

            // Avoid starting if already running (uses exe name without extension)
            string processName = Path.GetFileNameWithoutExtension(unityExePath);
            bool alreadyRunning = Process.GetProcessesByName(processName).Any();

            if (alreadyRunning)
            {
                message = $"INFO: \"{processName}\" is already running. Not starting another instance.";
                httpStatusCode = 200;
                return;
            }

            // No extra args for now; you can add them here if needed
            string arguments = "";

            var startInfo = new ProcessStartInfo
            {
                FileName = unityExePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(unityExePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = false  // true if you want to hide the Unity window (usually false)
            };

            Console.WriteLine($"Starting Unity app:\n  {unityExePath}");
            if (!string.IsNullOrEmpty(arguments))
                Console.WriteLine($"With arguments:\n  {arguments}");

            Process? proc = Process.Start(startInfo);

            if (proc == null)
            {
                message = "ERROR: Failed to start process (Process.Start returned null).";
                httpStatusCode = 500;
                return;
            }

            Console.WriteLine($"Unity process started, PID: {proc.Id}");
            message = $"Unity process started, PID: {proc.Id}";
            httpStatusCode = 200;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: Exception while starting Unity app:");
            Console.WriteLine(ex);
            message = "ERROR: Exception while starting Unity app:\n" + ex;
            httpStatusCode = 500;
        }
    }

    // Keeps your helper in case you later want to add args
    private static string QuoteIfNeeded(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return "\"\"";

        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }
}
