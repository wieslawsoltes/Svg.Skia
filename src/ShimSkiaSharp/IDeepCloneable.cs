// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace ShimSkiaSharp;

public interface IDeepCloneable<out T>
{
    T DeepClone();
}
