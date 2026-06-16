using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

namespace CrayonShinchanNotification;

/// <summary>
/// 支持透明背景的 GIF 播放器
/// </summary>
public class GifPlayer : System.Windows.Controls.Image
{
    private GifBitmapDecoder? _decoder;
    private int _currentFrame;
    private DispatcherTimer? _frameTimer;
    private bool _isPlaying;

    public GifPlayer()
    {
        Stretch = Stretch.Uniform;
    }

    /// <summary>
    /// 加载 GIF 文件
    /// </summary>
    public void LoadGif(string path)
    {
        Stop();

        var uri = new Uri(path, UriKind.Absolute);
        _decoder = new GifBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        if (_decoder.Frames.Count > 0)
        {
            Source = _decoder.Frames[0];
            _currentFrame = 0;
        }
    }

    /// <summary>
    /// 开始播放
    /// </summary>
    public void Play()
    {
        if (_decoder == null || _decoder.Frames.Count <= 1)
            return;

        _isPlaying = true;

        // Get frame delay from metadata, default to 100ms
        int delay = GetFrameDelay();

        _frameTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(delay)
        };
        _frameTimer.Tick += (s, e) =>
        {
            if (!_isPlaying || _decoder == null) return;

            _currentFrame = (_currentFrame + 1) % _decoder.Frames.Count;
            Source = _decoder.Frames[_currentFrame];
        };
        _frameTimer.Start();
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
        _frameTimer?.Stop();
        _frameTimer = null;
    }

    /// <summary>
    /// 获取 GIF 帧数
    /// </summary>
    public int FrameCount => _decoder?.Frames.Count ?? 0;

    /// <summary>
    /// 获取图片宽度
    /// </summary>
    public double NaturalWidth => _decoder?.Frames[0].PixelWidth ?? 0;

    /// <summary>
    /// 获取图片高度
    /// </summary>
    public double NaturalHeight => _decoder?.Frames[0].PixelHeight ?? 0;

    /// <summary>
    /// 获取帧延迟（毫秒），从 GIF 元数据读取
    /// </summary>
    private int GetFrameDelay()
    {
        try
        {
            if (_decoder?.Frames.Count > 0)
            {
                var metadata = _decoder.Frames[0].Metadata as BitmapMetadata;
                if (metadata != null)
                {
                    // Try to get delay from GIF metadata
                    var delay = metadata.GetQuery("/grctlext/Delay");
                    if (delay is int d)
                        return d * 10; // GIF delay is in 1/100th of a second
                }
            }
        }
        catch { }

        return 100; // Default 100ms (10fps)
    }
}
