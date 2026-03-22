using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorEffectItem : INotifyPropertyChanged
{
    private EditorEffectKind _kind;
    private bool _isEnabled;
    private double _offsetX;
    private double _offsetY;
    private double _blur;
    private double _spread;
    private double _scale;
    private double _amount;
    private double _distortion;
    private double _saturation;
    private Color _color;

    public event PropertyChangedEventHandler? PropertyChanged;

    public EditorEffectKind Kind
    {
        get => _kind;
        set
        {
            if (SetField(ref _kind, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    public double OffsetX
    {
        get => _offsetX;
        set
        {
            if (SetField(ref _offsetX, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double OffsetY
    {
        get => _offsetY;
        set
        {
            if (SetField(ref _offsetY, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double Blur
    {
        get => _blur;
        set
        {
            if (SetField(ref _blur, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double Spread
    {
        get => _spread;
        set
        {
            if (SetField(ref _spread, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double Scale
    {
        get => _scale;
        set
        {
            if (SetField(ref _scale, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double Amount
    {
        get => _amount;
        set
        {
            if (SetField(ref _amount, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double Distortion
    {
        get => _distortion;
        set
        {
            if (SetField(ref _distortion, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double Saturation
    {
        get => _saturation;
        set
        {
            if (SetField(ref _saturation, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            if (SetField(ref _color, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public string DisplayName => GetDisplayName(Kind);

    public string Summary => Kind switch
    {
        EditorEffectKind.DropShadow or EditorEffectKind.InnerShadow
            => $"{FormatNumber(OffsetX)}, {FormatNumber(OffsetY)} • {FormatNumber(Blur)} blur • {FormatPercent(Color.A)}%",
        EditorEffectKind.LayerBlur or EditorEffectKind.BackgroundBlur
            => $"{FormatNumber(Blur)} blur",
        EditorEffectKind.Noise or EditorEffectKind.Texture
            => $"{FormatNumber(Amount)} amount • {FormatNumber(Scale)} scale",
        EditorEffectKind.Glass
            => $"{FormatNumber(Blur)} blur • {FormatNumber(Distortion)} distortion",
        _ => DisplayName
    };

    public Visibility PositionVisibility => HasPosition(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BlurVisibility => HasBlur(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpreadVisibility => HasSpread(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColorVisibility => HasColor(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ScaleVisibility => HasScale(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AmountVisibility => HasAmount(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DistortionVisibility => HasDistortion(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SaturationVisibility => HasSaturation(Kind) ? Visibility.Visible : Visibility.Collapsed;

    public string OffsetXText
    {
        get => FormatNumber(OffsetX);
        set => TryApplyDouble(value, v => OffsetX = v);
    }

    public string OffsetYText
    {
        get => FormatNumber(OffsetY);
        set => TryApplyDouble(value, v => OffsetY = v);
    }

    public string BlurText
    {
        get => FormatNumber(Blur);
        set => TryApplyDouble(value, v => Blur = Math.Max(0.0, v));
    }

    public string SpreadText
    {
        get => FormatNumber(Spread);
        set => TryApplyDouble(value, v => Spread = v);
    }

    public string ScaleText
    {
        get => FormatNumber(Scale);
        set => TryApplyDouble(value, v => Scale = Math.Max(0.0, v));
    }

    public string AmountText
    {
        get => FormatNumber(Amount);
        set => TryApplyDouble(value, v => Amount = Math.Max(0.0, v));
    }

    public string DistortionText
    {
        get => FormatNumber(Distortion);
        set => TryApplyDouble(value, v => Distortion = Math.Max(0.0, v));
    }

    public string SaturationText
    {
        get => FormatNumber(Saturation);
        set => TryApplyDouble(value, v => Saturation = Math.Max(0.0, v));
    }

    public static EditorEffectItem CreateDefault(EditorEffectKind kind)
    {
        return kind switch
        {
            EditorEffectKind.DropShadow => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                OffsetX = 0.0,
                OffsetY = 4.0,
                Blur = 4.0,
                Spread = 0.0,
                Color = Color.FromArgb(64, 0, 0, 0)
            },
            EditorEffectKind.InnerShadow => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                OffsetX = 0.0,
                OffsetY = 2.0,
                Blur = 6.0,
                Spread = 0.0,
                Color = Color.FromArgb(56, 0, 0, 0)
            },
            EditorEffectKind.LayerBlur => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                Blur = 8.0
            },
            EditorEffectKind.BackgroundBlur => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                Blur = 18.0
            },
            EditorEffectKind.Noise => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                Scale = 0.12,
                Amount = 18.0,
                Color = Color.FromArgb(44, 255, 255, 255)
            },
            EditorEffectKind.Texture => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                Scale = 0.035,
                Amount = 24.0,
                Distortion = 6.0,
                Color = Color.FromArgb(32, 255, 255, 255)
            },
            EditorEffectKind.Glass => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true,
                Blur = 18.0,
                Distortion = 16.0,
                Saturation = 1.15,
                Color = Color.FromArgb(36, 255, 255, 255)
            },
            _ => new EditorEffectItem
            {
                Kind = kind,
                IsEnabled = true
            }
        };
    }

    public void RaiseDisplayState()
    {
        RaiseDerivedProperties();
    }

    private static string GetDisplayName(EditorEffectKind kind)
    {
        return kind switch
        {
            EditorEffectKind.DropShadow => "Drop shadow",
            EditorEffectKind.InnerShadow => "Inner shadow",
            EditorEffectKind.LayerBlur => "Layer blur",
            EditorEffectKind.BackgroundBlur => "Background blur",
            EditorEffectKind.Noise => "Noise",
            EditorEffectKind.Texture => "Texture",
            EditorEffectKind.Glass => "Glass",
            _ => kind.ToString()
        };
    }

    private static bool HasPosition(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.DropShadow or EditorEffectKind.InnerShadow;
    }

    private static bool HasBlur(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.DropShadow
            or EditorEffectKind.InnerShadow
            or EditorEffectKind.LayerBlur
            or EditorEffectKind.BackgroundBlur
            or EditorEffectKind.Glass;
    }

    private static bool HasSpread(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.DropShadow or EditorEffectKind.InnerShadow;
    }

    private static bool HasColor(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.DropShadow
            or EditorEffectKind.InnerShadow
            or EditorEffectKind.Noise
            or EditorEffectKind.Texture
            or EditorEffectKind.Glass;
    }

    private static bool HasScale(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.Noise or EditorEffectKind.Texture;
    }

    private static bool HasAmount(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.Noise or EditorEffectKind.Texture;
    }

    private static bool HasDistortion(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.Texture or EditorEffectKind.Glass;
    }

    private static bool HasSaturation(EditorEffectKind kind)
    {
        return kind is EditorEffectKind.Glass;
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static int FormatPercent(byte alpha)
    {
        return (int)Math.Round(alpha / 255.0 * 100.0);
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private void TryApplyDouble(string? text, Action<double> apply)
    {
        if (TryParseDouble(text, out var value))
        {
            apply(value);
        }
        else
        {
            RaiseDerivedProperties();
        }
    }

    private void RaiseDerivedProperties()
    {
        RaisePropertyChanged(nameof(DisplayName));
        RaisePropertyChanged(nameof(Summary));
        RaisePropertyChanged(nameof(PositionVisibility));
        RaisePropertyChanged(nameof(BlurVisibility));
        RaisePropertyChanged(nameof(SpreadVisibility));
        RaisePropertyChanged(nameof(ColorVisibility));
        RaisePropertyChanged(nameof(ScaleVisibility));
        RaisePropertyChanged(nameof(AmountVisibility));
        RaisePropertyChanged(nameof(DistortionVisibility));
        RaisePropertyChanged(nameof(SaturationVisibility));
        RaisePropertyChanged(nameof(OffsetXText));
        RaisePropertyChanged(nameof(OffsetYText));
        RaisePropertyChanged(nameof(BlurText));
        RaisePropertyChanged(nameof(SpreadText));
        RaisePropertyChanged(nameof(ScaleText));
        RaisePropertyChanged(nameof(AmountText));
        RaisePropertyChanged(nameof(DistortionText));
        RaisePropertyChanged(nameof(SaturationText));
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
