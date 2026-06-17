using System.Drawing;
using System.IO;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using WpfApplication = System.Windows.Application;
using DrawingFontFamily = System.Drawing.FontFamily;
using DrawingFontStyle = System.Drawing.FontStyle;

namespace CrayonShinchanNotification;

public partial class App : WpfApplication
{
    private MainWindow? _mainWindow;
    private Thread? _apiThread;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private AutoUpdater? _autoUpdater;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize auto updater
        _autoUpdater = new AutoUpdater();
        _autoUpdater.OnUpdateAvailable += version =>
        {
            _notifyIcon?.ShowBalloonTip(3000, "发现新版本", $"v{version} 可用，正在自动更新...", System.Windows.Forms.ToolTipIcon.Info);
        };
        _autoUpdater.OnUpdateProgress += message =>
        {
            Console.WriteLine($"[更新] {message}");
        };
        _autoUpdater.OnUpdateError += error =>
        {
            _notifyIcon?.ShowBalloonTip(3000, "更新失败", error, System.Windows.Forms.ToolTipIcon.Error);
        };

        // Setup system tray icon
        SetupSystemTray();

        // Check for updates on startup (in background)
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000); // Wait 3 seconds after startup
            await _autoUpdater.CheckForUpdatesAsync();
        });

        // Start API server in background
        _apiThread = new Thread(StartApiServer)
        {
            IsBackground = true,
            Name = "APIServer"
        };
        _apiThread.Start();

        // Create and show main window
        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }

    private void SetupSystemTray()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon();

        // Create a simple icon (red circle with white "S")
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            // Red circle background
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(204, 0, 0));
            g.FillEllipse(brush, 2, 2, 28, 28);

            // White "S" letter
            using var font = new Font(new DrawingFontFamily("Arial"), 16, DrawingFontStyle.Bold);
            using var textBrush = new SolidBrush(System.Drawing.Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("S", font, textBrush, new RectangleF(0, 0, 32, 32), sf);
        }

        _notifyIcon.Icon = Icon.FromHandle(bitmap.GetHicon());
        _notifyIcon.Text = "蜡笔小新消息通知";

        // Context menu
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开 Web 界面", null, (s, ev) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://127.0.0.1:8000/",
                UseShellExecute = true
            });
        });
        menu.Items.Add("测试动画", null, (s, ev) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _mainWindow?.TriggerAnimation(new MessageData
                {
                    Type = "text",
                    Content = "测试消息！",
                    Timestamp = DateTime.Now
                });
            });
        });
        menu.Items.Add("检查更新", null, async (s, ev) =>
        {
            if (_autoUpdater != null)
            {
                _notifyIcon?.ShowBalloonTip(2000, "检查更新", "正在检查...", System.Windows.Forms.ToolTipIcon.Info);
                await _autoUpdater.CheckForUpdatesAsync();
            }
        });
        menu.Items.Add($"版本: {_autoUpdater?.GetCurrentVersionString() ?? "1.0.0"}", null, (s, ev) => { });
        menu.Items.Add("-"); // Separator
        menu.Items.Add("退出", null, (s, ev) =>
        {
            _notifyIcon!.Visible = false;
            Shutdown();
        });

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.Visible = true;

        // Double-click to open web UI
        _notifyIcon.DoubleClick += (s, ev) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://127.0.0.1:8000/",
                UseShellExecute = true
            });
        };
    }

    private void StartApiServer()
    {
        // Configure URL - listen on all interfaces for remote access
        var apiKey = Environment.GetEnvironmentVariable("SHINCHAN_API_KEY") ?? "shinchan2024";
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://0.0.0.0:8000");

        var builder = WebApplication.CreateBuilder();

        // Add CORS services
        builder.Services.AddCors();

        var app = builder.Build();

        // CORS for local development
        app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        // API Key authentication middleware
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            // Only require API key for message sending endpoints
            if (path.StartsWith("/api/send"))
            {
                if (!context.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
                    return;
                }
            }
            await next();
        });

        // Serve static files (frontend)
        var staticPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(staticPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(staticPath)
            });
        }

        // GET / serves index.html
        app.MapGet("/", () =>
        {
            var indexPath = Path.Combine(staticPath, "index.html");
            return Results.File(indexPath, "text/html");
        });

        // POST /api/send - send text or image
        app.MapPost("/api/send", async (HttpContext context) =>
        {
            var body = await context.Request.ReadFromJsonAsync<ApiMessage>();
            if (body == null || string.IsNullOrEmpty(body.Content))
                return Results.BadRequest(new { error = "Content is required" });

            if (body.Type != "text" && body.Type != "image")
                return Results.BadRequest(new { error = "Type must be 'text' or 'image'" });

            var msg = new MessageData
            {
                Type = body.Type,
                Content = body.Content,
                Timestamp = DateTime.Now
            };

            // Trigger animation on WPF thread
            _ = Dispatcher.BeginInvoke(() =>
            {
                _mainWindow?.TriggerAnimation(msg);
            });

            return Results.Ok(new { status = "ok", message = "Animation triggered" });
        });

        // POST /api/send-image - upload image file
        app.MapPost("/api/send-image", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded" });

            if (file.Length > 5 * 1024 * 1024)
                return Results.BadRequest(new { error = "File too large (max 5MB)" });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var msg = new MessageData
            {
                Type = "image",
                Content = base64,
                Timestamp = DateTime.Now
            };

            _ = Dispatcher.BeginInvoke(() =>
            {
                _mainWindow?.TriggerAnimation(msg);
            });

            return Results.Ok(new { status = "ok", message = "Image animation triggered" });
        });

        // GET /health
        app.MapGet("/health", () => Results.Ok(new { status = "running" }));

        // GET /api/version - get current version
        app.MapGet("/api/version", () => Results.Ok(new
        {
            version = _autoUpdater?.GetCurrentVersionString() ?? "1.0.0",
            app = "CrayonShinchanNotification"
        }));

        // POST /api/check-update - manually trigger update check
        app.MapPost("/api/check-update", async () =>
        {
            if (_autoUpdater != null)
            {
                await _autoUpdater.CheckForUpdatesAsync();
                return Results.Ok(new { status = "ok", message = "Update check triggered" });
            }
            return Results.BadRequest(new { error = "Updater not initialized" });
        });

        Console.WriteLine("=========================================");
        Console.WriteLine("  Crayon Shin-chan Notification");
        Console.WriteLine("  API:  http://0.0.0.0:8000");
        Console.WriteLine("  Web:  http://localhost:8000/");
        Console.WriteLine($"  API Key: {apiKey}");
        Console.WriteLine("  (可通过环境变量 SHINCHAN_API_KEY 修改)");
        Console.WriteLine("  托盘图标右键可以退出程序");
        Console.WriteLine("=========================================");

        app.Run();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
        Environment.Exit(0);
    }
}

public class ApiMessage
{
    public string Type { get; set; } = "text";
    public string Content { get; set; } = "";
}
