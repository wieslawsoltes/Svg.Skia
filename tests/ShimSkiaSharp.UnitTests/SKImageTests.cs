using Xunit;
using ShimSkiaSharp;
using System.IO;

namespace ShimSkiaSharp.UnitTests;

public class SKImageTests
{
    [Fact]
    public void FromStream_ReadsAllBytes()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var ms = new MemoryStream(data);
        var result = SKImage.FromStream(ms);
        Assert.Equal(data, result);
    }
}
