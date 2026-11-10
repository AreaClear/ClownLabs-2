
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClownLabsBooster;

public sealed class MainForm : Form
{
    private readonly Color background = Color.FromArgb(12, 12, 14);
    private readonly Color surface = Color.FromArgb(25, 25, 28);
    private readonly Color gold = Color.FromArgb(218, 175, 85);
    private readonly Color muted = Color.FromArgb(160, 160, 165);

    private readonly System.Windows.Forms.Timer monitorTimer = new();
    private readonly List<CheckBox> cleanupChecks = new();
    private readonly ListBox processList = new();
    private readonly ListBox emulatorList = new();

    private Label cpuLabel = new();
    private Label ramLabel = new();
    private Label availableRamLabel = new();
    private Label emulatorStatus = new();
    private Label fpsStatus = new();
    private ProgressBar cpuProgress = new();
    private ProgressBar ramProgress = new();

    private long previousIdle;
    private long previousKernel;
    private long previousUser;
    private bool hasCpuSample;

    private sealed record CleanupItem(string Name, string Path, int DaysOld);

    private readonly List<CleanupItem> cleanupItems = new();

    private static readonly string[] EmulatorProcesses =
    {
        "HD-Player", "HD-Agent", "Bluestacks",
        "dnplayer", "LdVBoxHeadless", "Ld9BoxHeadless",
        "MEmu", "MEmuHeadless",
        "Nox", "NoxVMHandle",
        "AndroidEmulator", "qemu-system-x86_64",
        "aow_exe", "AppMarket",
        "GameLoop", "TxGameAssistant"
    };

