// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Avalonia.Markup.Xaml;

namespace Avalonia.Svg;

internal static class ServiceProviderExtensions
{
    public static T GetService<T>(this IServiceProvider sp)
        => (T)sp?.GetService(typeof(T))!;

    public static Uri GetContextBaseUri(this IServiceProvider ctx)
        => ctx.GetService<IUriContext>().BaseUri;
}
