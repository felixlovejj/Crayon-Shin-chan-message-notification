using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Brush = System.Windows.Media.Brush;

namespace CrayonShinchanNotification;

/// <summary>
/// 管理蜡笔小新跑步动画帧。
/// 支持加载 GIF 动画、水平排列的 PNG 精灵图，或回退到矢量程序化生成。
/// </summary>
public class ShinchanSpriteSheet : IDisposable
{
    private readonly BitmapSource[] _frames;

    public int FrameCount => _frames.Length;
    public int FrameWidth { get; }
    public int FrameHeight { get; }

    private ShinchanSpriteSheet(BitmapSource[] frames, int frameWidth, int frameHeight)
    {
        _frames = frames;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
    }

    public BitmapSource GetFrame(int index) => _frames[index % _frames.Length];

    /// <summary>
    /// 创建精灵图：优先 GIF > PNG > 程序化生成。
    /// </summary>
    public static ShinchanSpriteSheet Create(string basePath, int displayHeight)
    {
        string dir = Path.GetDirectoryName(basePath) ?? ".";
        string nameNoExt = Path.GetFileNameWithoutExtension(basePath);

        // 1. Try GIF
        string gifPath = Path.Combine(dir, nameNoExt + ".gif");
        if (File.Exists(gifPath))
        {
            try { return LoadFromGif(gifPath); }
            catch { }
        }

        // 2. Try PNG sprite sheet
        string pngPath = Path.Combine(dir, nameNoExt + ".png");
        if (File.Exists(pngPath))
        {
            try { return LoadFromPng(pngPath); }
            catch { }
        }

        // 3. Fallback to vector generation
        return GenerateProgrammatic(displayHeight);
    }

    /// <summary>
    /// 从 GIF 动画加载帧。
    /// </summary>
    private static ShinchanSpriteSheet LoadFromGif(string path)
    {
        var uri = new Uri(path, UriKind.Absolute);
        var decoder = new GifBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        int count = decoder.Frames.Count;
        if (count == 0) throw new InvalidOperationException("GIF has no frames");

        var frames = new BitmapSource[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = decoder.Frames[i];
            frames[i].Freeze();
        }

        int fw = frames[0].PixelWidth;
        int fh = frames[0].PixelHeight;
        return new ShinchanSpriteSheet(frames, fw, fh);
    }

    /// <summary>
    /// 从水平排列的 PNG 精灵图加载，按 8 帧切分。
    /// </summary>
    private static ShinchanSpriteSheet LoadFromPng(string path)
    {
        var uri = new Uri(path, UriKind.Absolute);
        var decoder = new PngBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var full = decoder.Frames[0];
        const int frameCount = 8;
        int fw = full.PixelWidth / frameCount;
        int fh = full.PixelHeight;
        var frames = new BitmapSource[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            var cropped = new CroppedBitmap(full, new Int32Rect(i * fw, 0, fw, fh));
            cropped.Freeze();
            frames[i] = cropped;
        }
        return new ShinchanSpriteSheet(frames, fw, fh);
    }

    private static ShinchanSpriteSheet GenerateProgrammatic(int displayHeight)
    {
        const int frameCount = 8;
        int fh = displayHeight * 2;
        int fw = (int)(fh * 0.7);
        var frames = new BitmapSource[frameCount];
        for (int i = 0; i < frameCount; i++)
            frames[i] = RenderFrame(i, fw, fh);
        return new ShinchanSpriteSheet(frames, fw, fh);
    }

    private static BitmapSource RenderFrame(int frameIndex, int w, int h)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            DrawCharacter(dc, frameIndex, w, h);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    // Running animation keyframes (8-frame cycle)
    private static readonly double[] LLeg =  { -30, -15, 0, 15, 30, 15, 0, -15 };
    private static readonly double[] RLeg =  {  30,  15, 0,-15,-30,-15, 0,  15 };
    private static readonly double[] LArm =  {  25,  12, 0,-12,-25,-12, 0,  12 };
    private static readonly double[] RArm =  { -25, -12, 0, 12, 25, 12, 0, -12 };
    private static readonly double[] Bounce = {   0, 0.5, 1,0.5,  0,0.5, 1,0.5 };