    public MainForm()
    {
        Text = "Clown Labs Booster";
        Size = new Size(1120, 740);
        MinimumSize = new Size(940, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        BuildUI();

        monitorTimer.Interval = 1000;
        monitorTimer.Tick += (_, _) => UpdateMonitoring();
        monitorTimer.Start();

        Shown += (_, _) => UpdateMonitoring();
    }

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = background
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 17, 19),
            Padding = new Padding(12)
        };
        root.Controls.Add(sidebar, 0, 0);

        var logo = new Label
        {
            Text = "CLOWN LABS\nBOOSTER",
            Dock = DockStyle.Top,
            Height = 90,
            ForeColor = gold,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        sidebar.Controls.Add(logo);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 16, 0, 0),
            BackColor = Color.Transparent
        };
        sidebar.Controls.Add(nav);

        AddNav(nav, "DASHBOARD", ShowDashboard);
        AddNav(nav, "PC CLEANUP", ShowCleanup);
        AddNav(nav, "RAM MANAGER", ShowRam);
        AddNav(nav, "SYSTEM BOOST", ShowBoost);
        AddNav(nav, "EMULATOR CENTER", ShowEmulators);
        AddNav(nav, "FPS MONITOR", ShowFps);
        AddNav(nav, "PROCESS MANAGER", ShowProcesses);
        AddNav(nav, "ABOUT", ShowAbout);

        var main = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = background,
            Padding = new Padding(22)
        };
        root.Controls.Add(main, 1, 0);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62
        };
        main.Controls.Add(header);

        var heading = new Label
        {
            Text = "ADVANCED CONTROL PANEL",
            Dock = DockStyle.Left,
            Width = 450,
            ForeColor = gold,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(heading);

        var version = new Label
        {
            Text = "V2.0  |  WINDOWS",
            Dock = DockStyle.Right,
            Width = 160,
            ForeColor = muted,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(version);

        var host = new Panel
        {
            Name = "pageHost",
            Dock = DockStyle.Fill,
            BackColor = background,
            AutoScroll = true
        };
        main.Controls.Add(host);
        host.BringToFront();
        header.BringToFront();

        ShowDashboard();
    }

    private Panel PageHost =>
        Controls.Find("pageHost", true).FirstOrDefault() as Panel
        ?? throw new InvalidOperationException("Page panel missing.");

    private void AddNav(FlowLayoutPanel nav, string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 170,
            Height = 43,
            Margin = new Padding(0, 5, 0, 5),
            Padding = new Padding(10, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(75, 62, 37);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 43, 28);
        button.Click += (_, _) => action();
        nav.Controls.Add(button);
    }

    private void ClearPage(string title)
    {
        PageHost.Controls.Clear();

        var heading = new Label
        {
            Text = title,
            Location = new Point(5, 8),
            Size = new Size(800, 42),
            ForeColor = gold,
            Font = new Font("Segoe UI", 19, FontStyle.Bold)
        };

        PageHost.Controls.Add(heading);
    }

    private Label AddText(string text, int x, int y, int width, int height)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };

        PageHost.Controls.Add(label);
        return label;
    }

    private Button AddButton(string text, int x, int y, int width,
        Action action)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 43),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 37, 24),
            ForeColor = gold,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = gold;
        button.Click += (_, _) => action();
        PageHost.Controls.Add(button);
        return button;
    }

    private Panel AddCard(string title, int x, int y, out Label value)
    {
        var card = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(225, 120),
            BackColor = surface
        };

        var caption = new Label
        {
            Text = title,
            Location = new Point(13, 13),
            Size = new Size(195, 25),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        value = new Label
        {
            Text = "--",
            Location = new Point(13, 48),
            Size = new Size(195, 48),
            ForeColor = gold,
            Font = new Font("Segoe UI", 22, FontStyle.Bold)
        };

        card.Controls.Add(caption);
        card.Controls.Add(value);
        PageHost.Controls.Add(card);
        return card;
    }

    private ProgressBar AddProgress(int x, int y)
    {
        var bar = new ProgressBar
        {
            Location = new Point(x, y),
            Size = new Size(650, 20),
            Maximum = 100,
            Style = ProgressBarStyle.Continuous
        };

        PageHost.Controls.Add(bar);
        return bar;
    }

    // DASHBOARD

    private void ShowDashboard()
    {
        ClearPage("SYSTEM DASHBOARD");

        AddCard("CPU USAGE", 5, 65, out cpuLabel);
        AddCard("RAM USAGE", 250, 65, out ramLabel);
        AddCard("AVAILABLE RAM", 495, 65, out availableRamLabel);

        AddText("CPU MONITOR", 10, 220, 250, 25);
        cpuProgress = AddProgress(10, 250);

        AddText("RAM MONITOR", 10, 300, 250, 25);
        ramProgress = AddProgress(10, 330);

        emulatorStatus = AddText("Detected emulator: Checking...", 10, 390, 650, 30);
        fpsStatus = AddText("FPS: Not available - FPS source required", 10, 430, 750, 30);

        AddText(
            "Monitoring is local. No hidden network connection or forced system changes.",
            10, 490, 780, 35);

        UpdateMonitoring();
    }

    private void UpdateMonitoring()
    {
        try
        {
            float cpu = GetCpuUsage();
            var memory = new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
            };

            if (!GlobalMemoryStatusEx(ref memory))
                return;

            double totalGb = memory.ullTotalPhys / 1073741824d;
            double freeGb = memory.ullAvailPhys / 1073741824d;
            double usedGb = totalGb - freeGb;
            int ramPercent = (int)Math.Clamp(memory.dwMemoryLoad, 0, 100);

            if (!IsDisposed && cpuLabel.IsHandleCreated)
            {
                cpuLabel.Text = $"{cpu:0}%";
                ramLabel.Text = $"{ramPercent}%";
                availableRamLabel.Text = $"{freeGb:0.0} GB";

                cpuProgress.Value = (int)Math.Clamp(cpu, 0, 100);
                ramProgress.Value = ramPercent;
            }

            if (emulatorStatus.IsHandleCreated)
                emulatorStatus.Text = "Detected emulator: " + GetDetectedEmulator();

            _ = usedGb;
        }
        catch
        {
            // Monitoring errors should not close the application.
        }
    }

    private float GetCpuUsage()
    {
        GetSystemTimes(out var idle, out var kernel, out var user);

        long idleNow = ToLong(idle);
        long kernelNow = ToLong(kernel);
        long userNow = ToLong(user);

        if (!hasCpuSample)
        {
            previousIdle = idleNow;
            previousKernel = kernelNow;
            previousUser = userNow;
            hasCpuSample = true;
            return 0;
        }

        long idleDiff = idleNow - previousIdle;
        long totalDiff =
            (kernelNow - previousKernel) + (userNow - previousUser);

        previousIdle = idleNow;
        previousKernel = kernelNow;
        previousUser = userNow;

        if (totalDiff <= 0) return 0;

        return (float)Math.Clamp(
            100d * (totalDiff - idleDiff) / totalDiff, 0, 100);
    }

    private static long ToLong(FILETIME time) =>
        ((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    // PC CLEANUP

    private void ShowCleanup()
    {
        ClearPage("PC CLEANUP");

        AddText(
            "Select the categories you want to scan and clean.\n" +
            "Files currently in use may be skipped. Personal folders are excluded.",
            10, 65, 800, 55);

        cleanupChecks.Clear();
        cleanupItems.Clear();

        AddCleanupCheck("User TEMP files (older than 7 days)", 10, 140,
            "TEMP", Path.GetTempPath(), 7);

        AddCleanupCheck("Windows thumbnail cache", 10, 185,
            "THUMBNAILS",
            Path.Combine(Environment.LocalApplicationData,
                "Microsoft", "Windows", "Explorer"), 7);

        AddCleanupCheck("DirectX shader cache", 10, 230,
            "DXCACHE",
            Path.Combine(Environment.LocalApplicationData,
                "D3DSCache"), 7);

        AddCleanupCheck("Windows error reports (user)", 10, 275,
            "WER",
            Path.Combine(Environment.LocalApplicationData,
                "Microsoft", "Windows", "WER"), 30);

        AddButton("SELECT ALL", 10, 350, 150,
            () => SetAllCleanup(true));

        AddButton("CLEAR SELECTION", 175, 350, 170,
            () => SetAllCleanup(false));

        AddButton("SCAN SELECTED", 360, 350, 180,
            ScanCleanup);

        AddButton("CLEAN SELECTED", 555, 350, 190,
            CleanSelected);

        AddText(
            "Recycle Bin and browser cache are not included in this version.\n" +
            "This avoids deleting files that may still be needed by applications.",
            10, 425, 800, 65);
    }

    private void AddCleanupCheck(string title, int x, int y,
        string id, string path, int days)
    {
        var check = new CheckBox
        {
            Text = title,
            Location = new Point(x, y),
            Size = new Size(700, 30),
            ForeColor = Color.White,
            BackColor = background,
            Font = new Font("Segoe UI", 10)
        };

        cleanupChecks.Add(check);
        cleanupItems.Add(new CleanupItem(id, path, days));
        PageHost.Controls.Add(check);
    }

    private void SetAllCleanup(bool value)
    {
        foreach (var check in cleanupChecks)
            check.Checked = value;
    }

    private List<string> GetSelectedCleanupFiles()
    {
        var result = new List<string>();

        for (int i = 0; i < cleanupChecks.Count; i++)
        {
            if (!cleanupChecks[i].Checked)
                continue;

            var item = cleanupItems[i];

            try
            {
                if (!Directory.Exists(item.Path))
                    continue;

                foreach (var file in Directory.EnumerateFiles(
                    item.Path, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) <
                            DateTime.Now.AddDays(-item.DaysOld))
                        {
                            result.Add(file);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return result;
    }

    private void ScanCleanup()
    {
        var files = GetSelectedCleanupFiles();
        long bytes = 0;

        foreach (string file in files)
        {
            try { bytes += new FileInfo(file).Length; }
            catch { }
        }

        MessageBox.Show(
            $"Files found: {files.Count}\n" +
            $"Approximate size: {bytes / 1048576d:0.00} MB",
            "Cleanup Scan",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CleanSelected()
    {
        var files = GetSelectedCleanupFiles();

        if (files.Count == 0)
        {
            MessageBox.Show("No eligible files found. Scan or select a category first.");
            return;
        }

        var answer = MessageBox.Show(
            $"Delete {files.Count} old temporary/cache files?\n\n" +
            "Only files in the selected categories will be considered.",
            "Confirm Cleanup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
            return;

        int deleted = 0;
        long bytes = 0;

        foreach (string file in files)
        {
            try
            {
                var info = new FileInfo(file);
                long length = info.Length;
                info.Delete();
                deleted++;
                bytes += length;
            }
            catch { }
        }

        MessageBox.Show(
            $"Deleted: {deleted} files\n" +
            $"Freed approximately: {bytes / 1048576d:0.00} MB",
            "Cleanup Finished");
    }

    // RAM MANAGER

    private void ShowRam()
    {
        ClearPage("RAM MANAGER");

        AddText("RAM MANAGEMENT", 10, 70, 400, 30);

        var memory = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };

        GlobalMemoryStatusEx(ref memory);

        double total = memory.ullTotalPhys / 1073741824d;
        double available = memory.ullAvailPhys / 1073741824d;

        AddText($"Total RAM: {total:0.00} GB", 10, 125, 400, 30);
        AddText($"Available RAM: {available:0.00} GB", 10, 165, 400, 30);
        AddText(
            "Windows automatically manages memory. This app does not force-clear RAM.",
            10, 220, 800, 45);

        AddButton("REFRESH RAM", 10, 290, 180, ShowRam);

        AddButton("OPEN TASK MANAGER", 205, 290, 220, () =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskmgr.exe")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        });

        AddText(
            "To free memory safely, close applications you are not using.\n" +
            "Avoid RAM cleaner tricks that empty Windows standby memory.",
            10, 370, 800, 70);
    }

    // SYSTEM BOOST

    private void ShowBoost()
    {
        ClearPage("SYSTEM BOOST");

        AddText(
            "SAFE GAMING PREPARATION\n\n" +
            "This page opens Windows settings and helps you prepare your PC.\n" +
            "It does not disable security or Windows services.",
            10, 70, 800, 90);

        AddButton("OPEN GAME MODE", 10, 190, 220,
            () => OpenWindowsSetting("ms-settings:gaming-gamemode"));

        AddButton("OPEN GRAPHICS SETTINGS", 250, 190, 250,
            () => OpenWindowsSetting("ms-settings:display-graphics"));

        AddButton("OPEN POWER SETTINGS", 10, 250, 220,
            () => OpenWindowsSetting("ms-settings:powersleep"));

        AddButton("OPEN TASK MANAGER", 250, 250, 250,
            () => StartProgram("taskmgr.exe"));

        AddText(
            "Before gaming:\n" +
            "1. Close apps you do not need.\n" +
            "2. Keep enough free disk space.\n" +
            "3. Use the Windows graphics preference for your emulator.\n" +
            "4. Keep GPU drivers up to date.",
            10, 340, 800, 150);
    }

    // EMULATOR CENTER

    private void ShowEmulators()
    {
        ClearPage("EMULATOR CENTER");

        AddText(
            "Detects common emulator processes while they are running.",
            10, 65, 750, 30);

        emulatorList = new ListBox
        {
            Location = new Point(10, 110),
            Size = new Size(650, 230),
            BackColor = surface,
            ForeColor = Color.White,
            Font = new Font("Consolas", 10)
        };

        PageHost.Controls.Add(emulatorList);
        RefreshEmulators();

        AddButton("SCAN EMULATORS", 10, 365, 190,
            RefreshEmulators);

        AddText(
            "Supported process names include BlueStacks, LDPlayer, MEmu,\n" +
            "Nox, GameLoop and Android Emulator. Detection depends on process names.",
            10, 430, 800, 70);
    }

    private string GetDetectedEmulator()
    {
        foreach (string processName in EmulatorProcesses)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                    return FriendlyEmulatorName(processName);
            }
            catch { }
        }

        return "None detected";
    }

    private static string FriendlyEmulatorName(string name)
    {
        string lower = name.ToLowerInvariant();

        if (lower.Contains("hd-") || lower.Contains("bluestacks"))
            return "BlueStacks";

        if (lower.Contains("dnplayer") || lower.Contains("ld"))
            return "LDPlayer";

        if (lower.Contains("memu"))
            return "MEmu";

        if (lower.Contains("nox"))
            return "Nox";

        if (lower.Contains("aow") || lower.Contains("txgame"))
            return "GameLoop";

        if (lower.Contains("android") || lower.Contains("qemu"))
            return "Android Emulator";

        return name;
    }

    private void RefreshEmulators()
    {
        emulatorList.Items.Clear();
        bool found = false;

        foreach (string name in EmulatorProcesses.Distinct())
        {
            try
            {
                var processes = Process.GetProcessesByName(name);

                foreach (var process in processes)
                {
                    found = true;
                    emulatorList.Items.Add(
                        $"{FriendlyEmulatorName(name)} | PID {process.Id}");
                    process.Dispose();
                }
            }
            catch { }
        }

        if (!found)
            emulatorList.Items.Add("No supported emulator currently detected.");
    }

    // FPS MONITOR

    private void ShowFps()
    {
        ClearPage("FPS MONITOR");

        AddText(
            "REAL FPS MONITORING\n\n" +
            "A Windows process list or CPU usage counter cannot tell the actual\n" +
            "frames per second rendered by an emulator.\n\n" +
            "This version does not invent an FPS number. To show real FPS,\n" +
            "a compatible PresentMon measurement source must be integrated.\n\n" +
            "CPU usage and RAM usage are available on the Dashboard.",
            10, 75, 800, 210);

        AddText("FPS STATUS: SOURCE NOT CONFIGURED",
            10, 315, 750, 35);

        AddButton("OPEN EMULATOR CENTER", 10, 380, 240,
            ShowEmulators);
    }

    // PROCESS MANAGER

    private void ShowProcesses()
    {
        ClearPage("PROCESS MANAGER");

        AddText(
            "Select an application and send a normal close request.\n" +
            "System processes are not force-killed.",
            10, 65, 800, 45);

        processList = new ListBox
        {
            Location = new Point(10, 120),
            Size = new Size(680, 300),
            BackColor = surface,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9)
        };

        PageHost.Controls.Add(processList);
        LoadProcesses();

        AddButton("REFRESH", 10, 445, 160,
            LoadProcesses);

        AddButton("REQUEST CLOSE", 185, 445, 200,
            RequestNormalClose);
    }

    private void LoadProcesses()
    {
        processList.Items.Clear();

        foreach (var process in Process.GetProcesses()
                     .OrderBy(p => p.ProcessName)
                     .Take(250))
        {
            try
            {
                processList.Items.Add(
                    $"{process.ProcessName} | PID {process.Id}");
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private void RequestNormalClose()
    {
        if (processList.SelectedItem == null)
        {
            MessageBox.Show("Select a process first.");
            return;
        }

        string text = processList.SelectedItem.ToString() ?? "";
        int marker = text.LastIndexOf("PID ", StringComparison.Ordinal);

        if (marker < 0 ||
            !int.TryParse(text[(marker + 4)..], out int pid))
            return;

        try
        {
            using var process = Process.GetProcessById(pid);

            if (MessageBox.Show(
                $"Send a normal close request to {process.ProcessName}?",
                "Confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            if (!process.CloseMainWindow())
                MessageBox.Show("This process did not accept a close request.");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not send close request: " + ex.Message);
        }
    }

    private void ShowAbout()
    {
        ClearPage("ABOUT");

        AddText(
            "CLOWN LABS BOOSTER\n" +
            "Advanced Edition 2.0\n\n" +
            "C# Windows Forms application\n" +
            "Black Luxury theme\n\n" +
            "Local CPU/RAM monitoring\n" +
            "Safe selected-file cleanup\n" +
            "Common emulator detection\n" +
            "Windows gaming settings shortcuts\n\n" +
            "No hidden network connection.\n" +
            "No forced process termination.\n" +
            "No Defender or Firewall changes.",
            10, 80, 750, 300);
    }

    private static void OpenWindowsSetting(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not open setting: " + ex.Message);
        }
    }

    private static void StartProgram(string name)
    {
        try
        {
            Process.Start(new ProcessStartInfo(name)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not start program: " + ex.Message);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(
        out FILETIME idleTime,
        out FILETIME kernelTime,
        out FILETIME userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(
        ref MEMORYSTATUSEX lpBuffer);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        monitorTimer.Stop();
        monitorTimer.Dispose();
        base.OnFormClosed(e);
    }
}
