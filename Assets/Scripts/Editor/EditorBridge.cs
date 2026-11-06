using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GeoWar.Game;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GeoWar.EditorTools
{
    // Açık duran Unity ile konuşmak için küçük yerel sunucu. Pencere açıp kapamadan derleme, Play ve test yapılır.
    // Sadece 127.0.0.1 dinler. Dosya yolu almaz, web sayfalarından gelen istekleri reddeder.
    // Komut listesi: GET /help
    [InitializeOnLoad]
    public static class EditorBridge
    {
        private const int Port = 47800;
        private const int MaxRequestBytes = 8192;
        private const int SocketTimeoutMs = 5000;
        private const int CommandTimeoutMs = 20000;
        private const int MaxLogLines = 300;
        private const int ErrorStackLines = 4;
        private const string EnabledFile = "UserSettings/GeoWarBridge.enabled";
        private const string ScreenshotFile = "Temp/GeoWarBridge/screenshot.png";
        private const string RerunStateKey = "GeoWarBridge.RerunState";
        private const string CompileErrorsKey = "GeoWarBridge.CompileErrors";

        private const int RerunIdle = 0;
        private const int RerunRefresh = 1;
        private const int RerunWaitCompile = 2;

        private const string HelpText =
            "status | refresh | play | stop | rerun | logs?since=N | screenshot | "
            + "state | tap?x=&y= | tapText?text= | pan?dx=&dy= | zoom?f= | speed?v= | select?name= | labels?overflow=1";

        private sealed class Pending
        {
            public string Path;
            public Dictionary<string, string> Query;
            public string Response;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static readonly ConcurrentQueue<Pending> Queue = new ConcurrentQueue<Pending>();
        private static readonly List<string> Logs = new List<string>();
        private static readonly object LogLock = new object();

        private static TcpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;
        private static long _logSequence;

        static EditorBridge()
        {
            // Varsayılan kapalı: Play ve derleme Unity penceresini öne getirip kullanıcıyı rahatsız ediyordu.
            if (!File.Exists(EnabledFile))
            {
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.update += OnEditorUpdate;

            // Unity arka plandayken de komut işlesin; bu sayede pencereyi öne getirmek gerekmez.
            GameDebugApi.Pump = OnEditorUpdate;
            KeepRunningInBackground();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            Application.logMessageReceivedThreaded += OnLog;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            StartServer();
        }

        // Bu ayar kapalıyken Unity arka planda hiç çalışmaz, köprü de cevap veremez.
        // Proje ayarı oturum açıkken hemen etkilemediği için çalışan değer de ayrıca açılır; Play'den çıkınca sıfırlandığı için tekrarlanır.
        private static void KeepRunningInBackground()
        {
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
            }

            Application.runInBackground = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            KeepRunningInBackground();
        }

        private static void StartServer()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _running = true;
                _thread = new Thread(ListenLoop) { IsBackground = true, Name = "GeoWarBridge" };
                _thread.Start();
            }
            catch (SocketException)
            {
                // Port doluysa (ör. projenin ikinci kopyası açıksa) araç sessizce devre dışı kalır.
                _running = false;
                _listener = null;
            }
        }

        private static void Stop()
        {
            _running = false;
            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }

            if (_thread != null)
            {
                _thread.Join(500);
                _thread = null;
            }
        }

        private static void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    using (TcpClient client = _listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = SocketTimeoutMs;
                        client.SendTimeout = SocketTimeoutMs;
                        HandleClient(client);
                    }
                }
                catch (SocketException)
                {
                    // Sunucu kapanırken bekleyen kabul bu hatayla biter.
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (IOException)
                {
                    // İstemci bağlantıyı erken kapattı.
                }
            }
        }

        private static void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            string request = ReadRequest(stream);
            string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] parts = lines[0].Split(' ');
            if (parts.Length < 2 || parts[0] != "GET" || !IsLocalRequest(lines))
            {
                WriteResponse(stream, "403 Forbidden", Failure("forbidden"));
                return;
            }

            var pending = new Pending();
            ParseTarget(parts[1], pending);
            Queue.Enqueue(pending);

            bool finished = pending.Done.Wait(CommandTimeoutMs);
            WriteResponse(stream, "200 OK", finished ? pending.Response : Failure("timeout"));
        }

        private static string ReadRequest(NetworkStream stream)
        {
            var buffer = new byte[MaxRequestBytes];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0)
                {
                    break;
                }

                total += read;
                string text = Encoding.UTF8.GetString(buffer, 0, total);
                if (text.Contains("\r\n\r\n"))
                {
                    return text;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, total);
        }

        // Tarayıcı Origin yollar; başka bir adla gelen istek de yerel sayılmaz.
        private static bool IsLocalRequest(string[] lines)
        {
            bool hostOk = false;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].ToLowerInvariant();
                if (line.StartsWith("origin:", StringComparison.Ordinal))
                {
                    return false;
                }

                if (line.StartsWith("host:", StringComparison.Ordinal))
                {
                    string host = line.Substring(5).Trim();
                    hostOk = host == "127.0.0.1:" + Port || host == "localhost:" + Port;
                }
            }

            return hostOk;
        }

        private static void ParseTarget(string target, Pending pending)
        {
            pending.Query = new Dictionary<string, string>(StringComparer.Ordinal);
            int mark = target.IndexOf('?');
            pending.Path = (mark < 0 ? target : target.Substring(0, mark)).TrimStart('/');
            if (mark < 0)
            {
                return;
            }

            foreach (string pair in target.Substring(mark + 1).Split('&'))
            {
                int equals = pair.IndexOf('=');
                if (equals > 0)
                {
                    pending.Query[Uri.UnescapeDataString(pair.Substring(0, equals))] = Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' '));
                }
            }
        }

        private static void WriteResponse(NetworkStream stream, string status, string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string head = "HTTP/1.1 " + status + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n";
            byte[] headBytes = Encoding.ASCII.GetBytes(head);
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(content, 0, content.Length);
        }

        private static void OnEditorUpdate()
        {
            AdvanceRerun();

            Pending pending;
            while (Queue.TryDequeue(out pending))
            {
                try
                {
                    pending.Response = Execute(pending);
                }
                catch (Exception exception)
                {
                    pending.Response = Failure(exception.GetType().Name + ": " + exception.Message);
                }

                pending.Done.Set();
            }
        }

        private static string Execute(Pending pending)
        {
            switch (pending.Path)
            {
                case "help":
                    return Success(HelpText);
                case "status":
                    return Status();
                case "refresh":
                    AssetDatabase.Refresh();
                    return Success("refresh");
                case "play":
                    EditorApplication.isPlaying = true;
                    return Success("play");
                case "stop":
                    EditorApplication.isPlaying = false;
                    return Success("stop");
                case "rerun":
                    return Rerun();
                case "logs":
                    return ReadLogs(pending.Query);
                case "screenshot":
                    return Screenshot();
                default:
                    return ExecuteGame(pending);
            }
        }

        private static string ExecuteGame(Pending pending)
        {
            if (!GameDebugApi.IsRunning)
            {
                return Failure("game is not running");
            }

            Dictionary<string, string> query = pending.Query;
            switch (pending.Path)
            {
                case "state":
                    return GameDebugApi.State();
                case "tap":
                    return Success(GameDebugApi.Tap(Number(query, "x"), Number(query, "y")));
                case "tapText":
                    return Success(GameDebugApi.TapText(query["text"]));
                case "pan":
                    GameDebugApi.Pan(Number(query, "dx"), Number(query, "dy"));
                    return Success("pan");
                case "zoom":
                    GameDebugApi.Zoom(Number(query, "f"));
                    return Success("zoom");
                case "speed":
                    GameDebugApi.Speed((int)Number(query, "v"));
                    return Success("speed");
                case "select":
                    return GameDebugApi.SelectProvince(query["name"]) ? Success("selected") : Failure("province not found");
                case "labels":
                    return GameDebugApi.Labels(query.ContainsKey("overflow"));
                default:
                    return Failure("unknown command: " + pending.Path + ". " + HelpText);
            }
        }

        private static float Number(Dictionary<string, string> query, string key)
        {
            return float.Parse(query[key], System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Status()
        {
            var json = new JsonText();
            json.Add("ok", true);
            json.Add("isPlaying", EditorApplication.isPlaying);
            json.Add("isPaused", EditorApplication.isPaused);
            json.Add("isCompiling", EditorApplication.isCompiling);
            json.Add("isUpdating", EditorApplication.isUpdating);
            json.Add("unityFocused", UnityEditorInternal.InternalEditorUtility.isApplicationActive);
            json.Add("rerunState", SessionState.GetInt(RerunStateKey, RerunIdle));
            json.Add("logSequence", Interlocked.Read(ref _logSequence));
            json.AddRaw("compileErrors", "[" + SessionState.GetString(CompileErrorsKey, "") + "]");
            json.AddRaw("game", GameDebugApi.IsRunning ? GameDebugApi.State() : "null");
            return json.Close();
        }

        // Kodu yeniler, derlemeyi bekler, hata yoksa Play'e girer. Derleme sırasında sunucu yeniden başlar.
        private static string Rerun()
        {
            SessionState.SetInt(RerunStateKey, RerunRefresh);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            return Success("rerun started");
        }

        private static void AdvanceRerun()
        {
            // Oyun tamamen durmadan derleme başlatılmaz: ikisi birbirini bekler, Unity komut alamaz hale gelir.
            int state = SessionState.GetInt(RerunStateKey, RerunIdle);
            if (state == RerunIdle || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (state == RerunRefresh)
            {
                SessionState.SetInt(RerunStateKey, RerunWaitCompile);
                AssetDatabase.Refresh();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            SessionState.SetInt(RerunStateKey, RerunIdle);
            if (SessionState.GetString(CompileErrorsKey, "").Length == 0)
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static string Screenshot()
        {
            if (!EditorApplication.isPlaying)
            {
                return Failure("screenshot needs play mode");
            }

            string path = Path.GetFullPath(ScreenshotFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ScreenCapture.CaptureScreenshot(path);
            var json = new JsonText();
            json.Add("ok", true);
            json.Add("path", path);
            return json.Close();
        }

        private static string ReadLogs(Dictionary<string, string> query)
        {
            long since = 0;
            string value;
            if (query.TryGetValue("since", out value))
            {
                long.TryParse(value, out since);
            }

            var items = new StringBuilder("[");
            lock (LogLock)
            {
                long first = _logSequence - Logs.Count;
                for (int i = 0; i < Logs.Count; i++)
                {
                    if (first + i >= since)
                    {
                        items.Append(items.Length > 1 ? "," : "").Append(Logs[i]);
                    }
                }
            }

            var json = new JsonText();
            json.Add("ok", true);
            json.Add("sequence", Interlocked.Read(ref _logSequence));
            json.AddRaw("lines", items.Append(']').ToString());
            return json.Close();
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            string text = message;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                string[] stack = (stackTrace ?? "").Split('\n');
                text += "\n" + string.Join("\n", stack, 0, Math.Min(ErrorStackLines, stack.Length));
            }

            var json = new JsonText();
            json.Add("type", type.ToString());
            json.Add("text", text);
            string line = json.Close();

            lock (LogLock)
            {
                Logs.Add(line);
                if (Logs.Count > MaxLogLines)
                {
                    Logs.RemoveAt(0);
                }

                _logSequence++;
            }
        }

        private static void OnCompilationStarted(object context)
        {
            SessionState.SetString(CompileErrorsKey, "");
        }

        private static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            var errors = new StringBuilder(SessionState.GetString(CompileErrorsKey, ""));
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    errors.Append(errors.Length > 0 ? "," : "").Append(JsonText.Quote(message.message));
                }
            }

            SessionState.SetString(CompileErrorsKey, errors.ToString());
        }

        private static string Success(string message)
        {
            var json = new JsonText();
            json.Add("ok", true);
            json.Add("result", message);
            return json.Close();
        }

        private static string Failure(string message)
        {
            var json = new JsonText();
            json.Add("ok", false);
            json.Add("error", message);
            return json.Close();
        }
    }
}
