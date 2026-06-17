using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.IO;
using System.Runtime.InteropServices;

using MediaColor = System.Windows.Media.Color;
using MediaSize = System.Windows.Size;
using MediaFontFamily = System.Windows.Media.FontFamily;

using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;
using Path = System.Windows.Shapes.Path;

namespace CrayonShinchanNotification;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    // ═══ Animation constants ═════════════════════════════════════
    private const double CharHeight = 160;
    private const double RunSpeed = 320;          // px/s
    private const double OffscreenMargin = 80;
    private const double FadeTime = 0.35;         // fade in/out seconds
    private const double SpriteFPS = 10;          // sprite frames per second
    private const double BounceAmp = 5;           // px
    private const double BounceFreq = 4.0;        // Hz
    private const double SpringK = 12.0;          // spring stiffness
    private const double SpringC = 7.0;           // damping coefficient
    private const double BannerWaveAmp = 3;       // px
    private const double BannerWaveFreq = 2.0;    // Hz
    private const double DustInterval = 0.07;     // seconds between spawns
    private const int MaxDust = 25;

    // ═══ State ═══════════════════════════════════════════════════
    private bool _isAnimating;
    private DateTime _startTime;
    private DateTime _lastFrame;
    private double _elapsed;
    private MessageData? _msg;
    private double _screenW, _screenH;
    private double _charW, _bannerW, _totalDist, _totalDur;
    private double _charX, _charY;
    private double _banX, _banY, _banVX, _banVY;
    
    // Hand positions per frame (normalized 0-1 within GIF frame 272x724)
    // Provided by user for rope attachment
    private static readonly double[] HandXN = { 194.0/233, 180.0/233, 150.0/233, 182.0/233, 195.0/233, 189.0/233, 148.0/233, 178.0/233 };
    private static readonly double[] HandYN = { 161.0/291, 165.0/291, 180.0/291, 168.0/291, 159.0/291, 173.0/291, 178.0/291, 162.0/291 };