    // Brushes (frozen for performance)
    private static readonly SolidColorBrush SkinBrush       = Make(255, 213, 170);
    private static readonly SolidColorBrush SkinShadowBrush = Make(230, 185, 140);
    private static readonly SolidColorBrush HairBrush       = Make( 40,  25,  15);
    private static readonly SolidColorBrush EyeWhiteBrush   = Make(255, 255, 255);
    private static readonly SolidColorBrush EyeBlackBrush   = Make( 15,  15,  15);
    private static readonly SolidColorBrush MouthBrush      = Make(200,  60,  60);
    private static readonly SolidColorBrush ShirtBrush      = Make(230,  50,  50);
    private static readonly SolidColorBrush ShirtDarkBrush  = Make(200,  40,  40);
    private static readonly SolidColorBrush ShortsBrush     = Make(255, 210,   0);
    private static readonly SolidColorBrush ShoeBrush       = Make(200,  50,  50);
    private static readonly SolidColorBrush CheekBrush      = MakeA(255, 180, 150, 160);
    private static readonly SolidColorBrush ShadowBrush     = MakeA(  0,   0,   0,  30);
    private static readonly SolidColorBrush OutlineBrush    = Make( 30,  20,  10);

    private static SolidColorBrush Make(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private static SolidColorBrush MakeA(byte r, byte g, byte b, byte a)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }

    private static void DrawCharacter(DrawingContext dc, int f, int w, int h)
    {
        double ll = LLeg[f], rl = RLeg[f], la = LArm[f], ra = RArm[f], bo = Bounce[f];

        double hr = h * 0.155;
        double cx = w * 0.48;
        double bounceOff = bo * h * 0.025;
        double baseY = h * 0.42 - bounceOff;
        double bodyTop = baseY;
        double bodyH = h * 0.2;
        double bodyW = h * 0.17;
        double hipY = bodyTop + bodyH;
        double legLen = h * 0.24;
        double armLen = h * 0.17;
        double headCY = bodyTop - hr * 0.65;

        double shadowW = hr * 1.3 * (1.2 - bo * 0.3);
        dc.DrawEllipse(ShadowBrush, null, new Point(cx, h * 0.9), shadowW, h * 0.018);

        DrawLimb(dc, cx - bodyW * 0.18, hipY, ll, legLen, h * 0.048, SkinBrush, ShoeBrush, h * 0.07, h * 0.04);
        DrawLimb(dc, cx + bodyW * 0.18, hipY, rl, legLen, h * 0.048, SkinBrush, ShoeBrush, h * 0.07, h * 0.04);

        var shirtRect = new Rect(cx - bodyW / 2, bodyTop, bodyW, bodyH);
        dc.DrawRoundedRectangle(ShirtBrush, new Pen(OutlineBrush, 1.2), shirtRect, 5, 5);
        dc.DrawRoundedRectangle(ShirtDarkBrush, null,
            new Rect(shirtRect.X + 2, shirtRect.Y + bodyH * 0.3, bodyW - 4, bodyH * 0.15), 3, 3);

        DrawLimb(dc, cx - bodyW * 0.48, bodyTop + bodyH * 0.15, la, armLen, h * 0.038, SkinBrush, null, 0, 0);
        DrawLimb(dc, cx + bodyW * 0.48, bodyTop + bodyH * 0.15, ra, armLen, h * 0.038, SkinBrush, null, 0, 0);

        var shortsRect = new Rect(cx - bodyW * 0.42, hipY - h * 0.01, bodyW * 0.84, h * 0.09);
        dc.DrawRoundedRectangle(ShortsBrush, new Pen(OutlineBrush, 1), shortsRect, 3, 3);

        dc.DrawEllipse(SkinBrush, new Pen(OutlineBrush, 1.2), new Point(cx, headCY), hr, hr);
        DrawHair(dc, cx, headCY, hr);
        DrawFace(dc, cx, headCY, hr);
    }

