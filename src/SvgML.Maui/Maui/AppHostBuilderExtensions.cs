using System;
using Microsoft.Maui.Hosting;

namespace SvgML;

public static class AppHostBuilderExtensions
{
    public static MauiAppBuilder UseSvgML(this MauiAppBuilder builder)
    {
        GC.KeepAlive(typeof(svg));
        GC.KeepAlive(typeof(defs));
        GC.KeepAlive(typeof(linearGradient));
        GC.KeepAlive(typeof(stop));
        GC.KeepAlive(typeof(rect));
        GC.KeepAlive(typeof(circle));

        // TODO: Add all types from SvgML.Maui into UseSvgML() extension method
        
        return builder;
    }
}
