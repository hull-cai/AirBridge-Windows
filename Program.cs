using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace AirBridge;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ReceiverWindow());
    }
}

public sealed class ReceiverSettings
{
    public string BackendPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "backend", "uxplay.exe");
    public string DeviceName { get; set; } = "AirBridge";
    public bool Fullscreen { get; set; }
    public bool StartOnLaunch { get; set; }
}

public sealed class ReceiverWindow : Form
{
    private readonly string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirBridge", "settings.json");
    private readonly TextBox backend = new() { Dock = DockStyle.Fill };
    private readonly TextBox name = new() { Dock = DockStyle.Fill };
    private readonly CheckBox fullscreen = new() { Text = "Start mirroring fullscreen", AutoSize = true };
    private readonly CheckBox autoStart = new() { Text = "Start receiver when AirBridge opens", AutoSize = true };
    private readonly Button start = new() { Text = "Start receiver", Width = 155, Height = 42, BackColor = Color.FromArgb(64, 132, 245), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button stop = new() { Text = "Stop receiver", Width = 140, Height = 42, Enabled = false, FlatStyle = FlatStyle.Flat };
    private readonly Button browse = new() { Text = "Browse…", Width = 100 };
    private readonly Button save = new() { Text = "Save settings", AutoSize = true };
    private readonly Button clear = new() { Text = "Clear log", AutoSize = true };
    private readonly Label status = new() { Text = "● Stopped", AutoSize = true, ForeColor = Color.FromArgb(242, 167, 93), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly RichTextBox logs = new() { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(17, 22, 32), ForeColor = Color.FromArgb(212, 222, 239), BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9) };
    private Process? process;
    private bool closing;

    public ReceiverWindow()
    {
        Text = "AirBridge — AirPlay for Windows";
        Width = 780; Height = 670; MinimumSize = new Size(650, 590);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(25, 31, 44); ForeColor = Color.FromArgb(237, 241, 249);
        Font = new Font("Segoe UI", 10);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        heading.Controls.Add(new Label { Text = "AirBridge", Font = new Font("Segoe UI Semibold", 26), AutoSize = true }, 0, 0);
        heading.Controls.Add(new Label { Text = "Stream your iPhone screen and audio to this PC", ForeColor = Color.FromArgb(161, 176, 197), AutoSize = true }, 0, 1);
        root.Controls.Add(heading, 0, 0);

        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 5, Padding = new Padding(0, 8, 0, 0) };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        settings.Controls.Add(new Label { Text = "Receiver name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        settings.Controls.Add(name, 1, 0); settings.SetColumnSpan(name, 2);
        settings.Controls.Add(new Label { Text = "UxPlay executable", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        settings.Controls.Add(backend, 1, 1); settings.Controls.Add(browse, 2, 1);
        settings.Controls.Add(fullscreen, 1, 2); settings.SetColumnSpan(fullscreen, 2);
        settings.Controls.Add(autoStart, 1, 3); settings.SetColumnSpan(autoStart, 2);
        settings.Controls.Add(save, 1, 4);
        root.Controls.Add(settings, 0, 1);

        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        controls.Controls.Add(start); controls.Controls.Add(stop); controls.Controls.Add(status);
        status.Margin = new Padding(20, 12, 0, 0);
        root.Controls.Add(controls, 0, 2);

        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var logHead = new FlowLayoutPanel { Dock = DockStyle.Fill };
        logHead.Controls.Add(new Label { Text = "Activity log", AutoSize = true, Font = new Font("Segoe UI Semibold", 11), Margin = new Padding(0, 5, 24, 0) });
        logHead.Controls.Add(clear);
        logPanel.Controls.Add(logHead, 0, 0); logPanel.Controls.Add(logs, 0, 1);
        root.Controls.Add(logPanel, 0, 3);

        browse.Click += (_, _) => { using var picker = new OpenFileDialog { Filter = "UxPlay (uxplay.exe)|uxplay.exe|Executable files (*.exe)|*.exe", CheckFileExists = true }; if (picker.ShowDialog(this) == DialogResult.OK) backend.Text = picker.FileName; };
        save.Click += (_, _) => SaveSettings();
        clear.Click += (_, _) => logs.Clear();
        start.Click += (_, _) => StartReceiver();
        stop.Click += (_, _) => StopReceiver();
        FormClosing += (_, _) => { closing = true; SaveSettings(); StopReceiver(); };
        var settingsLoaded = LoadSettings();
        backend.Text = settingsLoaded.BackendPath; name.Text = settingsLoaded.DeviceName;
        fullscreen.Checked = settingsLoaded.Fullscreen; autoStart.Checked = settingsLoaded.StartOnLaunch;
        Append("Ready. Start receiver, then select AirBridge in iPhone Control Centre → Screen Mirroring.");
        if (settingsLoaded.StartOnLaunch) Shown += (_, _) => BeginInvoke(StartReceiver);
    }

    private ReceiverSettings LoadSettings()
    {
        try { return File.Exists(configFile) ? JsonSerializer.Deserialize<ReceiverSettings>(File.ReadAllText(configFile)) ?? new() : new(); }
        catch { return new(); }
    }
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configFile)!);
            var settings = new ReceiverSettings { BackendPath = backend.Text.Trim(), DeviceName = name.Text.Trim(), Fullscreen = fullscreen.Checked, StartOnLaunch = autoStart.Checked };
            File.WriteAllText(configFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            Append("Settings saved.");
        }
        catch (Exception ex) { Append("Cannot save settings: " + ex.Message); }
    }
    private void Append(string message)
    {
        if (closing || IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(() => Append(message)); } catch (InvalidOperationException) { } return; }
        logs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        logs.SelectionStart = logs.TextLength; logs.ScrollToCaret();
    }
    private void SetRunning(bool running)
    {
        if (closing || IsDisposed) return;
        start.Enabled = !running; stop.Enabled = running;
        status.Text = running ? "● Receiver running" : "● Stopped";
        status.ForeColor = running ? Color.FromArgb(90, 218, 154) : Color.FromArgb(242, 167, 93);
    }
    private void StartReceiver()
    {
        if (process is { HasExited: false }) return;
        var path = backend.Text.Trim();
        if (!File.Exists(path)) { MessageBox.Show(this, "UxPlay executable not found. Click Browse and select your working uxplay.exe.", "Missing backend", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var receiverName = name.Text.Trim();
        if (receiverName.Length == 0 || receiverName.Contains('"') || receiverName.Contains('\n')) { MessageBox.Show(this, "Please enter a valid receiver name."); return; }
        SaveSettings();
        var info = new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        // UxPlay built under MSYS2 UCRT64 needs runtime DLLs from ucrt64\bin.
        // Set PATH only for the child process; do not change the user's system PATH.
        var portableBin = Path.Combine(AppContext.BaseDirectory, "backend");
        var ucrtBin = File.Exists(Path.Combine(portableBin, "libgstreamer-1.0-0.dll")) ? portableBin : @"C:\msys64\ucrt64\bin";
        if (Directory.Exists(Path.Combine(portableBin, "gstreamer-1.0")))
        {
            info.Environment["GST_PLUGIN_PATH_1_0"] = Path.Combine(portableBin, "gstreamer-1.0");
            info.Environment["GST_PLUGIN_SYSTEM_PATH_1_0"] = "";
            var scanner = Path.Combine(portableBin, "gst-plugin-scanner.exe");
            if (File.Exists(scanner)) info.Environment["GST_PLUGIN_SCANNER"] = scanner;
        }
        if (Directory.Exists(ucrtBin))
        {
            var existingPath = info.Environment.TryGetValue("PATH", out var value) ? value ?? "" : "";
            info.Environment["PATH"] = ucrtBin + Path.PathSeparator + existingPath;
        }
        else
        {
            Append("Warning: MSYS2 UCRT64 runtime directory not found: " + ucrtBin);
        }
        info.ArgumentList.Add("-n"); info.ArgumentList.Add(receiverName);
        if (fullscreen.Checked) info.ArgumentList.Add("-fs");
        try
        {
            var p = new Process { StartInfo = info, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Append(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Append(e.Data); };
            p.Exited += (_, _) =>
            {
                Append($"UxPlay exited (code {p.ExitCode}).");
                if (!closing && IsHandleCreated) { try { BeginInvoke(() => { if (ReferenceEquals(process, p)) { process = null; SetRunning(false); } }); } catch (InvalidOperationException) { } }
            };
            p.Start(); process = p; p.BeginOutputReadLine(); p.BeginErrorReadLine();
            SetRunning(true); Append("Started UxPlay. Connect using iPhone Screen Mirroring.");
        }
        catch (Exception ex) { SetRunning(false); Append("Failed to start: " + ex.Message); MessageBox.Show(this, ex.Message, "UxPlay error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void StopReceiver()
    {
        var p = process; process = null;
        if (p != null)
        {
            try { if (!p.HasExited) { p.Kill(entireProcessTree: true); p.WaitForExit(2000); } } catch (Exception ex) { Append("Stop warning: " + ex.Message); }
            p.Dispose();
        }
        if (!closing) { SetRunning(false); Append("Receiver stopped."); }
    }
}
