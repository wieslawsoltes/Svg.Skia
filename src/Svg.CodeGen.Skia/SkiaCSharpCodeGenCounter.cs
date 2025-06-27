// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace Svg.CodeGen.Skia;

public class SkiaCSharpCodeGenCounter
{
    public int Picture { get; set; } = -1;

    public int PictureRecorder { get; set; } = -1;

    public int Canvas { get; set; } = -1;

    public int Paint { get; set; } = -1;

    public int Typeface { get; set; } = -1;

    public int TextBlob { get; set; } = -1;

    public int Font { get; set; } = -1;

    public int ColorFilter { get; set; } = -1;

    public int ImageFilter { get; set; } = -1;

    public int PathEffect { get; set; } = -1;

    public int Shader { get; set; } = -1;

    public int Path { get; set; } = -1;

    public int Image { get; set; } = -1;

    public string PictureVarName { get; set; } = "skPicture";

    public string PictureRecorderVarName { get; set; } = "skPictureRecorder";

    public string CanvasVarName { get; set; } = "skCanvas";

    public string PaintVarName { get; set; } = "skPaint";

    public string TypefaceVarName { get; set; } = "skTypeface";

    public string TextBlobVarName { get; set; } = "skTextBlob";

    public string FontVarName { get; set; } = "skFont";

    public string ColorFilterVarName { get; set; } = "skColorFilter";

    public string ImageFilterVarName { get; set; } = "skImageFilter";

    public string PathEffectVarName { get; set; } = "skPathEffect";

    public string ShaderVarName { get; set; } = "skShader";

    public string PathVarName { get; set; } = "skPath";

    public string ImageVarName { get; set; } = "skImage";

    public string FontManagerVarName { get; set; } = "skFontManager";

    public string FontStyleVarName { get; set; } = "skFontStyle";

    public string FontStyleSetVarName { get; set; } = "skFontStyleSet";
}
