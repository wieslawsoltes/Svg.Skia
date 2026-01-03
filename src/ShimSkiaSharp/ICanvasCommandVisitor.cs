// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public interface ICanvasCommandVisitor
{
    void Visit(ClipPathCanvasCommand cmd);
    void Visit(ClipRectCanvasCommand cmd);
    void Visit(DrawImageCanvasCommand cmd);
    void Visit(DrawPathCanvasCommand cmd);
    void Visit(DrawTextBlobCanvasCommand cmd);
    void Visit(DrawTextCanvasCommand cmd);
    void Visit(DrawTextOnPathCanvasCommand cmd);
    void Visit(RestoreCanvasCommand cmd);
    void Visit(SaveCanvasCommand cmd);
    void Visit(SaveLayerCanvasCommand cmd);
    void Visit(SetMatrixCanvasCommand cmd);
}
