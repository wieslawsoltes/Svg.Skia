using System;
using System.Collections.Generic;
using System.Globalization;
using ShimSkiaSharp;
using Svg.Transforms;

namespace Svg.JavaScript;

internal static class SvgJavaScriptMatrixHelpers
{
    public static SKMatrix ToSkMatrix(SvgTransform? transform)
    {
        return transform switch
        {
            SvgMatrix svgMatrix => new SKMatrix
            {
                ScaleX = svgMatrix.Points[0],
                SkewY = svgMatrix.Points[1],
                SkewX = svgMatrix.Points[2],
                ScaleY = svgMatrix.Points[3],
                TransX = svgMatrix.Points[4],
                TransY = svgMatrix.Points[5],
                Persp0 = 0f,
                Persp1 = 0f,
                Persp2 = 1f
            },
            SvgRotate rotate => SKMatrix.CreateRotationDegrees(rotate.Angle, rotate.CenterX, rotate.CenterY),
            SvgScale scale => SKMatrix.CreateScale(scale.X, scale.Y),
            SvgSkew skew => SKMatrix.CreateSkew(
                (float)Math.Tan(Math.PI * skew.AngleX / 180d),
                (float)Math.Tan(Math.PI * skew.AngleY / 180d)),
            SvgTranslate translate => SKMatrix.CreateTranslation(translate.X, translate.Y),
            _ => SKMatrix.Identity
        };
    }

    public static SKMatrix ToSkMatrix(SvgTransformCollection? transforms)
    {
        var total = SKMatrix.Identity;
        if (transforms is null)
        {
            return total;
        }

        for (var i = 0; i < transforms.Count; i++)
        {
            total = total.PreConcat(ToSkMatrix(transforms[i]));
        }

        return total;
    }

    public static SvgTransform FromSkMatrix(SKMatrix matrix)
    {
        return new SvgMatrix(new List<float>
        {
            matrix.ScaleX,
            matrix.SkewY,
            matrix.SkewX,
            matrix.ScaleY,
            matrix.TransX,
            matrix.TransY
        });
    }
}

public sealed class SvgJavaScriptRect
{
    private readonly SvgJavaScriptRuntime? _runtime;
    private SvgJavaScriptRectState _state;
    private readonly Func<SvgJavaScriptRectState>? _getter;
    private readonly Action<SvgJavaScriptRectState>? _setter;
    private readonly bool _readOnly;

    public SvgJavaScriptRect(float x, float y, float width, float height)
    {
        _state = new SvgJavaScriptRectState(x, y, width, height);
    }