    private static void DrawLimb(DrawingContext dc, double jx, double jy,
        double angleDeg, double length, double width,
        System.Windows.Media.Brush skinBrush, System.Windows.Media.Brush? endBrush, double endW, double endH)
    {
        double rad = angleDeg * Math.PI / 180;
        double dx = Math.Sin(rad) * length;
        double dy = Math.Cos(rad) * length;

        var pen = new Pen(skinBrush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, new Point(jx, jy), new Point(jx + dx, jy + dy));

        if (endBrush != null)
            dc.DrawEllipse(endBrush, new Pen(OutlineBrush, 0.8),
                new Point(jx + dx, jy + dy + endH * 0.3), endW * 0.5, endH * 0.5);
    }

    private static void DrawHair(DrawingContext dc, double cx, double cy, double r)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(cx - r * 0.85, cy - r * 0.15), true, true);
            ctx.BezierTo(
                new Point(cx - r * 0.7, cy - r * 1.15),
                new Point(cx - r * 0.1, cy - r * 1.35),
                new Point(cx + r * 0.15, cy - r * 1.2), true, false);
            ctx.BezierTo(
                new Point(cx + r * 0.5, cy - r * 1.45),
                new Point(cx + r * 0.8, cy - r * 1.1),
                new Point(cx + r * 0.9, cy - r * 0.25), true, false);
            ctx.BezierTo(
                new Point(cx + r * 0.95, cy - r * 0.05),
                new Point(cx + r * 0.85, cy + r * 0.12),
                new Point(cx + r * 0.65, cy + r * 0.15), true, false);
            ctx.LineTo(new Point(cx - r * 0.65, cy + r * 0.15), true, false);
            ctx.BezierTo(
                new Point(cx - r * 0.85, cy + r * 0.1),
                new Point(cx - r * 0.92, cy - r * 0.02),
                new Point(cx - r * 0.85, cy - r * 0.15), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(HairBrush, null, geo);
    }

    private static void DrawFace(DrawingContext dc, double cx, double cy, double r)
    {
        double eo = r * 0.3;
        double ey = cy - r * 0.05;
        double ew = r * 0.28, eh = r * 0.35;

        dc.DrawEllipse(EyeWhiteBrush, null, new Point(cx - eo, ey), ew, eh);
        dc.DrawEllipse(EyeBlackBrush, null, new Point(cx - eo + r * 0.02, ey), ew * 0.45, eh * 0.5);
        dc.DrawEllipse(EyeWhiteBrush, null, new Point(cx - eo - r * 0.05, ey - r * 0.08), ew * 0.2, eh * 0.2);

        dc.DrawEllipse(EyeWhiteBrush, null, new Point(cx + eo, ey), ew, eh);
        dc.DrawEllipse(EyeBlackBrush, null, new Point(cx + eo + r * 0.02, ey), ew * 0.45, eh * 0.5);
        dc.DrawEllipse(EyeWhiteBrush, null, new Point(cx + eo - r * 0.05, ey - r * 0.08), ew * 0.2, eh * 0.2);

        double my = cy + r * 0.25;
        var mg = new StreamGeometry();
        using (var ctx = mg.Open())
        {
            ctx.BeginFigure(new Point(cx - r * 0.12, my), false, false);
            ctx.QuadraticBezierTo(new Point(cx, my + r * 0.1), new Point(cx + r * 0.12, my), true, false);
        }
        mg.Freeze();
        dc.DrawGeometry(null, new Pen(MouthBrush, 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, mg);

        dc.DrawEllipse(CheekBrush, null, new Point(cx - r * 0.48, cy + r * 0.15), r * 0.16, r * 0.1);
        dc.DrawEllipse(CheekBrush, null, new Point(cx + r * 0.48, cy + r * 0.15), r * 0.16, r * 0.1);
    }

    public void Dispose() { }
}
