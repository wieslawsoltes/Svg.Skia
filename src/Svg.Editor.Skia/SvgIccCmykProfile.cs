using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Managed ICC output transform for CMYK printer profiles. Implements ICC.1 LUT8, LUT16
/// and lutBToAType pipelines, including sampled/parametric curves and tetrahedral CLUT
/// interpolation. Source colors are sRGB, adapted to the D50 profile connection space.
/// </summary>
/// <remarks>ICC.1:2022, sections 6.3, 10.6, 10.10, 10.11, 10.13 and 10.18.</remarks>
public sealed class SvgIccCmykProfile
{
    private readonly byte[] _bytes;
    private readonly Curve[] _input = { Curve.Identity, Curve.Identity, Curve.Identity };
    private readonly Curve[] _middle = { Curve.Identity, Curve.Identity, Curve.Identity };
    private readonly Curve[] _output = { Curve.Identity, Curve.Identity, Curve.Identity, Curve.Identity };
    private readonly double[] _matrix = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
    private readonly int[] _grid = new int[3];
    private float[] _clut = Array.Empty<float>();
    private bool _legacyLab16;
    private bool _matrixFirst;
    private readonly bool _lab;

    public string Description { get; }
    public string TransformTag { get; }
    public string TransformType { get; }
    public byte[] GetProfileBytes() => (byte[])_bytes.Clone();