    internal SvgJavaScriptRect(
        SvgJavaScriptRuntime runtime,
        Func<SvgJavaScriptRectState> getter,
        Action<SvgJavaScriptRectState>? setter,
        bool readOnly)
    {
        _runtime = runtime;
        _state = getter();
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public float x
    {
        get => GetState().X;
        set => UpdateState(state => state.X = value);
    }

    public float y
    {
        get => GetState().Y;
        set => UpdateState(state => state.Y = value);
    }

    public float width
    {
        get => GetState().Width;
        set => UpdateState(state => state.Width = value);
    }

    public float height
    {
        get => GetState().Height;
        set => UpdateState(state => state.Height = value);
    }

    internal static SvgJavaScriptRect From(SKRect rect)
    {
        return new SvgJavaScriptRect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    internal SKRect ToSkRect()
    {
        var state = GetState();
        return new SKRect(state.X, state.Y, state.X + state.Width, state.Y + state.Height);
    }

    private SvgJavaScriptRectState GetState()
    {
        return _getter?.Invoke() ?? _state;
    }

    private void UpdateState(Action<SvgJavaScriptRectState> update)
    {
        if (_readOnly)
        {
            _runtime?.ThrowDomException(7, "This SVGRect is read only.");
        }

        var state = GetState();
        update(state);
        if (_setter is not null)
        {
            _setter(state);
        }
        else
        {
            _state = state;
        }
    }

    internal sealed class SvgJavaScriptRectState
    {
        public SvgJavaScriptRectState(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}

public sealed class SvgJavaScriptPoint
{
    private readonly SvgJavaScriptRuntime? _runtime;
    private SvgJavaScriptPointState _state;
    private readonly Func<SvgJavaScriptPointState>? _getter;
    private readonly Action<SvgJavaScriptPointState>? _setter;
    private readonly bool _readOnly;

    public SvgJavaScriptPoint()
        : this(0f, 0f)
    {
    }

    public SvgJavaScriptPoint(float x, float y)
    {
        _state = new SvgJavaScriptPointState(x, y);
    }

    internal SvgJavaScriptPoint(
        SvgJavaScriptRuntime runtime,
        Func<SvgJavaScriptPointState> getter,
        Action<SvgJavaScriptPointState>? setter,
        bool readOnly)
    {
        _runtime = runtime;
        _state = getter();
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public float x
    {
        get => GetState().X;
        set => UpdateState(state => state.X = value);
    }

    public float y
    {
        get => GetState().Y;
        set => UpdateState(state => state.Y = value);
    }

    public SvgJavaScriptPoint matrixTransform(SvgJavaScriptMatrix matrix)
    {
        if (matrix is null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        var state = GetState();
        var mapped = matrix.ToSkMatrix().MapPoint(new SKPoint(state.X, state.Y));
        return new SvgJavaScriptPoint(mapped.X, mapped.Y);
    }

    internal SKPoint ToSkPoint()
    {
        var state = GetState();
        return new SKPoint(state.X, state.Y);
    }

    private SvgJavaScriptPointState GetState()
    {
        return _getter?.Invoke() ?? _state;
    }

    private void UpdateState(Action<SvgJavaScriptPointState> update)
    {
        if (_readOnly)
        {
            _runtime?.ThrowDomException(7, "This SVGPoint is read only.");
        }

        var state = GetState();
        update(state);
        if (_setter is not null)
        {
            _setter(state);
        }
        else
        {
            _state = state;
        }
    }

    internal sealed class SvgJavaScriptPointState
    {
        public SvgJavaScriptPointState(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }
        public float Y { get; set; }
    }
}

public sealed class SvgJavaScriptMatrix
{
    private readonly SvgJavaScriptRuntime? _runtime;
    private SKMatrix _matrix;
    private readonly Func<SKMatrix>? _getter;
    private readonly Action<SKMatrix>? _setter;
    private readonly bool _readOnly;

    public SvgJavaScriptMatrix()
        : this(SKMatrix.Identity)
    {
    }

    public SvgJavaScriptMatrix(float a, float b, float c, float d, float e, float f)
        : this(new SKMatrix
        {
            ScaleX = a,
            SkewY = b,
            SkewX = c,
            ScaleY = d,
            TransX = e,
            TransY = f,
            Persp0 = 0f,
            Persp1 = 0f,
            Persp2 = 1f
        })
    {
    }

    internal SvgJavaScriptMatrix(SKMatrix matrix)
    {
        _matrix = matrix;
    }

    internal SvgJavaScriptMatrix(
        SvgJavaScriptRuntime runtime,
        Func<SKMatrix> getter,
        Action<SKMatrix>? setter,
        bool readOnly)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public float a
    {
        get => GetMatrix().ScaleX;
        set => UpdateMatrix(matrix =>
        {
            matrix.ScaleX = value;
            return matrix;
        });
    }

    public float b
    {
        get => GetMatrix().SkewY;
        set => UpdateMatrix(matrix =>
        {
            matrix.SkewY = value;
            return matrix;
        });
    }

    public float c
    {
        get => GetMatrix().SkewX;
        set => UpdateMatrix(matrix =>
        {
            matrix.SkewX = value;
            return matrix;
        });
    }

    public float d
    {
        get => GetMatrix().ScaleY;
        set => UpdateMatrix(matrix =>
        {
            matrix.ScaleY = value;
            return matrix;
        });
    }

    public float e
    {
        get => GetMatrix().TransX;
        set => UpdateMatrix(matrix =>
        {
            matrix.TransX = value;
            return matrix;
        });
    }

    public float f
    {
        get => GetMatrix().TransY;
        set => UpdateMatrix(matrix =>
        {
            matrix.TransY = value;
            return matrix;
        });
    }

    internal SKMatrix ToSkMatrix()
    {
        return GetMatrix();
    }

    private SKMatrix GetMatrix()
    {
        return _getter?.Invoke() ?? _matrix;
    }

    private void UpdateMatrix(Func<SKMatrix, SKMatrix> update)
    {
        if (_readOnly)
        {
            _runtime?.ThrowDomException(7, "This SVGMatrix is read only.");
        }

        var matrix = GetMatrix();
        matrix = update(matrix);
        if (_setter is not null)
        {
            _setter(matrix);
        }
        else
        {
            _matrix = matrix;
        }
    }
}

public sealed class SvgJavaScriptTransform
{
    private readonly SvgJavaScriptRuntime _runtime;
    private SvgTransform _transform;
    private readonly Action<SvgTransform, SvgTransform>? _persist;
    private readonly bool _readOnly;
    private readonly SvgJavaScriptMatrix _matrix;

    internal SvgJavaScriptTransform(
        SvgJavaScriptRuntime runtime,
        SvgTransform transform,
        Action<SvgTransform, SvgTransform>? persist,
        bool readOnly)
    {
        _runtime = runtime;
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
        _persist = persist;
        _readOnly = readOnly;
        _matrix = new SvgJavaScriptMatrix(runtime, GetMatrix, SetMatrixInternal, readOnly);
    }

    public int type => GetTransformType(_transform);

    public float angle => _transform is SvgRotate rotate ? rotate.Angle : 0f;

    public SvgJavaScriptMatrix matrix => _matrix;

    public void setTranslate(float tx, float ty)
    {
        UpdateTransform(new SvgTranslate(tx, ty));
    }

    public void setScale(float sx, float sy)
    {
        UpdateTransform(new SvgScale(sx, sy));
    }

    public void setRotate(float angleInDegrees, float cx, float cy)
    {
        UpdateTransform(new SvgRotate(angleInDegrees, cx, cy));
    }

    public void setMatrix(SvgJavaScriptMatrix matrix)
    {
        if (matrix is null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        UpdateTransform(SvgJavaScriptMatrixHelpers.FromSkMatrix(matrix.ToSkMatrix()));
    }

    internal SvgTransform CloneTransform()
    {
        return (SvgTransform)_transform.Clone();
    }

    private SKMatrix GetMatrix()
    {
        return SvgJavaScriptMatrixHelpers.ToSkMatrix(_transform);
    }

    private void SetMatrixInternal(SKMatrix matrix)
    {
        UpdateTransform(SvgJavaScriptMatrixHelpers.FromSkMatrix(matrix));
    }

    private void UpdateTransform(SvgTransform replacement)
    {
        if (_readOnly)
        {
            _runtime.ThrowDomException(7, "This SVGTransform is read only.");
        }

        var previous = _transform;
        _transform = replacement;
        _persist?.Invoke(previous, replacement);
        _runtime.MarkMutation();
    }

    private static int GetTransformType(SvgTransform transform)
    {
        return transform switch
        {
            SvgMatrix => 1,
            SvgTranslate => 2,
            SvgScale => 3,
            SvgRotate => 4,
            SvgSkew skew when Math.Abs(skew.AngleY) <= float.Epsilon => 5,
            SvgSkew => 6,
            _ => 0
        };
    }
}

public sealed class SvgJavaScriptTransformList
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgJavaScriptElement _element;
    private readonly bool _readOnly;

    internal SvgJavaScriptTransformList(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, bool readOnly)
    {
        _runtime = runtime;
        _element = element;
        _readOnly = readOnly;
    }

    public int numberOfItems => GetCollection().Count;

    public int length => numberOfItems;

    public SvgJavaScriptTransform? getItem(int index)
    {
        var collection = GetCollection();
        if (index < 0 || index >= collection.Count)
        {
            _runtime.ThrowDomException(1, "Transform index is out of range.");
        }

        var transform = collection[index];
        return new SvgJavaScriptTransform(_runtime, transform, PersistTransform(collection), _readOnly);
    }

    public void clear()
    {
        EnsureWritable();
        _element.Element.Transforms = new SvgTransformCollection();
        _runtime.MarkMutation();
    }

    public SvgJavaScriptTransform appendItem(SvgJavaScriptTransform newItem)
    {
        EnsureWritable();
        if (newItem is null)
        {
            throw new ArgumentNullException(nameof(newItem));
        }

        var clone = newItem.CloneTransform();
        var collection = EnsureCollection();
        collection.Add(clone);
        _runtime.MarkMutation();
        return new SvgJavaScriptTransform(_runtime, clone, PersistTransform(collection), false);
    }

    public SvgJavaScriptTransform createSVGTransformFromMatrix(SvgJavaScriptMatrix matrix)
    {
        if (matrix is null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        return new SvgJavaScriptTransform(_runtime, SvgJavaScriptMatrixHelpers.FromSkMatrix(matrix.ToSkMatrix()), null, false);
    }

    public SvgJavaScriptTransform? consolidate()
    {
        EnsureWritable();
        var collection = GetCollection();
        if (collection.Count == 0)
        {
            return null;
        }

        var consolidated = SvgJavaScriptMatrixHelpers.FromSkMatrix(SvgJavaScriptMatrixHelpers.ToSkMatrix(collection));
        var replacement = new SvgTransformCollection { consolidated };
        _element.Element.Transforms = replacement;
        _runtime.MarkMutation();
        return new SvgJavaScriptTransform(_runtime, consolidated, PersistTransform(replacement), false);
    }

    public SvgJavaScriptTransform initialize(SvgJavaScriptTransform newItem)
    {
        EnsureWritable();
        if (newItem is null)
        {
            throw new ArgumentNullException(nameof(newItem));
        }

        var clone = newItem.CloneTransform();
        var replacement = new SvgTransformCollection { clone };
        _element.Element.Transforms = replacement;
        _runtime.MarkMutation();
        return new SvgJavaScriptTransform(_runtime, clone, PersistTransform(replacement), false);
    }

    internal SvgTransformCollection CloneCollection()
    {
        return (SvgTransformCollection)GetCollection().Clone();
    }

    private SvgTransformCollection GetCollection()
    {
        return _element.Element.Transforms ?? new SvgTransformCollection();
    }

    private SvgTransformCollection EnsureCollection()
    {
        if (_element.Element.Transforms is { } transforms)
        {
            return transforms;
        }

        var created = new SvgTransformCollection();
        _element.Element.Transforms = created;
        return created;
    }

    private Action<SvgTransform, SvgTransform> PersistTransform(SvgTransformCollection collection)
    {
        return (previous, replacement) =>
        {
            for (var i = 0; i < collection.Count; i++)
            {
                if (!ReferenceEquals(collection[i], previous))
                {
                    continue;
                }

                collection[i] = replacement;
                return;
            }
        };
    }

    private void EnsureWritable()
    {
        if (_readOnly)
        {
            _runtime.ThrowDomException(7, "This SVGTransformList is read only.");
        }
    }
}

public sealed class SvgJavaScriptPreserveAspectRatio
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly Func<string> _getter;
    private readonly Action<string>? _setter;
    private readonly bool _readOnly;

    internal SvgJavaScriptPreserveAspectRatio(
        SvgJavaScriptRuntime runtime,
        Func<string> getter,
        Action<string>? setter,
        bool readOnly)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public int align
    {
        get => ParseAlign(_getter());
        set
        {
            EnsureWritable();
            _setter?.Invoke(FormatPreserveAspectRatio(value, ParseMeetOrSlice(_getter())));
        }
    }

    public int meetOrSlice
    {
        get => ParseMeetOrSlice(_getter());
        set
        {
            EnsureWritable();
            _setter?.Invoke(FormatPreserveAspectRatio(ParseAlign(_getter()), value));
        }
    }

    private void EnsureWritable()
    {
        if (_readOnly)
        {
            _runtime.ThrowDomException(7, "This SVGPreserveAspectRatio is read only.");
        }
    }

    private static int ParseAlign(string value)
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(value);
        var token = tokens.Length == 0 ? "xMidYMid" : tokens[0];

        return token switch
        {
            "none" => 1,
            "xMinYMin" => 2,
            "xMidYMin" => 3,
            "xMaxYMin" => 4,
            "xMinYMid" => 5,
            "xMidYMid" => 6,
            "xMaxYMid" => 7,
            "xMinYMax" => 8,
            "xMidYMax" => 9,
            "xMaxYMax" => 10,
            _ => 0
        };
    }

    private static int ParseMeetOrSlice(string value)
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(value);
        if (tokens.Length < 2)
        {
            return 1;
        }

        return tokens[1] switch
        {
            "meet" => 1,
            "slice" => 2,
            _ => 0
        };
    }

    private static string FormatAlign(int align)
    {
        return align switch
        {
            1 => "none",
            2 => "xMinYMin",
            3 => "xMidYMin",
            4 => "xMaxYMin",
            5 => "xMinYMid",
            6 => "xMidYMid",
            7 => "xMaxYMid",
            8 => "xMinYMax",
            9 => "xMidYMax",
            10 => "xMaxYMax",
            _ => "xMidYMid"
        };
    }

    private static string FormatMeetOrSlice(int meetOrSlice)
    {
        return meetOrSlice switch
        {
            2 => "slice",
            1 => "meet",
            _ => "meet"
        };
    }

    private static string FormatPreserveAspectRatio(int align, int meetOrSlice)
    {
        var alignToken = FormatAlign(align);
        if (alignToken == "none")
        {
            return alignToken;
        }

        var meetOrSliceToken = FormatMeetOrSlice(meetOrSlice);
        return meetOrSliceToken == "meet"
            ? alignToken
            : string.Concat(alignToken, " ", meetOrSliceToken);
    }
}

public sealed class SvgJavaScriptAnimatedPreserveAspectRatio
{
    private readonly SvgJavaScriptPreserveAspectRatio _baseVal;
    private readonly SvgJavaScriptPreserveAspectRatio _animVal;

    internal SvgJavaScriptAnimatedPreserveAspectRatio(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, string attributeName)
    {
        _baseVal = new SvgJavaScriptPreserveAspectRatio(runtime, () => element.getAttribute(attributeName), value => element.setAttribute(attributeName, value), false);
        _animVal = new SvgJavaScriptPreserveAspectRatio(runtime, () => element.getAttribute(attributeName), null, true);
    }

    public SvgJavaScriptPreserveAspectRatio baseVal => _baseVal;

    public SvgJavaScriptPreserveAspectRatio animVal => _animVal;
}
