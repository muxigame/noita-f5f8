using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

// No external libraries, game patches or administrator privileges are required.
namespace NoitaF5F8
{
    internal sealed class Options
    {
        public string Game;
        public string SaveRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Nolla_Games_Noita");
        public string DataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoitaF5F8");
        public bool SelfTest;
        public bool Quiet;
        public string Operation;
        public bool ManualContinue;
        public bool LaunchGame;
        public string InitialArguments = "";

        public static Options Parse(string[] args)
        {
            Options o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--self-test") o.SelfTest = true;
                else if (arg == "--quiet") o.Quiet = true;
                else if (arg == "--manual-continue") o.ManualContinue = true;
                else if (arg == "--launch-game")
                {
                    if (i + 1 >= args.Length) throw new ArgumentException("--launch-game requires the original Noita executable and optional game arguments.");
                    o.LaunchGame = true; o.Game = Path.GetFullPath(args[++i]);
                    o.InitialArguments = String.Join(" ", args.Skip(i + 1).Select(LaunchInfo.Quote).ToArray());
                    break; // All remaining arguments belong to Noita, not the helper.
                }
                else if (arg == "--save-now" || arg == "--load-now")
                {
                    if (o.Operation != null) throw new ArgumentException("Choose only one of --save-now and --load-now.");
                    o.Operation = arg;
                }
                else if ((arg == "--game" || arg == "--save-root" || arg == "--data-root") && i + 1 < args.Length)
                {
                    string value = Path.GetFullPath(args[++i]);
                    if (arg == "--game") o.Game = value;
                    else if (arg == "--save-root") o.SaveRoot = value;
                    else o.DataRoot = value;
                }
                else throw new ArgumentException("Unknown or incomplete option: " + arg);
            }
            if (!o.SelfTest)
            {
                if (o.Game == null)
                {
                    string guess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Noita", "noita.exe");
                    if (File.Exists(guess)) o.Game = guess;
                    else throw new ArgumentException("Specify --game with the full path to noita.exe.");
                }
                string name = Path.GetFileName(o.Game);
                if ((!name.Equals("noita.exe", StringComparison.OrdinalIgnoreCase) && !name.Equals("noita_dev.exe", StringComparison.OrdinalIgnoreCase)) || !File.Exists(o.Game))
                    throw new ArgumentException("--game must name an existing noita.exe or noita_dev.exe.");
                Paths.NoReparse(o.Game);
            }
            o.SaveRoot = Paths.Normalize(o.SaveRoot);
            o.DataRoot = Paths.Normalize(o.DataRoot);
            Paths.NotDriveRoot(o.SaveRoot);
            Paths.NotDriveRoot(o.DataRoot);
            if (Paths.Contains(o.SaveRoot, o.DataRoot) || Paths.Contains(o.DataRoot, o.SaveRoot))
                throw new ArgumentException("The save root and backup root must be separate directories.");
            if (o.LaunchGame && (o.Operation != null || o.SelfTest)) throw new ArgumentException("--launch-game cannot be combined with operation/test options.");
            return o;
        }
    }

    internal static class Paths
    {
        public static string Normalize(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        public static bool Contains(string root, string path)
        {
            root = Normalize(root); path = Normalize(path);
            return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        public static void NotDriveRoot(string path)
        {
            if (Normalize(path).Equals(Path.GetPathRoot(Path.GetFullPath(path)).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new IOException("A drive root cannot be used as a save or backup directory.");
        }
        public static void NoReparse(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                try { NoReparseEntry(current); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
        }
        public static void NoReparseEntry(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links and junctions are not supported: " + path);
        }
        private static void NoReparseEntry(FileSystemInfo entry)
        {
            // Directory enumeration already obtained these attributes. Reusing
            // them avoids a separate attribute query for every ancestor/file.
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links and junctions are not supported: " + entry.FullName);
        }
        public static string Child(string root, string relative)
        {
            if (String.IsNullOrEmpty(relative) || Path.IsPathRooted(relative)) throw new IOException("Invalid relative path.");
            string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!Contains(root, result) || Normalize(result).Equals(Normalize(root), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Path leaves the intended directory: " + relative);
            // This constructs a bounded path only. Actual I/O validates its root
            // and checks enumerated entries before traversing them.
            return result;
        }
        public static void MakeDirectory(string path) { NoReparse(path); Directory.CreateDirectory(path); NoReparse(path); }
        public static string Unique(string root, string prefix)
        {
            return Child(root, prefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
        public static void Move(string source, string destination, string allowedRoot)
        {
            if (!Contains(allowedRoot, source) || !Contains(allowedRoot, destination)) throw new IOException("Move leaves its intended directory.");
            NoReparse(source); NoReparse(destination);
            Directory.Move(source, destination);
        }
        public static IEnumerable<string> Files(string root)
        {
            NoReparse(root);
            return FilesUnderValidatedRoot(root);
        }
        private static IEnumerable<string> FilesUnderValidatedRoot(string root)
        {
            foreach (FileSystemInfo entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
            {
                NoReparseEntry(entry);
                if ((entry.Attributes & FileAttributes.Directory) == 0) yield return entry.FullName;
                else foreach (string file in FilesUnderValidatedRoot(entry.FullName)) yield return file;
            }
        }
        public static void CopyTree(string source, string destination)
        {
            NoReparse(source); NoReparse(destination);
            CopyTreeUnderValidatedRoots(source, destination);
        }
        private static void CopyTreeUnderValidatedRoots(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            NoReparseEntry(destination);
            foreach (FileSystemInfo entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
            {
                NoReparseEntry(entry);
                string target = Child(destination, entry.Name);
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                    CopyTreeUnderValidatedRoots(entry.FullName, target);
                else File.Copy(entry.FullName, target, false);
            }
        }
    }

    internal sealed class SnapshotStore
    {
        public readonly string SaveRoot;
        public readonly string DataRoot;
        public string Active { get { return Paths.Child(SaveRoot, "save00"); } }
        public string Quick { get { return Paths.Child(DataRoot, "quick"); } }
        public string RecoveryRoot { get { return Paths.Child(SaveRoot, "NoitaF5F8-Recovery"); } }
        public SnapshotStore(string saveRoot, string dataRoot)
        {
            SaveRoot = Paths.Normalize(saveRoot); DataRoot = Paths.Normalize(dataRoot);
            if (Paths.Contains(SaveRoot, DataRoot) || Paths.Contains(DataRoot, SaveRoot)) throw new IOException("Save and backup roots overlap.");
            Paths.NotDriveRoot(SaveRoot); Paths.NotDriveRoot(DataRoot);
            Paths.NoReparse(SaveRoot); Paths.NoReparse(DataRoot);
            Paths.MakeDirectory(DataRoot);
        }
        public static void RequireRun(string directory)
        {
            Paths.NoReparse(directory);
            foreach (string name in new string[] { "player.xml", "world_state.xml" })
            {
                string file = Paths.Child(directory, name);
                if (File.Exists(file)) Paths.NoReparseEntry(file);
                if (!File.Exists(file) || new FileInfo(file).Length == 0) throw new IOException("A complete run is required; missing or empty " + name + ". Enter a run and try F5 again.");
                try
                {
                    // Noita's serializer is XML-like, but mod VariableStorage strings
                    // can contain raw quotes. System.Xml rejects genuine saves. Check
                    // the native Entity envelope and completion markers, and preserve
                    // every original byte; only our own manifest uses strict XML.
                    string document = File.ReadAllText(file, new UTF8Encoding(false, true)).Trim();
                    if (!IsEntityOpen(document) || !document.EndsWith("</Entity>", StringComparison.Ordinal))
                        throw new IOException("The run appears incomplete or is not a Noita Entity document: " + name);
                    int depth = 0; bool closed = false;
                    using (StringReader lines = new StringReader(document))
                    {
                        string line;
                        while ((line = lines.ReadLine()) != null)
                        {
                            foreach (char c in line)
                                if (Char.IsControl(c) && c != '\t') throw new IOException("The run contains binary data in " + name);
                            string marker = line.Trim();
                            if (marker.Length == 0) continue;
                            if (closed) throw new IOException("Unexpected data after the root Entity in " + name);
                            if (IsEntityOpen(marker)) depth++;
                            if (marker.EndsWith("</Entity>", StringComparison.Ordinal))
                            {
                                depth--;
                                if (depth < 0) throw new IOException("Unbalanced Entity completion markers in " + name);
                                if (depth == 0) closed = true;
                            }
                        }
                    }
                    if (!closed || depth != 0) throw new IOException("The run has an incomplete Entity document in " + name);
                }
                catch (DecoderFallbackException error) { throw new IOException("The run contains invalid text data in " + name + ". No snapshot was changed.", error); }
            }
        }
        private static bool IsEntityOpen(string text)
        {
            return text.StartsWith("<Entity", StringComparison.Ordinal) && (text.Length == 7 || (text.Length > 7 && (Char.IsWhiteSpace(text[7]) || text[7] == '>')));
        }
        private static string Hash(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string Relative(string root, string file) { return file.Substring(Paths.Normalize(root).Length + 1).Replace('\\', '/'); }
        private static void WriteManifest(string directory)
        {
            string payload = Paths.Child(directory, "save00");
            using (XmlWriter writer = XmlWriter.Create(Paths.Child(directory, "manifest.xml"), new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
            {
                writer.WriteStartElement("NoitaF5F8"); writer.WriteAttributeString("version", "1");
                writer.WriteAttributeString("createdUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                foreach (string file in Paths.Files(payload).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteStartElement("File");
                    writer.WriteAttributeString("path", Relative(payload, file));
                    writer.WriteAttributeString("length", new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("sha256", Hash(file)); writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }
        public static void Validate(string directory)
        {
            Paths.NoReparse(directory);
            string payload = Paths.Child(directory, "save00");
            RequireRun(payload);
            Paths.NoReparseEntry(Paths.Child(directory, "manifest.xml"));
            XmlDocument manifest = new XmlDocument(); manifest.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(Paths.Child(directory, "manifest.xml"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) manifest.Load(reader);
            if (manifest.DocumentElement == null || manifest.DocumentElement.Name != "NoitaF5F8" || manifest.DocumentElement.GetAttribute("version") != "1") throw new IOException("Unsupported snapshot manifest.");
            Dictionary<string, XmlElement> expected = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode node in manifest.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element.Name != "File") throw new IOException("Invalid snapshot manifest entry.");
                string relative = element.GetAttribute("path");
                string full = Paths.Child(payload, relative);
                string canonical = Relative(payload, full);
                if (!relative.Equals(canonical, StringComparison.Ordinal) || expected.ContainsKey(relative)) throw new IOException("Invalid or duplicate snapshot path.");
                expected.Add(relative, element);
            }
            string[] files = Paths.Files(payload).ToArray();
            if (files.Length != expected.Count) throw new IOException("Snapshot file count does not match its manifest.");
            foreach (string file in files)
            {
                XmlElement entry;
                if (!expected.TryGetValue(Relative(payload, file), out entry) || entry.GetAttribute("length") != new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture) || !entry.GetAttribute("sha256").Equals(Hash(file), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Snapshot verification failed: " + Relative(payload, file));
            }
        }
        public void Save() { Save(null); }
        public void Save(Action beforeCommit)
        {
            RequireRun(Active);
            string stagingRoot = Paths.Child(DataRoot, "staging"); Paths.MakeDirectory(stagingRoot);
            string staged = Paths.Unique(stagingRoot, "save-"); Paths.MakeDirectory(staged);
            Paths.CopyTree(Active, Paths.Child(staged, "save00"));
            WriteManifest(staged); Validate(staged);
            if (beforeCommit != null) beforeCommit();
            string previous = null;
            if (Directory.Exists(Quick))
            {
                string history = Paths.Child(DataRoot, "history"); Paths.MakeDirectory(history);
                previous = Paths.Unique(history, "quick-"); Paths.Move(Quick, previous, DataRoot);
            }
            try { Paths.Move(staged, Quick, DataRoot); }
            catch
            {
                if (previous != null && !Directory.Exists(Quick)) Paths.Move(previous, Quick, DataRoot);
                throw;
            }
        }
        // Stages a verified snapshot without changing the active save. The F8
        // pipeline validates Quick before termination, then stages after exit.
        public string PrepareRestore()
        {
            Validate(Quick);
            Paths.MakeDirectory(RecoveryRoot);
            string staged = Paths.Unique(RecoveryRoot, "stage-"); Paths.MakeDirectory(staged);
            Paths.CopyTree(Paths.Child(Quick, "save00"), Paths.Child(staged, "save00"));
            File.Copy(Paths.Child(Quick, "manifest.xml"), Paths.Child(staged, "manifest.xml"));
            Validate(staged);
            return staged;
        }
        // Caller guarantees the game has exited. All moves stay on the save volume.
        public string CommitRestore(string staged)
        {
            if (!Paths.Contains(RecoveryRoot, staged)) throw new IOException("Restore stage is outside the recovery directory.");
            Validate(staged);
            string recovery = null;
            if (Directory.Exists(Active))
            {
                recovery = Paths.Unique(RecoveryRoot, "before-load-"); Paths.Move(Active, recovery, SaveRoot);
            }
            try { Paths.Move(Paths.Child(staged, "save00"), Active, SaveRoot); }
            catch
            {
                if (recovery != null && !Directory.Exists(Active)) Paths.Move(recovery, Active, SaveRoot);
                throw;
            }
            return recovery;
        }
    }

    public static class GameReadiness
    {
        public const string MarkerName = "noita_f5f8_gameplay_ready";
        public static string MarkerPath(string saveRoot)
        {
            return Paths.Child(saveRoot, "save00\\persistent\\flags\\" + MarkerName);
        }
        public static void Clear(string saveRoot)
        {
            string marker = MarkerPath(saveRoot); Paths.NoReparse(marker);
            if (File.Exists(marker)) File.Delete(marker); // Only this mod's fixed private marker.
        }
        public static bool IsReady(string saveRoot)
        {
            string marker = MarkerPath(saveRoot);
            if (!File.Exists(marker)) return false;
            Paths.NoReparse(marker);
            return File.Exists(marker);
        }
    }

    internal sealed class LaunchInfo
    {
        public string Exe;
        public string Arguments;
        [DllImport("shell32.dll", SetLastError = true)] private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int count);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        public static LaunchInfo Capture(Process process, Options options)
        {
            string exe = process.MainModule.FileName;
            if (!Path.GetFullPath(exe).Equals(options.Game, StringComparison.OrdinalIgnoreCase)) throw new IOException("The foreground game does not match --game.");
            string commandLine = null;
            using (ManagementObject query = new ManagementObject("Win32_Process.Handle='" + process.Id.ToString(CultureInfo.InvariantCulture) + "'")) commandLine = query["CommandLine"] as string;
            if (String.IsNullOrEmpty(commandLine)) throw new IOException("Cannot read the game's launch arguments safely.");
            int count;
            IntPtr vector = CommandLineToArgvW(commandLine, out count);
            if (vector == IntPtr.Zero || count == 0) throw new IOException("Cannot parse game launch arguments.");
            List<string> args = new List<string>();
            try
            {
                for (int i = 1; i < count; i++)
                {
                    string arg = Marshal.PtrToStringUni(Marshal.ReadIntPtr(vector, i * IntPtr.Size));
                    if (arg.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0 || arg.IndexOf("workshop_upload", StringComparison.OrdinalIgnoreCase) >= 0 || arg.StartsWith("-clean", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Custom save, clean and Workshop-upload launch arguments are not supported. Relaunch a normal save00 game first.");
                    args.Add(Quote(arg));
                }
            }
            finally { LocalFree(vector); }
            return new LaunchInfo { Exe = exe, Arguments = String.Join(" ", args.ToArray()) };
        }
        internal static string Quote(string value)
        {
            StringBuilder result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                slashes = 0; result.Append(c);
            }
            result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
        }
        internal static Process Start(string exe, string arguments)
        {
            Paths.NoReparse(exe);
            return Process.Start(new ProcessStartInfo { FileName = exe, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false });
        }
        public bool Restart(Options options, Action<string> log)
        {
            // Steam or the user may reopen the game while a large save is being
            // copied. Never add a second instance sharing the same save00.
            if (GameOperations.RunningGameCount() != 0)
            {
                log("Restart skipped: Noita is already running. The helper did not open a second instance.");
                return false;
            }
            string restartArguments = Arguments;
            if (restartArguments.IndexOf("\"-no_logo_splashes\"", StringComparison.OrdinalIgnoreCase) < 0)
                restartArguments = (restartArguments + " " + Quote("-no_logo_splashes")).Trim();
            GameReadiness.Clear(options.SaveRoot);
            using (Process restarted = Start(Exe, restartArguments))
            {
                if (!options.ManualContinue)
                {
                    bool resumed = NativeAutoContinue.TryResume(restarted, Exe, options.SaveRoot, log);
                    log(resumed ? "Restarted Noita and confirmed automatic Continue." : "Restarted Noita; automatic Continue was not confirmed. Continue manually if the menu remains open.");
                }
                else log("Automatic Continue disabled by --manual-continue.");
            }
            return true;
        }
    }

    internal static class NativeAutoContinue
    {
        private const string SupportedExeHash = "808D2A0AB51EA0B46E9AD2AEB3327A4B0CE3FEAE04F32BA26326BF585B5779BD";
        private const int MenuRva = 0xE0761B;
        private const int ContinueRva = 0xE07618;
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", SetLastError = true)] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr window);
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeMessage
        {
            public IntPtr Window; public uint Message; public UIntPtr WParam; public IntPtr LParam;
            public uint Time; public NativePoint Point; public uint Private;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint first, uint last, uint remove);
        private static bool FocusRestartedWindow(Process process, NativeMemory memory, Action<string> log)
        {
            if (memory.Exited) return false;
            process.Refresh(); IntPtr window = process.MainWindowHandle;
            uint owner = 0;
            uint gameThread = window == IntPtr.Zero ? 0 : GetWindowThreadProcessId(window, out owner);
            if (window == IntPtr.Zero || gameThread == 0 || owner != process.Id)
            {
                log("Automatic Continue skipped: the restarted process has no verified game window."); return false;
            }
            ShowWindow(window, 9); // SW_RESTORE; exactly this newly started PID's window.
            bool requested = SetForegroundWindow(window);
            uint foreground; GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
            if (foreground != process.Id)
            {
                // A background helper may be denied foreground activation by
                // Windows. Share the existing input queues briefly for this
                // verified window, without injecting keys or changing settings.
                NativeMessage message; PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
                uint currentThread = GetCurrentThreadId();
                uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                bool attachedForeground = false, attachedGame = false;
                try
                {
                    if (foregroundThread != 0 && foregroundThread != currentThread)
                        attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
                    if (gameThread != currentThread && gameThread != foregroundThread)
                        attachedGame = AttachThreadInput(currentThread, gameThread, true);
                    ShowWindow(window, 9); BringWindowToTop(window);
                    requested = SetForegroundWindow(window); SetActiveWindow(window); SetFocus(window);
                }
                finally
                {
                    if (attachedGame) AttachThreadInput(currentThread, gameThread, false);
                    if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
                }
                GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                log("One-shot focus queue fallback: foregroundAttached=" + attachedForeground + "; gameAttached=" + attachedGame + ".");
            }
            log("Restarted window focus attempt; SetForegroundWindow=" + requested + "; foregroundPID=" + foreground + "; restartedPID=" + process.Id + ".");
            return true;
        }
        internal sealed class NativeMemory : IDisposable
        {
            [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr read);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr written);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
            [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
            private IntPtr handle;
            public NativeMemory(int processId)
            {
                handle = OpenProcess(0x00100000 | 0x1000 | 0x0010 | 0x0020 | 0x0008, false, processId);
                if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the restarted game for automatic Continue.");
            }
            public bool Exited
            {
                get
                {
                    uint state = WaitForSingleObject(handle, 0);
                    if (state == 0xFFFFFFFF) throw new Win32Exception(Marshal.GetLastWin32Error());
                    return state == 0;
                }
            }
            public byte Read(IntPtr address)
            {
                byte[] bytes = new byte[1]; IntPtr read;
                if (!ReadProcessMemory(handle, address, bytes, new IntPtr(1), out read) || read.ToInt64() != 1)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the restarted game's menu state.");
                return bytes[0];
            }
            public void Write(IntPtr address, byte value)
            {
                IntPtr written;
                if (!WriteProcessMemory(handle, address, new byte[] { value }, new IntPtr(1), out written) || written.ToInt64() != 1)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot request automatic Continue.");
            }
            public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        }
        internal static bool RequestContinue(NativeMemory memory, IntPtr menu, IntPtr trigger, int timeoutMilliseconds, Action<string> log, Action validateRun, Action<byte, int, long> observe = null, Func<bool> gameplayReady = null)
        {
            Stopwatch elapsed = Stopwatch.StartNew(); int writes = 0;
            while (elapsed.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (memory.Exited) { log("Automatic Continue stopped: the restarted process exited."); return false; }
                if (gameplayReady != null && gameplayReady())
                {
                    log("Automatic Continue confirmed by the fresh gameplay marker; stopping native requests and focus maintenance."); return true;
                }
                byte state = memory.Read(menu);
                if (state > 1) { log("Automatic Continue skipped: unexpected menu state."); return false; }
                if (observe != null) observe(state, writes, elapsed.ElapsedMilliseconds);
                if (state == 0 && writes != 0)
                {
                    log("Automatic Continue completed; native requests=" + writes + "."); return true;
                }
                if (state == 1 && writes < 3000)
                {
                    if (writes == 0) { validateRun(); log("Automatic Continue: verified main menu; requesting Continue for this restarted PID only."); }
                    // The engine clears this native Continue flag each menu frame.
                    // Repeating only this flag never selects the New Game action.
                    memory.Write(trigger, 1); writes++;
                }
                Thread.Sleep(15);
            }
            log("Automatic Continue timed out; choose Continue manually. Native requests=" + writes + "."); return false;
        }
        public static bool TryResume(Process process, string exe, string saveRoot, Action<string> log)
        {
            try
            {
                string hash;
                using (SHA256 sha = SHA256.Create())
                using (FileStream file = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.Read))
                    hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                if (!hash.Equals(SupportedExeHash, StringComparison.OrdinalIgnoreCase))
                {
                    log("Automatic Continue unavailable for this game build; choose Continue manually."); return false;
                }
                using (NativeMemory memory = new NativeMemory(process.Id))
                {
                    ProcessModule module = null; Stopwatch ready = Stopwatch.StartNew();
                    while (ready.ElapsedMilliseconds < 10000 && !memory.Exited)
                    {
                        try { process.Refresh(); module = process.MainModule; if (module != null) break; }
                        catch (Win32Exception) { }
                        catch (InvalidOperationException) { }
                        Thread.Sleep(15);
                    }
                    if (module == null || !module.FileName.Equals(exe, StringComparison.OrdinalIgnoreCase) || module.ModuleMemorySize <= MenuRva)
                    {
                        log("Automatic Continue skipped: the restarted image could not be verified."); return false;
                    }
                    IntPtr menu = new IntPtr(module.BaseAddress.ToInt64() + MenuRva);
                    IntPtr trigger = new IntPtr(module.BaseAddress.ToInt64() + ContinueRva);
                    Stopwatch resume = Stopwatch.StartNew(); long lastFocus = -1000, lastDiagnostic = -5000; int focusAttempts = 0;
                    Func<bool> focusIfNeeded = delegate
                    {
                        uint foreground; GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                        if (foreground == process.Id || memory.Exited) return true;
                        if (focusAttempts >= 45 || resume.ElapsedMilliseconds - lastFocus < 1000) return true;
                        lastFocus = resume.ElapsedMilliseconds; focusAttempts++;
                        return FocusRestartedWindow(process, memory, log);
                    };
                    bool resumed = RequestContinue(memory, menu, trigger, 45000, log, delegate
                    {
                        SnapshotStore.RequireRun(Paths.Child(saveRoot, "save00"));
                        focusIfNeeded();
                    }, delegate(byte state, int writes, long elapsed)
                    {
                        uint foreground; GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                        if (elapsed - lastDiagnostic >= 5000)
                        {
                            lastDiagnostic = elapsed;
                            log("Automatic Continue progress: menu=" + state + "; foregroundPID=" + foreground + "; restartedPID=" + process.Id + "; requests=" + writes + "; focusAttempts=" + focusAttempts + ".");
                        }
                        // Startup/overlay activation can take focus back. Reacquire
                        // only while the verified menu remains up, at most 1/sec.
                        if (state == 1 && writes != 0) focusIfNeeded();
                    }, delegate { return GameReadiness.IsReady(saveRoot); });
                    if (resumed && !memory.Exited)
                    {
                        uint foreground; GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                        if (foreground != process.Id)
                        {
                            long spacing = 1000 - (resume.ElapsedMilliseconds - lastFocus);
                            if (spacing > 0) Thread.Sleep((int)spacing);
                            focusIfNeeded();
                        }
                        GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                        log("Automatic Continue final foregroundPID=" + foreground + "; restartedPID=" + process.Id + "; focusAttempts=" + focusAttempts + ".");
                    }
                    return resumed;
                }
            }
            catch (Exception error) { log("Automatic Continue unavailable; choose Continue manually. " + error.Message); return false; }
        }
    }

    internal static class GameOperations
    {
        private const string OperationMutexName = "Local\\NoitaF5F8-Operation";
        public static bool IsOperationBusy()
        {
            using (Mutex mutex = new Mutex(false, OperationMutexName))
            {
                bool acquired = false;
                try { try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; } return !acquired; }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }
        public static void StartRequestedGame(Options options, Action<string> log)
        {
            using (Mutex mutex = new Mutex(false, OperationMutexName))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) { log("Steam launch skipped: a save/load operation is already reopening Noita."); return; }
                    if (RunningGameCount() != 0) { log("Steam launch skipped: Noita is already running."); return; }
                    GameReadiness.Clear(options.SaveRoot);
                    using (Process initial = LaunchInfo.Start(options.Game, options.InitialArguments))
                        log("Steam wrapper launched Noita PID=" + initial.Id + "; initial menu is unchanged.");
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }
        internal sealed class ProcessMonitor : IDisposable
        {
            [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr handle, uint code);
            [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
            private IntPtr handle;
            public ProcessMonitor(int processId)
            {
                // Hold a real handle before shutdown. Attached .NET Process objects can
                // lose exit information once their externally-created process exits.
                handle = OpenProcess(0x00100000 | 0x1000 | 0x0001, false, processId);
                if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the running game safely.");
            }
            public bool Wait(uint milliseconds)
            {
                uint result = WaitForSingleObject(handle, milliseconds);
                if (result == 0) return true;
                if (result == 0x102) return false;
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot wait for the game to exit.");
            }
            public uint ExitCode
            {
                get { uint code; if (!GetExitCodeProcess(handle, out code)) throw new Win32Exception(Marshal.GetLastWin32Error()); return code; }
            }
            public void Kill()
            {
                if (!TerminateProcess(handle, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot terminate the selected game for restoring.");
            }
            public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        }
        internal static Dictionary<string, DateTime> SaveTimes(string active)
        {
            Dictionary<string, DateTime> times = new Dictionary<string, DateTime>();
            foreach (string name in new string[] { "player.xml", "world_state.xml" })
            {
                string file = Paths.Child(active, name);
                times.Add(name, File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue);
            }
            return times;
        }
        internal static void RequireFreshSave(string active, Dictionary<string, DateTime> before)
        {
            foreach (KeyValuePair<string, DateTime> file in before)
            {
                string path = Paths.Child(active, file.Key);
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) <= file.Value)
                    throw new IOException("Noita did not write a fresh " + file.Key + " during save-and-quit. Enter an active run before pressing F5. The previous snapshot was retained.");
            }
        }
        public static int RunningGameCount()
        {
            List<Process> processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName("noita"));
            processes.AddRange(Process.GetProcessesByName("noita_dev"));
            try { return processes.Count; }
            finally { foreach (Process process in processes) process.Dispose(); }
        }
        private static void RequireNoRunningGames()
        {
            if (RunningGameCount() != 0)
                throw new IOException("Noita was reopened before copying finished. The previous snapshot was retained. Keep only one game open, then press F5 and wait for the helper to restart it.");
        }
        public static Process SelectGame(Options options)
        {
            List<Process> processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName("noita"));
            processes.AddRange(Process.GetProcessesByName("noita_dev"));
            Process selected = null;
            try
            {
                if (processes.Count != 1) throw new IOException("Found " + processes.Count + " Noita instances. Close extra game windows, keep exactly one open, then retry F5/F8. While saving, wait for the helper to reopen Noita.");
                selected = processes[0];
                if (!selected.MainModule.FileName.Equals(options.Game, StringComparison.OrdinalIgnoreCase)) throw new IOException("The running game does not match --game.");
                processes.Remove(selected);
                return selected;
            }
            finally { foreach (Process process in processes) process.Dispose(); }
        }
        private static void Phase(Action<string> log, Action<string> progress, string message)
        {
            log(message);
            if (progress != null) progress(message);
        }
        public static string Execute(Options options, SnapshotStore store, Process process, bool load, Action<string> log, Action<string> progress = null)
        {
            using (Mutex operation = new Mutex(false, OperationMutexName))
            {
                bool owned = false;
                try
                {
                    try { owned = operation.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned) throw new IOException("A save/load operation is already in progress.");
                    using (Process selected = SelectGame(options))
                        if (selected.Id != process.Id) throw new IOException("The running game changed before the operation began.");
                    LaunchInfo launch = LaunchInfo.Capture(process, options);
                    Phase(log, progress, load ? "F8 started: verifying the full snapshot..." : "F5 started: waiting for Noita to save and quit...");
                    using (ProcessMonitor monitor = new ProcessMonitor(process.Id))
                    {
                    bool exited = false;
                    Exception failure = null;
                    string message = null;
                    try
                    {
                        if (load)
                        {
                            // Invalid snapshots never kill the game. Once this fast
                            // verification succeeds, stop immediately; do the copy
                            // while offline instead of keeping gameplay running.
                            SnapshotStore.Validate(store.Quick);
                            Phase(log, progress, "F8 stopping Noita immediately...");
                            monitor.Kill();
                            if (!monitor.Wait(15000)) throw new IOException("Noita did not terminate. No restore was committed.");
                            exited = true;
                            RequireNoRunningGames();
                            Phase(log, progress, "F8 restoring snapshot files...");
                            string staged = store.PrepareRestore();
                            string recovery = store.CommitRestore(staged);
                            message = "Snapshot restored.";
                            log("F8 restored quick snapshot; previous save=" + (recovery ?? "none"));
                        }
                        else
                        {
                            Dictionary<string, DateTime> before = SaveTimes(store.Active);
                            if (!process.CloseMainWindow()) throw new IOException("Noita did not accept save-and-quit. No snapshot was changed.");
                            if (!monitor.Wait(90000)) throw new IOException("Noita has not finished save-and-quit after 90 seconds. No snapshot was changed; finish closing the game and retry.");
                            exited = true;
                            if (monitor.ExitCode != 0) throw new IOException("Noita exited with code " + monitor.ExitCode + ". The previous snapshot was retained because saving may not have finished.");
                            RequireFreshSave(store.Active, before);
                            RequireNoRunningGames();
                            Dictionary<string, DateTime> afterQuit = SaveTimes(store.Active);
                            Phase(log, progress, "F5 copying and verifying the snapshot; keep Noita closed...");
                            store.Save(delegate
                            {
                                RequireNoRunningGames();
                                foreach (KeyValuePair<string, DateTime> file in afterQuit)
                                    if (File.GetLastWriteTimeUtc(Paths.Child(store.Active, file.Key)) != file.Value)
                                        throw new IOException("The active save changed while being copied. The previous snapshot was retained. Keep Noita closed until copying finishes.");
                            });
                            message = "Full snapshot saved.";
                            log("F5 committed full quick snapshot.");
                        }
                    }
                    catch (Exception error) { failure = error; }
                    finally
                    {
                        if (exited)
                        {
                            try { Phase(log, progress, "Restarting Noita..."); launch.Restart(options, log); }
                            catch (Exception error) { failure = new IOException("The operation ended but Noita could not restart. Launch Noita normally. " + error.Message, failure ?? error); }
                        }
                    }
                    if (failure != null) throw failure;
                    return message;
                    }
                }
                finally { if (owned) operation.ReleaseMutex(); }
            }
        }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private readonly Options options;
        private readonly SnapshotStore store;
        private readonly NotifyIcon icon;
        private readonly System.Windows.Forms.Timer timer;
        private readonly ToolStripMenuItem exitItem;
        private readonly ToolStripMenuItem statusItem;
        private readonly Control dispatcher;
        private bool previousSave, previousLoad, busy;
        private DateTime nextLifecycleCheck = DateTime.MinValue;
        private DateTime noGameSince = DateTime.MinValue;
        private readonly object logLock = new object();
        public TrayContext(Options options)
        {
            this.options = options; store = new SnapshotStore(options.SaveRoot, options.DataRoot);
            dispatcher = new Control(); IntPtr handle = dispatcher.Handle;
            ContextMenuStrip menu = new ContextMenuStrip();
            statusItem = new ToolStripMenuItem("Ready: F5 save / F8 load") { Enabled = false }; menu.Items.Add(statusItem);
            menu.Items.Add("Open backups", null, delegate { Process.Start(new ProcessStartInfo { FileName = store.DataRoot, UseShellExecute = true }); });
            menu.Items.Add("Open recovery saves", null, delegate { Paths.MakeDirectory(store.RecoveryRoot); Process.Start(new ProcessStartInfo { FileName = store.RecoveryRoot, UseShellExecute = true }); });
            menu.Items.Add(new ToolStripSeparator());
            exitItem = new ToolStripMenuItem("Exit helper", null, delegate { if (!busy) ExitThread(); }); menu.Items.Add(exitItem);
            icon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Noita F5 save / F8 load", ContextMenuStrip = menu };
            timer = new System.Windows.Forms.Timer { Interval = 25 }; timer.Tick += Tick; timer.Start();
            Log("Helper started. Game=" + options.Game + "; SaveRoot=" + options.SaveRoot);
        }
        private void Log(string message)
        {
            lock (logLock)
            {
                string file = Paths.Child(store.DataRoot, "helper.log"); Paths.NoReparse(file);
                File.AppendAllText(file, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        private void Tick(object sender, EventArgs e)
        {
            if (options.LaunchGame && !busy && DateTime.UtcNow >= nextLifecycleCheck)
            {
                nextLifecycleCheck = DateTime.UtcNow.AddMilliseconds(500);
                if (GameOperations.RunningGameCount() == 0 && !GameOperations.IsOperationBusy())
                {
                    if (noGameSince == DateTime.MinValue) noGameSince = DateTime.UtcNow;
                    else if ((DateTime.UtcNow - noGameSince).TotalSeconds >= 2)
                    {
                        Log("Steam wrapper exiting after normal game exit."); ExitThread(); return;
                    }
                }
                else noGameSince = DateTime.MinValue;
            }
            bool save = (GetAsyncKeyState(0x74) & 0x8000) != 0;
            bool load = (GetAsyncKeyState(0x77) & 0x8000) != 0;
            bool triggerSave = save && !previousSave, triggerLoad = load && !previousLoad;
            previousSave = save; previousLoad = load;
            if (busy || (!triggerSave && !triggerLoad)) return;
            uint id; GetWindowThreadProcessId(GetForegroundWindow(), out id);
            Process process = null;
            try
            {
                process = Process.GetProcessById((int)id);
                if (!process.ProcessName.Equals(Path.GetFileNameWithoutExtension(options.Game), StringComparison.OrdinalIgnoreCase) || !process.MainModule.FileName.Equals(options.Game, StringComparison.OrdinalIgnoreCase)) { process.Dispose(); return; }
                // Only one game may own save00. The uploader and other slots are not safe concurrent writers.
                busy = true; exitItem.Enabled = false; statusItem.Text = triggerLoad ? "Restoring snapshot..." : "Saving run...";
                icon.Text = triggerLoad ? "Noita F8: restoring; please wait" : "Noita F5: saving; wait for restart";
                Process captured = process; process = null;
                ThreadPool.QueueUserWorkItem(delegate { Operate(captured, triggerLoad); });
            }
            catch (Exception error) { NotifyError(error); }
            finally { if (process != null) process.Dispose(); }
        }
        private void NotifyError(Exception error)
        {
            try { Log("ERROR " + error); } catch { }
            statusItem.Text = "Error: " + error.Message;
            icon.Text = "Noita F5 / F8: error (see helper.log)";
        }
        private void Operate(Process process, bool load)
        {
            Exception failure = null; string message = null;
            try { message = GameOperations.Execute(options, store, process, load, Log, ShowProgress); }
            catch (Exception error) { failure = error; }
            finally { process.Dispose(); }
            dispatcher.BeginInvoke((Action)delegate
            {
                busy = false; exitItem.Enabled = true; statusItem.Text = "Ready: F5 save / F8 load";
                icon.Text = "Noita F5 save / F8 load";
                if (failure != null) NotifyError(failure);
            });
        }
        private void ShowProgress(string message)
        {
            dispatcher.BeginInvoke((Action)delegate
            {
                statusItem.Text = message;
                icon.Text = message.Length > 63 ? message.Substring(0, 63) : message;
            });
        }
        protected override void ExitThreadCore()
        {
            timer.Stop(); timer.Dispose(); icon.Visible = false; icon.Dispose(); dispatcher.Dispose(); base.ExitThreadCore();
        }
    }

    internal static class SelfTests
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(IntPtr file, uint code, byte[] input, int inputLength, IntPtr output, int outputLength, out int returned, IntPtr overlapped);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private static int passed;
        internal static int MemoryChild(string metadata, string readyMarker = null)
        {
            IntPtr memory = Marshal.AllocHGlobal(2);
            try
            {
                Marshal.WriteByte(memory, 0, 0); Marshal.WriteByte(memory, 1, 0);
                File.WriteAllText(metadata + ".writing", Process.GetCurrentProcess().Id + "\n" + memory.ToInt64().ToString(CultureInfo.InvariantCulture));
                File.Move(metadata + ".writing", metadata);
                Thread.Sleep(150); Marshal.WriteByte(memory, 0, 1);
                Stopwatch elapsed = Stopwatch.StartNew(); int requests = 0;
                while (elapsed.ElapsedMilliseconds < 10000)
                {
                    if (Marshal.ReadByte(memory, 1) == 1)
                    {
                        Marshal.WriteByte(memory, 1, 0); requests++;
                        if (requests == 3)
                        {
                            if (readyMarker == null) Marshal.WriteByte(memory, 0, 0);
                            else File.WriteAllText(readyMarker, "cooperative gameplay ready");
                            Thread.Sleep(500); return 0;
                        }
                    }
                    Thread.Sleep(5);
                }
                return 24;
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        private static void CreateJunction(string link, string target)
        {
            // Standard-user NTFS junction confined to this unique temp fixture.
            Paths.MakeDirectory(link);
            byte[] substitute = Encoding.Unicode.GetBytes("\\??\\" + Path.GetFullPath(target));
            byte[] display = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
            byte[] buffer;
            using (MemoryStream memory = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memory))
            {
                writer.Write((uint)0xA0000003); // IO_REPARSE_TAG_MOUNT_POINT
                writer.Write((ushort)(8 + substitute.Length + 2 + display.Length + 2)); writer.Write((ushort)0);
                writer.Write((ushort)0); writer.Write((ushort)substitute.Length);
                writer.Write((ushort)(substitute.Length + 2)); writer.Write((ushort)display.Length);
                writer.Write(substitute); writer.Write((ushort)0); writer.Write(display); writer.Write((ushort)0);
                buffer = memory.ToArray();
            }
            IntPtr handle = CreateFile(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
            if (handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create junction regression fixture.");
            try
            {
                int returned;
                if (!DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out returned, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot set junction regression fixture.");
            }
            finally { CloseHandle(handle); }
        }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            passed++; Console.WriteLine("PASS: " + description);
        }
        public static int Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "NoitaF5F8-SelfTest-" + Guid.NewGuid().ToString("N"));
            string saveRoot = Path.Combine(root, "saves"), dataRoot = Path.Combine(root, "backups");
            Paths.MakeDirectory(Path.Combine(saveRoot, "save00", "stats"));
            File.WriteAllText(Path.Combine(saveRoot, "save00", "player.xml"), "<Entity \n  hp='100' >\n</Entity>\n");
            string nativeWorld = "<Entity \n  seed='123' >\n  <VariableStorageComponent\n    value_string=\"|\"left_ctrl\"|\"left_alt\"|\" >\n  </VariableStorageComponent>\n</Entity>\n";
            File.WriteAllText(Path.Combine(saveRoot, "save00", "world_state.xml"), nativeWorld, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(saveRoot, "save00", "stats", "progress.txt"), "original progress");
            Paths.MakeDirectory(Path.Combine(saveRoot, "save00", "empty-directory"));
            SnapshotStore store = new SnapshotStore(saveRoot, dataRoot);
            store.Save(); SnapshotStore.Validate(store.Quick);
            Check(File.ReadAllBytes(Path.Combine(store.Quick, "save00", "world_state.xml")).SequenceEqual(new UTF8Encoding(false).GetBytes(nativeWorld)), "native raw-quote mod strings are accepted and preserved byte-for-byte");
            Check(File.ReadAllText(Path.Combine(store.Quick, "save00", "stats", "progress.txt")) == "original progress", "full save includes nested progress");
            Check(Directory.Exists(Path.Combine(store.Quick, "save00", "empty-directory")), "full save includes empty directories");
            File.WriteAllText(Path.Combine(store.Active, "player.xml"), "<Entity hp='1'>\n</Entity>\n");
            File.WriteAllText(Path.Combine(store.Active, "stats", "progress.txt"), "changed progress");
            string staged = store.PrepareRestore();
            Check(File.ReadAllText(Path.Combine(store.Active, "player.xml")).Contains("hp='1'"), "preparing restore does not alter active save");
            string recovery = store.CommitRestore(staged);
            Check(File.ReadAllText(Path.Combine(store.Active, "player.xml")).Contains("hp='100'"), "restore returns player to snapshot");
            Check(File.ReadAllText(Path.Combine(store.Active, "stats", "progress.txt")) == "original progress", "restore returns nested progress");
            Check(File.ReadAllText(Path.Combine(recovery, "player.xml")).Contains("hp='1'"), "restore retains previous active save for recovery");
            store.Save();
            Check(Directory.GetDirectories(Path.Combine(dataRoot, "history")).Length == 1, "saving retains previous quick snapshot");
            string quickPlayer = Path.Combine(store.Quick, "save00", "player.xml");
            string valid = File.ReadAllText(quickPlayer); File.WriteAllText(quickPlayer, "tampered");
            bool rejected = false; try { store.PrepareRestore(); } catch (IOException) { rejected = true; }
            Check(rejected, "tampered snapshot is rejected before restore");
            Check(File.ReadAllText(Path.Combine(store.Active, "player.xml")) == valid, "failed restore leaves active save unchanged");
            File.WriteAllText(quickPlayer, valid);
            string world = Path.Combine(store.Active, "world_state.xml"); string worldText = File.ReadAllText(world);
            File.WriteAllText(world, ""); rejected = false; try { store.Save(); } catch (IOException) { rejected = true; }
            Check(rejected, "incomplete active run cannot replace quick snapshot");
            SnapshotStore.Validate(store.Quick); File.WriteAllText(world, worldText);
            File.WriteAllText(world, "<Entity broken"); rejected = false; try { store.Save(); } catch (IOException) { rejected = true; }
            Check(rejected, "truncated nonempty Entity document cannot replace quick snapshot");
            SnapshotStore.Validate(store.Quick); File.WriteAllText(world, worldText);
            File.WriteAllText(world, "<Entity>\n  <Entity>\n  </Entity>\n"); rejected = false; try { store.Save(); } catch (IOException) { rejected = true; }
            Check(rejected, "missing root close is rejected even when a child close remains");
            SnapshotStore.Validate(store.Quick); File.WriteAllText(world, worldText);
            File.WriteAllBytes(world, new byte[] { 0, 255, 16, 32 }); rejected = false; try { store.Save(); } catch (IOException) { rejected = true; }
            Check(rejected, "random binary data cannot replace quick snapshot");
            SnapshotStore.Validate(store.Quick); File.WriteAllText(world, worldText);
            rejected = false; try { Paths.Child(dataRoot, "..\\outside"); } catch (IOException) { rejected = true; }
            Check(rejected, "path traversal is rejected");
            staged = store.PrepareRestore();
            File.WriteAllText(Path.Combine(staged, "save00", "player.xml"), "tampered after staging");
            rejected = false; try { store.CommitRestore(staged); } catch (IOException) { rejected = true; }
            Check(rejected, "changed restore staging is rejected before active save move");
            Check(File.ReadAllText(Path.Combine(store.Active, "player.xml")) == valid, "staging failure leaves active save unchanged");
            bool guardReached = false;
            File.WriteAllText(Path.Combine(store.Active, "player.xml"), "<Entity hp='55'>\n</Entity>\n");
            rejected = false;
            try { store.Save(delegate { guardReached = true; throw new IOException("A game reopened during copying."); }); }
            catch (IOException) { rejected = true; }
            Check(rejected && guardReached, "pre-commit guard can reject a completed staging copy");
            Check(File.ReadAllText(Path.Combine(store.Quick, "save00", "player.xml")) == valid, "pre-commit guard failure retains the previous snapshot");
            Check(Directory.GetDirectories(Path.Combine(dataRoot, "history")).Length == 1, "pre-commit guard failure does not rotate quick history");
            File.WriteAllText(Path.Combine(store.Active, "player.xml"), valid);
            Dictionary<string, DateTime> before = GameOperations.SaveTimes(store.Active);
            rejected = false; try { GameOperations.RequireFreshSave(store.Active, before); } catch (IOException) { rejected = true; }
            Check(rejected, "unchanged menu save cannot be committed as a fresh run");
            foreach (KeyValuePair<string, DateTime> file in before)
                File.SetLastWriteTimeUtc(Path.Combine(store.Active, file.Key), file.Value.AddSeconds(2));
            GameOperations.RequireFreshSave(store.Active, before);
            Check(true, "freshly written player and world are accepted");
            using (Process child = Process.Start(new ProcessStartInfo { FileName = Process.GetCurrentProcess().MainModule.FileName, Arguments = "--self-test-exit-child", UseShellExecute = false, CreateNoWindow = true }))
            using (Process attached = Process.GetProcessById(child.Id))
            using (GameOperations.ProcessMonitor monitor = new GameOperations.ProcessMonitor(attached.Id))
            {
                Check(monitor.Wait(10000) && monitor.ExitCode == 23, "native retained handle reports attached process exit code");
            }
            string metadata = Path.Combine(root, "native-menu-child.txt");
            using (Process child = Process.Start(new ProcessStartInfo { FileName = Process.GetCurrentProcess().MainModule.FileName, Arguments = "--self-test-memory-child " + LaunchInfo.Quote(metadata), UseShellExecute = false, CreateNoWindow = true }))
            {
                try
                {
                    Stopwatch metadataWait = Stopwatch.StartNew();
                    while ((!File.Exists(metadata) || new FileInfo(metadata).Length == 0) && metadataWait.ElapsedMilliseconds < 5000) Thread.Sleep(10);
                    string[] info = File.ReadAllLines(metadata);
                    Check(Int32.Parse(info[0], CultureInfo.InvariantCulture) == child.Id, "native menu test targets only the newly started child PID");
                    IntPtr menu = new IntPtr(Int64.Parse(info[1], CultureInfo.InvariantCulture));
                    using (NativeAutoContinue.NativeMemory memory = new NativeAutoContinue.NativeMemory(child.Id))
                    {
                        bool validationCalled = false; rejected = false;
                        try { NativeAutoContinue.RequestContinue(memory, menu, new IntPtr(menu.ToInt64() + 1), 3000, delegate { }, delegate { validationCalled = true; throw new IOException("Invalid run fixture."); }); }
                        catch (IOException) { rejected = true; }
                        Check(rejected && validationCalled && memory.Read(new IntPtr(menu.ToInt64() + 1)) == 0, "invalid run is rejected before any native Continue write");
                        bool resumed = NativeAutoContinue.RequestContinue(memory, menu, new IntPtr(menu.ToInt64() + 1), 3000, delegate { }, delegate { SnapshotStore.RequireRun(store.Active); });
                        Check(resumed, "native Continue repeats a bounded request until menu closes without keyboard input");
                    }
                    Check(child.WaitForExit(3000) && child.ExitCode == 0, "native child observed repeated Continue flags and completed");
                }
                finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(3000); } }
            }
            Check(!NativeAutoContinue.TryResume(Process.GetCurrentProcess(), Process.GetCurrentProcess().MainModule.FileName, saveRoot, delegate { }), "unknown executable hash skips native Continue safely");
            string fakeGame = Path.Combine(root, "wrapper-game", "noita.exe"); Paths.MakeDirectory(Path.GetDirectoryName(fakeGame));
            File.Copy(Process.GetCurrentProcess().MainModule.FileName, fakeGame);
            Options wrapper = Options.Parse(new string[] { "--manual-continue", "--launch-game", fakeGame, "-foo", "space arg", "--data-root", "game-owned" });
            Check(wrapper.LaunchGame && wrapper.ManualContinue && wrapper.Game == fakeGame && wrapper.InitialArguments == "\"-foo\" \"space arg\" \"--data-root\" \"game-owned\"" && wrapper.DataRoot != "game-owned", "Steam wrapper preserves terminal game arguments and helper opt-out");
            string readyMarker = GameReadiness.MarkerPath(saveRoot); Paths.MakeDirectory(Path.GetDirectoryName(readyMarker));
            string otherFlag = Path.Combine(Path.GetDirectoryName(readyMarker), "unrelated_progress_flag");
            File.WriteAllText(otherFlag, "keep this progress"); File.WriteAllText(readyMarker, "stale engine-written marker");
            Check(GameReadiness.IsReady(saveRoot), "nonempty engine gameplay marker is recognized");
            GameReadiness.Clear(saveRoot);
            Check(!GameReadiness.IsReady(saveRoot) && File.ReadAllText(otherFlag) == "keep this progress", "prelaunch reset removes only the stale private gameplay marker");
            metadata = Path.Combine(root, "native-marker-child.txt");
            using (Process child = Process.Start(new ProcessStartInfo { FileName = Process.GetCurrentProcess().MainModule.FileName, Arguments = "--self-test-marker-child " + LaunchInfo.Quote(metadata) + " " + LaunchInfo.Quote(readyMarker), UseShellExecute = false, CreateNoWindow = true }))
            {
                try
                {
                    Stopwatch metadataWait = Stopwatch.StartNew();
                    while (!File.Exists(metadata) && metadataWait.ElapsedMilliseconds < 5000) Thread.Sleep(10);
                    string[] info = File.ReadAllLines(metadata); IntPtr menu = new IntPtr(Int64.Parse(info[1], CultureInfo.InvariantCulture));
                    using (NativeAutoContinue.NativeMemory memory = new NativeAutoContinue.NativeMemory(child.Id))
                    {
                        bool markerConfirmed = false;
                        bool resumed = NativeAutoContinue.RequestContinue(memory, menu, new IntPtr(menu.ToInt64() + 1), 3000, delegate(string message) { if (message.Contains("fresh gameplay marker")) markerConfirmed = true; }, delegate { SnapshotStore.RequireRun(store.Active); }, null, delegate { return GameReadiness.IsReady(saveRoot); });
                        Check(resumed && markerConfirmed && memory.Read(menu) == 1, "fresh cooperative marker confirms gameplay even when native menu stays one");
                    }
                    Check(child.WaitForExit(3000) && child.ExitCode == 0, "cooperative child completed without clearing native menu state");
                }
                finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(3000); } }
            }
            string junctionTarget = Path.Combine(root, "junction-target"); Paths.MakeDirectory(junctionTarget);
            File.WriteAllText(Path.Combine(junctionTarget, "sentinel.txt"), "must not traverse");
            string junctionSource = Path.Combine(root, "junction-source"); Paths.MakeDirectory(junctionSource);
            string junction = Path.Combine(junctionSource, "redirect"); CreateJunction(junction, junctionTarget);
            rejected = false; try { Paths.Files(junctionSource).ToArray(); } catch (IOException error) { rejected = error.Message.Contains("junctions"); }
            Check(rejected, "tree enumeration rejects an immediate directory junction");
            rejected = false; try { Paths.Files(Path.Combine(junction, "child")).ToArray(); } catch (IOException error) { rejected = error.Message.Contains("junctions"); }
            Check(rejected, "root validation rejects a junction in an ancestor");
            string copyTarget = Path.Combine(root, "junction-copy");
            rejected = false; try { Paths.CopyTree(junctionSource, copyTarget); } catch (IOException error) { rejected = error.Message.Contains("junctions"); }
            Check(rejected && !File.Exists(Path.Combine(copyTarget, "redirect", "sentinel.txt")), "copy rejects a junction without following it");
            CreateJunction(Path.Combine(store.Quick, "save00", "redirect"), junctionTarget);
            rejected = false; try { SnapshotStore.Validate(store.Quick); } catch (IOException error) { rejected = error.Message.Contains("junctions"); }
            Check(rejected, "snapshot validation rejects a junction inside its payload");
            string redirectedMarker = Path.Combine(junctionTarget, GameReadiness.MarkerName); File.WriteAllText(redirectedMarker, "must remain");
            string linkedReadinessRoot = Path.Combine(root, "linked-readiness");
            Paths.MakeDirectory(Path.Combine(linkedReadinessRoot, "save00", "persistent"));
            CreateJunction(Path.Combine(linkedReadinessRoot, "save00", "persistent", "flags"), junctionTarget);
            rejected = false; try { GameReadiness.Clear(linkedReadinessRoot); } catch (IOException error) { rejected = error.Message.Contains("junctions"); }
            Check(rejected && File.ReadAllText(redirectedMarker) == "must remain", "gameplay marker clear rejects junction traversal without deleting target data");
            rejected = false; try { GameReadiness.IsReady(linkedReadinessRoot); } catch (IOException error) { rejected = error.Message.Contains("junctions"); }
            Check(rejected, "gameplay readiness detection rejects a redirected marker path");
            Console.WriteLine("All " + passed + " checks passed. Fixtures retained at " + root);
            return 0;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--self-test-exit-child") { Thread.Sleep(500); return 23; }
            if (args.Length == 2 && args[0] == "--self-test-memory-child") return SelfTests.MemoryChild(args[1]);
            if (args.Length == 3 && args[0] == "--self-test-marker-child") return SelfTests.MemoryChild(args[1], args[2]);
            Options options = null;
            try
            {
                options = Options.Parse(args);
                if (options.SelfTest) return SelfTests.Run();
                if (options.Operation != null)
                {
                    SnapshotStore store = new SnapshotStore(options.SaveRoot, options.DataRoot);
                    Action<string> log = delegate(string message)
                    {
                        string line = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message;
                        Console.WriteLine(line);
                        string file = Paths.Child(options.DataRoot, "helper.log"); Paths.NoReparse(file);
                        File.AppendAllText(file, line + Environment.NewLine, new UTF8Encoding(false));
                    };
                    try
                    {
                        using (Process process = GameOperations.SelectGame(options))
                            log(GameOperations.Execute(options, store, process, options.Operation == "--load-now", log));
                        return 0;
                    }
                    catch (Exception error) { log("ERROR " + error); return 1; }
                }
                bool created;
                using (Mutex mutex = new Mutex(true, "Local\\NoitaF5F8-Helper", out created))
                {
                    if (options.LaunchGame)
                    {
                        Action<string> wrapperLog = delegate(string message)
                        {
                            Paths.MakeDirectory(options.DataRoot);
                            string file = Paths.Child(options.DataRoot, "helper.log"); Paths.NoReparse(file);
                            File.AppendAllText(file, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine, new UTF8Encoding(false));
                        };
                        GameOperations.StartRequestedGame(options, wrapperLog);
                    }
                    if (!created) return 0;
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TrayContext(options));
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.ToString());
                if (!args.Contains("--self-test"))
                {
                    try
                    {
                        string root = options == null ? new Options().DataRoot : options.DataRoot;
                        Paths.MakeDirectory(root);
                        string file = Paths.Child(root, "helper.log"); Paths.NoReparse(file);
                        File.AppendAllText(file, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " STARTUP ERROR " + error + Environment.NewLine, new UTF8Encoding(false));
                    }
                    catch { }
                }
                return 1;
            }
        }
    }
}
