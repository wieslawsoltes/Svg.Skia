using System.ComponentModel;

namespace AvalonDraw.Services;

public enum AppearanceEffect
{
    None,
    DropShadow,
    Blur
}

public class AppearanceLayer : INotifyPropertyChanged
{
    private string? _fill;
    private string? _stroke;
    private float _strokeWidth;
    private AppearanceEffect _effect;
    private float _blurRadius;
    private float _shadowOffsetX;
    private float _shadowOffsetY;
    private string? _shadowColor;

    public string? Fill
    {
        get => _fill;
        set
        {
            if (_fill != value)
            {
                _fill = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fill)));
            }
        }
    }

    public string? Stroke
    {
        get => _stroke;
        set
        {
            if (_stroke != value)
            {
                _stroke = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stroke)));
            }
        }
    }

    public float StrokeWidth
    {
        get => _strokeWidth;
        set
        {
            if (_strokeWidth != value)
            {
                _strokeWidth = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StrokeWidth)));
            }
        }
    }

    public AppearanceEffect Effect
    {
        get => _effect;
        set
        {
            if (_effect != value)
            {
                _effect = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Effect)));
            }
        }
    }

    public float BlurRadius
    {
        get => _blurRadius;
        set
        {
            if (_blurRadius != value)
            {
                _blurRadius = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlurRadius)));
            }
        }
    }

    public float ShadowOffsetX
    {
        get => _shadowOffsetX;
        set
        {
            if (_shadowOffsetX != value)
            {
                _shadowOffsetX = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShadowOffsetX)));
            }
        }
    }

    public float ShadowOffsetY
    {
        get => _shadowOffsetY;
        set
        {
            if (_shadowOffsetY != value)
            {
                _shadowOffsetY = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShadowOffsetY)));
            }
        }
    }

    public string? ShadowColor
    {
        get => _shadowColor;
        set
        {
            if (_shadowColor != value)
            {
                _shadowColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShadowColor)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