private double _dustTimer;
    private readonly Random _rng = new();

    // ═══ Visual elements ═════════════════════════════════════════
    private System.Windows.Controls.Image? _charImg;
    private Ellipse? _shadow;
    private System.Windows.Shapes.Path? _rope;
    private Border? _banner;
    private RotateTransform? _bannerRotate;
    private readonly List<DustP> _dust = new();
    private ShinchanSpriteSheet? _sprites;

    public MainWindow()
    {
        InitializeComponent();
        _screenW = SystemParameters.PrimaryScreenWidth;
        _screenH = SystemParameters.PrimaryScreenHeight;
        Width = _screenW;
        Height = _screenH;
        Loaded += OnLoaded;
        Hide();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    // ══════════════════════════════════════════════════════════════
    // Public entry point
    // ══════════════════════════════════════════════════════════════

    public void TriggerAnimation(MessageData message)
    {
        if (_isAnimating) StopAnimation();
        _msg = message;
        _isAnimating = true;
        AnimationCanvas.Children.Clear();
        _dust.Clear();

        // Load sprite sheet (external PNG preferred, fallback to vector generation)
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "shinchan_run.png");
        _sprites?.Dispose();
        _sprites = ShinchanSpriteSheet.Create(path, (int)CharHeight);
        _charW = _sprites.FrameWidth * (CharHeight / _sprites.FrameHeight);

        // Character image (flip horizontally so character faces left = running direction)
        _charImg = new Image
        {
            Width = _charW,
            Height = CharHeight,
            Stretch = Stretch.Uniform
        };
        AnimationCanvas.Children.Add(_charImg);

        // Character shadow
        _shadow = new Ellipse
        {
            Width = _charW * 0.45,
            Height = 10,
            Fill = new SolidColorBrush(MediaColor.FromArgb(35, 0, 0, 0))
        };
        ((SolidColorBrush)_shadow.Fill).Freeze();
        AnimationCanvas.Children.Add(_shadow);

        // Rope (Bezier curve path)
        _rope = new Path
        {
            Stroke = new SolidColorBrush(MediaColor.FromRgb(139, 90, 43)),
            StrokeThickness = 2.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        ((SolidColorBrush)_rope.Stroke).Freeze();
        AnimationCanvas.Children.Add(_rope);

        // Banner
        CreateBanner(message);

        // Calculate total animation duration
        _totalDist = _screenW + OffscreenMargin + _charW + _bannerW + OffscreenMargin + 200;
        _totalDur = _totalDist / RunSpeed;

        // Initialize positions
        _charX = _screenW + OffscreenMargin;
        _charY = (_screenH - CharHeight) / 2;
        _banX = _charX + _charW * 0.85;
        _banY = _charY + CharHeight * 0.4;
        _banVX = _banVY = 0;
        _dustTimer = 0;
        _elapsed = 0;

        // Use CompositionTarget.Rendering (vsync-locked, true 60fps smooth animation)
        _startTime = _lastFrame = DateTime.Now;
        CompositionTarget.Rendering += OnRendering;
        Show();
        Activate();
    }

    private void CreateBanner(MessageData msg)
    {
        _bannerRotate = new RotateTransform(0);
        var bg = new SolidColorBrush(MediaColor.FromRgb(204, 0, 0));
        var border = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        var fg = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        bg.Freeze(); border.Freeze(); fg.Freeze();

        _banner = new Border
        {
            Background = bg,
            BorderBrush = border,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 12, 20, 12),
            MaxWidth = 500,
            RenderTransform = _bannerRotate,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.25
            }
        };

        if (msg.Type == "text")
        {
            _banner.Child = new TextBlock
            {
                Text = msg.Content,
                Foreground = fg,
                FontSize = 24,
                FontFamily = new MediaFontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap
            };
        }
        else if (msg.Type == "image")
        {
            try
            {
                var img = new Image
                {
                    MaxHeight = 100,
                    MaxWidth = 350,
                    Stretch = Stretch.Uniform
                };
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                using var ms = new MemoryStream(Convert.FromBase64String(msg.Content));
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                img.Source = bmp;
                _banner.Child = img;
            }
            catch
            {
                _banner.Child = new TextBlock
                {
                    Text = "[图片加载失败]",
                    Foreground = fg,
                    FontSize = 20,
                    FontFamily = new MediaFontFamily("Microsoft YaHei")
                };
            }
        }

        _banner.Measure(new MediaSize(double.PositiveInfinity, double.PositiveInfinity));
        _bannerW = _banner.DesiredSize.Width;
        AnimationCanvas.Children.Add(_banner);
    }

    // ══════════════════════════════════════════════════════════════
    // Main animation loop (vsync-driven)
    // ══════════════════════════════════════════════════════════════

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_isAnimating) return;

        var now = DateTime.Now;
        double dt = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        dt = Math.Min(dt, 0.05); // clamp to prevent big jumps
        _elapsed = (now - _startTime).TotalSeconds;

        if (_elapsed >= _totalDur)
        {
            StopAnimation();
            return;
        }

        UpdateCharacter(dt);
        UpdateBanner(dt);
        UpdateRope();
        UpdateParticles(dt);
    }

    // ── Character update ─────────────────────────────────────────

    private void UpdateCharacter(double dt)
    {
        // Constant speed, right to left
        double progress = _elapsed / _totalDur;
        double startX = _screenW + OffscreenMargin;
        double endX = -(_charW + _bannerW + OffscreenMargin + 200);
        _charX = startX + (endX - startX) * progress;

        // Vertical bounce (sine wave simulating running gait)
        double bounce = Math.Sin(_elapsed * BounceFreq * Math.PI * 2) * BounceAmp;
        _charY = (_screenH - CharHeight) / 2 + bounce;

        // Sprite frame switching
        int frame = (int)(_elapsed * SpriteFPS) % _sprites!.FrameCount;
        _charImg!.Source = _sprites.GetFrame(frame);

        // Entry scale animation
        double scale = 1.0;
        if (_elapsed < 0.3) scale = 0.85 + 0.15 * EaseOutCubic(_elapsed / 0.3);
        _charImg.RenderTransform = new ScaleTransform(scale, scale, 0.5, 0.5);

        // Opacity fade in/out
        double opacity = 1.0;
        if (_elapsed < FadeTime) opacity = _elapsed / FadeTime;
        if (_totalDur - _elapsed < FadeTime) opacity = (_totalDur - _elapsed) / FadeTime;
        _charImg.Opacity = opacity;

        Canvas.SetLeft(_charImg, _charX);
        Canvas.SetTop(_charImg, _charY);

        // Shadow (smaller and fainter when bouncing higher)
        if (_shadow != null)
        {
            double bounceNorm = bounce / BounceAmp;
            double shadowScale = 1.0 - bounceNorm * 0.3;
            _shadow.Width = _charW * 0.45 * shadowScale;
            Canvas.SetLeft(_shadow, _charX + (_charW - _shadow.Width) / 2);
            Canvas.SetTop(_shadow, _screenH * 0.5 + CharHeight * 0.45);
            _shadow.Opacity = opacity * 0.5 * shadowScale;
        }
    }

    // ── Banner update (spring-damper physics) ────────────────────

    private void UpdateBanner(double dt)
    {
        if (_banner == null) return;

        // Target: rope attachment point on character
        int banFrame = (int)(_elapsed * SpriteFPS) % _sprites!.FrameCount;
        double targetX = _charX + HandXN[banFrame] * _charW;
        double targetY = _charY + HandYN[banFrame] * CharHeight;

        // Spring-damper: F = -k*x - c*v (semi-implicit Euler for stability)
        double dx = _banX - targetX;
        double dy = _banY - targetY;
        double ax = -SpringK * dx - SpringC * _banVX;
        double ay = -SpringK * dy - SpringC * _banVY;

        _banVX += ax * dt;
        _banVY += ay * dt;
        _banX += _banVX * dt;
        _banY += _banVY * dt;

        // Wave flutter effect (two superimposed frequencies for organic feel)
        double wave = BannerWaveAmp * Math.Sin(_elapsed * BannerWaveFreq * Math.PI * 2);
        double tilt = 2.0 * Math.Sin(_elapsed * BannerWaveFreq * 1.7 * Math.PI * 2);

        // Opacity
        double opacity = 1.0;
        if (_elapsed < FadeTime) opacity = _elapsed / FadeTime;
        if (_totalDur - _elapsed < FadeTime) opacity = (_totalDur - _elapsed) / FadeTime;

        _banner.Opacity = opacity;
        _bannerRotate!.Angle = tilt;

        Canvas.SetLeft(_banner, _banX);
        Canvas.SetTop(_banner, _banY + wave);
    }

    // ── Rope update (Bezier curve) ───────────────────────────────

    private void UpdateRope()
    {
        if (_rope == null) return;

        int ropeFrame = (int)(_elapsed * SpriteFPS) % _sprites!.FrameCount;
        double sx = _charX + HandXN[ropeFrame] * _charW;
        double sy = _charY + HandYN[ropeFrame] * CharHeight;
        double ex = _banX;
        double ey = _banY + 10;

        // Control point: midpoint with natural droop (catenary approximation)
        double dist = Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy));
        double droop = Math.Min(dist * 0.12, 25);
        double mx = (sx + ex) / 2;
        double my = Math.Max(sy, ey) + droop;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(sx, sy), false, false);
            ctx.QuadraticBezierTo(new Point(mx, my), new Point(ex, ey), true, false);
        }
        geo.Freeze();
        _rope.Data = geo;

        double opacity = 1.0;
        if (_elapsed < FadeTime) opacity = _elapsed / FadeTime;
        if (_totalDur - _elapsed < FadeTime) opacity = (_totalDur - _elapsed) / FadeTime;
        _rope.Opacity = opacity;
    }

    // ══════════════════════════════════════════════════════════════
    // Dust particle system
    // ══════════════════════════════════════════════════════════════

    private void UpdateParticles(double dt)
    {
        _dustTimer += dt;
        while (_dustTimer >= DustInterval)
        {
            _dustTimer -= DustInterval;
            if (_dust.Count < MaxDust) SpawnDust();
        }

        for (int i = _dust.Count - 1; i >= 0; i--)
        {
            var p = _dust[i];
            p.Age += dt;
            if (p.Age >= p.MaxAge)
            {
                AnimationCanvas.Children.Remove(p.Ell);
                _dust.RemoveAt(i);
                continue;
            }
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.VY -= 15 * dt; // drift upward
            double t = p.Age / p.MaxAge;
            p.Ell.Opacity = 0.4 * (1 - t * t);
            p.Ell.Width = p.Size * (1 + t * 0.5);
            p.Ell.Height = p.Size * (0.6 + t * 0.3);
            Canvas.SetLeft(p.Ell, p.X);
            Canvas.SetTop(p.Ell, p.Y);
        }
    }

    private void SpawnDust()
    {
        double size = 3 + _rng.NextDouble() * 5;
        var ell = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(MediaColor.FromArgb(100, 210, 180, 140))
        };
        ((SolidColorBrush)ell.Fill).Freeze();

        var p = new DustP
        {
            Ell = ell,
            X = _charX + _charW * 0.15 + _rng.NextDouble() * _charW * 0.35,
            Y = _charY + CharHeight * 0.82 + _rng.NextDouble() * 8,
            VX = -40 - _rng.NextDouble() * 50,
            VY = -15 - _rng.NextDouble() * 25,
            Age = 0,
            MaxAge = 0.3 + _rng.NextDouble() * 0.3,
            Size = size
        };

        Canvas.SetLeft(ell, p.X);
        Canvas.SetTop(ell, p.Y);
        AnimationCanvas.Children.Add(ell);
        _dust.Add(p);
    }

    // ══════════════════════════════════════════════════════════════
    // Stop animation
    // ══════════════════════════════════════════════════════════════

    private void StopAnimation()
    {
        _isAnimating = false;
        CompositionTarget.Rendering -= OnRendering;
        AnimationCanvas.Children.Clear();
        _dust.Clear();
        _charImg = null;
        _shadow = null;
        _rope = null;
        _banner = null;
        _bannerRotate = null;
        Hide();
    }

    // ── Easing ───────────────────────────────────────────────────

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    // ── Dust particle ────────────────────────────────────────────

    private class DustP
    {
        public Ellipse Ell = null!;
        public double X, Y, VX, VY, Age, MaxAge, Size;
    }
}