    /// <param name="renderingIntent">0 perceptual, 1 relative colorimetric, or 2 saturation.</param>
    public SvgIccCmykProfile(byte[] profile, int renderingIntent = 1)
    {
        if (profile is null)
            throw new ArgumentNullException(nameof(profile));
        if (profile.Length < 132 || profile.Length > 32 * 1024 * 1024)
            throw new InvalidDataException("An ICC profile must be between 132 bytes and 32 MiB.");
        _bytes = (byte[])profile.Clone();
        if (U32(0) != _bytes.Length || Signature(36) != "acsp" || Signature(16) != "CMYK" || Signature(12) != "prtr")
            throw new InvalidDataException("Choose a valid CMYK printer ICC profile.");
        _lab = Signature(20) == "Lab ";
        if (!_lab && Signature(20) != "XYZ ")
            throw new InvalidDataException("The output profile must use the Lab or XYZ connection space.");
        if (renderingIntent < 0 || renderingIntent > 2)
            throw new ArgumentOutOfRangeException(nameof(renderingIntent));
        var count = CheckedSize(U32(128), 4096);
        Require(132, checked(count * 12));
        var tags = new Dictionary<string, (int Offset, int Length)>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var at = 132 + i * 12;
            var offset = CheckedSize(U32(at + 4), _bytes.Length);
            var length = CheckedSize(U32(at + 8), _bytes.Length);
            Require(offset, length);
            var signature = Signature(at);
            if (offset < 132 + count * 12 || length < 8 || tags.ContainsKey(signature))
                throw new InvalidDataException("The ICC tag table is malformed.");
            tags.Add(signature, (offset, length));
        }
        var requested = "B2A" + renderingIntent;
        TransformTag = tags.ContainsKey(requested) ? requested : "B2A0";
        if (!tags.TryGetValue(TransformTag, out var transform))
            throw new InvalidDataException("The profile has no PCS-to-CMYK output transform.");
        TransformType = Signature(transform.Offset);
        Description = tags.TryGetValue("desc", out var description) ? ReadDescription(description.Offset, description.Length) : "CMYK output profile";
        if (TransformType is "mft1" or "mft2")
            ReadLegacy(transform.Offset, transform.Length, TransformType == "mft2");
        else if (TransformType == "mBA ")
            ReadBToA(transform.Offset, transform.Length);
        else
            throw new NotSupportedException($"ICC transform '{TransformType}' is unsupported. Use an ICC.1 LUT8, LUT16 or B-to-A output profile.");
    }

    public SvgCmykColor Convert(SKColor color)
    {
        var alpha = color.Alpha / 255.0;
        static double Linear(double channel) => channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        var r = Linear(color.Red / 255.0 * alpha + 1 - alpha);
        var g = Linear(color.Green / 255.0 * alpha + 1 - alpha);
        var b = Linear(color.Blue / 255.0 * alpha + 1 - alpha);
        var x = 0.4360747 * r + 0.3850649 * g + 0.1430804 * b;
        var y = 0.2225045 * r + 0.7168786 * g + 0.0606169 * b;
        var z = 0.0139322 * r + 0.0971045 * g + 0.7141733 * b;
        if (_lab)
        {
            static double Lab(double t) => t > 216.0 / 24389 ? Math.Pow(t, 1.0 / 3) : (24389.0 / 27 * t + 16) / 116;
            var fx = Lab(x / 0.96422);
            var fy = Lab(y);
            var fz = Lab(z / 0.82521);
            x = (116 * fy - 16) / 100;
            y = (500 * (fx - fy) + 128) / 255;
            z = (200 * (fy - fz) + 128) / 255;
            if (_legacyLab16)
            { x *= 65280.0 / 65535; y *= 65280.0 / 65535; z *= 65280.0 / 65535; }
        }
        else
        { x *= 32768.0 / 65535; y *= 32768.0 / 65535; z *= 32768.0 / 65535; }
        if (_matrixFirst)
            ApplyMatrix(ref x, ref y, ref z);
        x = _input[0].Evaluate(x);
        y = _input[1].Evaluate(y);
        z = _input[2].Evaluate(z);
        if (!_matrixFirst)
            ApplyMatrix(ref x, ref y, ref z);
        x = _middle[0].Evaluate(x);
        y = _middle[1].Evaluate(y);
        z = _middle[2].Evaluate(z);
        return new SvgCmykColor(_output[0].Evaluate(Sample(x, y, z, 0)), _output[1].Evaluate(Sample(x, y, z, 1)),
            _output[2].Evaluate(Sample(x, y, z, 2)), _output[3].Evaluate(Sample(x, y, z, 3)));
    }

    private void ApplyMatrix(ref double x, ref double y, ref double z)
    {
        var a = x;
        var b = y;
        var c = z;
        x = SvgCmykColor.Clamp(_matrix[0] * a + _matrix[1] * b + _matrix[2] * c + _matrix[9]);
        y = SvgCmykColor.Clamp(_matrix[3] * a + _matrix[4] * b + _matrix[5] * c + _matrix[10]);
        z = SvgCmykColor.Clamp(_matrix[6] * a + _matrix[7] * b + _matrix[8] * c + _matrix[11]);
    }

    private double Sample(double x, double y, double z, int channel)
    {
        x = SvgCmykColor.Clamp(x) * (_grid[0] - 1);
        y = SvgCmykColor.Clamp(y) * (_grid[1] - 1);
        z = SvgCmykColor.Clamp(z) * (_grid[2] - 1);
        var ix = Math.Min((int)x, _grid[0] - 2);
        var iy = Math.Min((int)y, _grid[1] - 2);
        var iz = Math.Min((int)z, _grid[2] - 2);
        var dx = x - ix;
        var dy = y - iy;
        var dz = z - iz;
        double At(int a, int b, int c) => _clut[(((ix + a) * _grid[1] + iy + b) * _grid[2] + iz + c) * 4 + channel];
        var c0 = At(0, 0, 0);
        var c1 = At(1, 1, 1);
        if (dx >= dy)
        {
            if (dy >= dz)
                return c0 + dx * (At(1, 0, 0) - c0) + dy * (At(1, 1, 0) - At(1, 0, 0)) + dz * (c1 - At(1, 1, 0));
            if (dx >= dz)
                return c0 + dx * (At(1, 0, 0) - c0) + dz * (At(1, 0, 1) - At(1, 0, 0)) + dy * (c1 - At(1, 0, 1));
            return c0 + dz * (At(0, 0, 1) - c0) + dx * (At(1, 0, 1) - At(0, 0, 1)) + dy * (c1 - At(1, 0, 1));
        }
        if (dx >= dz)
            return c0 + dy * (At(0, 1, 0) - c0) + dx * (At(1, 1, 0) - At(0, 1, 0)) + dz * (c1 - At(1, 1, 0));
        if (dy >= dz)
            return c0 + dy * (At(0, 1, 0) - c0) + dz * (At(0, 1, 1) - At(0, 1, 0)) + dx * (c1 - At(0, 1, 1));
        return c0 + dz * (At(0, 0, 1) - c0) + dy * (At(0, 1, 1) - At(0, 0, 1)) + dx * (c1 - At(0, 1, 1));
    }

    private void ReadLegacy(int start, int length, bool sixteenBit)
    {
        var header = sixteenBit ? 52 : 48;
        RequireTag(start, length, start, header);
        if (_bytes[start + 8] != 3 || _bytes[start + 9] != 4)
            throw new InvalidDataException("The printer transform must map three PCS channels to four CMYK channels.");
        for (var i = 0; i < 3; i++)
            _grid[i] = _bytes[start + 10];
        for (var i = 0; i < 9; i++)
            _matrix[i] = Fixed(start + 12 + i * 4);
        if (_lab && _matrix.Where((value, i) => i < 9 && Math.Abs(value - (i % 4 == 0 ? 1 : 0)) > 1e-6).Any())
            throw new InvalidDataException("A Lab LUT profile must use an identity input matrix.");
        _matrixFirst = true;
        _legacyLab16 = sixteenBit;
        var inputCount = sixteenBit ? U16(start + 48) : 256;
        var outputCount = sixteenBit ? U16(start + 50) : 256;
        if (inputCount < 2 || outputCount < 2)
            throw new InvalidDataException("ICC curves require at least two samples.");
        var at = start + header;
        for (var i = 0; i < 3; i++)
            _input[i] = ReadTable(ref at, inputCount, sixteenBit, start, length);
        ReadClut(ref at, sixteenBit, start, length);
        for (var i = 0; i < 4; i++)
            _output[i] = ReadTable(ref at, outputCount, sixteenBit, start, length);
    }

    private void ReadBToA(int start, int length)
    {
        RequireTag(start, length, start, 32);
        if (_bytes[start + 8] != 3 || _bytes[start + 9] != 4)
            throw new InvalidDataException("The printer transform must map three PCS channels to four CMYK channels.");
        int Offset(int at) => U32(start + at) == 0 ? 0 : checked(start + CheckedSize(U32(start + at), length));
        ReadCurves(Offset(12), _input, start, length);
        var matrix = Offset(16);
        if (matrix != 0)
        {
            RequireTag(start, length, matrix, 48);
            for (var i = 0; i < 12; i++)
                _matrix[i] = Fixed(matrix + i * 4);
        }
        ReadCurves(Offset(20), _middle, start, length);
        var clut = Offset(24);
        if (clut == 0)
            throw new InvalidDataException("A three-to-four channel transform requires a CLUT.");
        RequireTag(start, length, clut, 20);
        for (var i = 0; i < 3; i++)
            _grid[i] = _bytes[clut + i];
        var precision = _bytes[clut + 16];
        if (precision is not (1 or 2))
            throw new InvalidDataException("ICC CLUT samples must be 8 or 16 bits.");
        var at = clut + 20;
        ReadClut(ref at, precision == 2, start, length);
        ReadCurves(Offset(28), _output, start, length);
    }

    private void ReadClut(ref int at, bool sixteenBit, int start, int length)
    {
        if (_grid.Any(value => value < 2 || value > 65))
            throw new InvalidDataException("ICC CLUT grid sizes must be between 2 and 65.");
        var count = checked(_grid[0] * _grid[1] * _grid[2] * 4);
        RequireTag(start, length, at, checked(count * (sixteenBit ? 2 : 1)));
        _clut = new float[count];
        for (var i = 0; i < count; i++, at += sixteenBit ? 2 : 1)
            _clut[i] = sixteenBit ? U16(at) / 65535f : _bytes[at] / 255f;
    }

    private Curve ReadTable(ref int at, int count, bool sixteenBit, int start, int length)
    {
        RequireTag(start, length, at, checked(count * (sixteenBit ? 2 : 1)));
        var samples = new double[count];
        for (var i = 0; i < count; i++, at += sixteenBit ? 2 : 1)
            samples[i] = sixteenBit ? U16(at) / 65535.0 : _bytes[at] / 255.0;
        return new Curve(samples);
    }

    private void ReadCurves(int at, Curve[] curves, int start, int length)
    {
        if (at == 0)
            return;
        for (var i = 0; i < curves.Length; i++)
        {
            RequireTag(start, length, at, 12);
            var signature = Signature(at);
            if (signature == "curv")
            {
                var count = CheckedSize(U32(at + 8), 65536);
                at += 12;
                if (count == 0)
                    curves[i] = Curve.Identity;
                else if (count == 1)
                {
                    RequireTag(start, length, at, 2);
                    var gamma = U16(at) / 256.0;
                    if (gamma <= 0)
                        throw new InvalidDataException("ICC curve gamma must be positive.");
                    curves[i] = new Curve(0, new[] { gamma });
                    at += 2;
                }
                else
                    curves[i] = ReadTable(ref at, count, true, start, length);
            }
            else if (signature == "para")
            {
                var type = U16(at + 8);
                var count = type switch { 0 => 1, 1 => 3, 2 => 4, 3 => 5, 4 => 7, _ => throw new InvalidDataException("Unsupported ICC curve function.") };
                RequireTag(start, length, at + 12, count * 4);
                var parameters = new double[count];
                for (var j = 0; j < count; j++)
                    parameters[j] = Fixed(at + 12 + j * 4);
                if (parameters[0] <= 0 || count > 1 && parameters[1] <= 0)
                    throw new InvalidDataException("ICC curve gamma and slope must be positive.");
                curves[i] = new Curve(type, parameters);
                at += 12 + count * 4;
            }
            else
                throw new InvalidDataException("Expected an ICC sampled or parametric curve.");
            at = checked((at + 3) & ~3);
        }
    }

    private string ReadDescription(int start, int length)
    {
        var type = Signature(start);
        if (type == "desc" && length >= 12)
        {
            var count = CheckedSize(U32(start + 8), length - 12);
            return Encoding.ASCII.GetString(_bytes, start + 12, count).TrimEnd('\0');
        }
        if (type == "mluc" && length >= 28 && U32(start + 8) > 0 && U32(start + 12) >= 12)
        {
            var count = CheckedSize(U32(start + 20), length);
            var offset = CheckedSize(U32(start + 24), length);
            RequireTag(start, length, start + offset, count);
            return Encoding.BigEndianUnicode.GetString(_bytes, start + offset, count);
        }
        return "CMYK output profile";
    }

    private string Signature(int at) { Require(at, 4); return Encoding.ASCII.GetString(_bytes, at, 4); }
    private uint U32(int at) { Require(at, 4); return (uint)(_bytes[at] << 24 | _bytes[at + 1] << 16 | _bytes[at + 2] << 8 | _bytes[at + 3]); }
    private int U16(int at) { Require(at, 2); return _bytes[at] << 8 | _bytes[at + 1]; }
    private double Fixed(int at) => unchecked((int)U32(at)) / 65536.0;
    private static int CheckedSize(uint value, int maximum) => value <= maximum ? (int)value : throw new InvalidDataException("ICC data exceeds its declared bounds.");
    private void Require(int at, int length)
    {
        if (at < 0 || length < 0 || (long)at + length > _bytes.Length)
            throw new InvalidDataException("Truncated ICC profile.");
    }
    private void RequireTag(int start, int length, int at, int count)
    {
        Require(at, count);
        if (at < start || (long)at + count > (long)start + length)
            throw new InvalidDataException("An ICC processing element exceeds its tag.");
    }

    private sealed class Curve
    {
        public static readonly Curve Identity = new(0, new[] { 1.0 });
        private readonly double[] _values;
        private readonly int _type;
        public Curve(double[] samples) { _values = samples; _type = -1; }
        public Curve(int type, double[] parameters) { _type = type; _values = parameters; }
        public double Evaluate(double input)
        {
            var x = SvgCmykColor.Clamp(input);
            if (_type == -1)
            {
                var position = x * (_values.Length - 1);
                var index = Math.Min((int)position, _values.Length - 2);
                return _values[index] + (position - index) * (_values[index + 1] - _values[index]);
            }
            var p = _values;
            var result = _type switch
            {
                0 => Math.Pow(x, p[0]),
                1 => x >= -p[2] / p[1] ? Math.Pow(Math.Max(0, p[1] * x + p[2]), p[0]) : 0,
                2 => x >= -p[2] / p[1] ? Math.Pow(Math.Max(0, p[1] * x + p[2]), p[0]) + p[3] : p[3],
                3 => x >= p[4] ? Math.Pow(Math.Max(0, p[1] * x + p[2]), p[0]) : p[3] * x,
                4 => x >= p[4] ? Math.Pow(Math.Max(0, p[1] * x + p[2]), p[0]) + p[5] : p[3] * x + p[6],
                _ => x
            };
            return SvgCmykColor.Clamp(result);
        }
    }
}
