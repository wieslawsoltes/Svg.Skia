using System;
using System.Collections.Generic;
using System.Diagnostics;
using Svg.Model.Paint;
using Svg.Model.Path;
using Svg.Model.Picture.Commands;
using Svg.Model.Primitives;

namespace Svg.Model.Picture
{
    public class Canvas : IDisposable
    {
        private int _saveCount = 0;
        private readonly Stack<Matrix> _totalMatrices = new Stack<Matrix>();

        public IList<CanvasCommand>? Commands { get; set; }
        public Matrix TotalMatrix { get; set; }

        public Canvas()
        {
            Commands = new List<CanvasCommand>();
            TotalMatrix = Matrix.Identity;
        }

        public void ClipPath(ClipPath clipPath, ClipOperation operation = ClipOperation.Intersect, bool antialias = false)
        {
            Commands?.Add(new ClipPathCanvasCommand(clipPath, operation, antialias));
        }

        public void ClipRect(Rect rect, ClipOperation operation = ClipOperation.Intersect, bool antialias = false)
        {
            Commands?.Add(new ClipRectCanvasCommand(rect, operation, antialias));
        }

        public void DrawImage(Image image, Rect source, Rect dest, Paint.Paint? paint = null)
        {
            Commands?.Add(new DrawImageCanvasCommand(image, source, dest, paint));
        }

        public void DrawPath(Path.Path path, Paint.Paint paint)
        {
            Commands?.Add(new DrawPathCanvasCommand(path, paint));
        }

        public void DrawText(TextBlob textBlob, float x, float y, Paint.Paint paint)
        {
            Commands?.Add(new DrawTextBlobCanvasCommand(textBlob, x, y, paint));
        }

        public void DrawText(string text, float x, float y, Paint.Paint paint)
        {
            Commands?.Add(new DrawTextCanvasCommand(text, x, y, paint));
        }

        public void DrawTextOnPath(string text, Path.Path path, float hOffset, float vOffset, Paint.Paint paint)
        {
            Commands?.Add(new DrawTextOnPathCanvasCommand(text, path, hOffset, vOffset, paint));
        }

        public void SetMatrix(Matrix matrix)
        {
            TotalMatrix = matrix;
            Commands?.Add(new SetMatrixCanvasCommand(matrix));
        }

        public int Save()
        {
            _totalMatrices.Push(TotalMatrix);
            Commands?.Add(new SaveCanvasCommand(_saveCount));
            _saveCount++;
            return _saveCount;
        }

        public int SaveLayer(Paint.Paint paint)
        {
            _totalMatrices.Push(TotalMatrix);
            Commands?.Add(new SaveLayerCanvasCommand(_saveCount, paint));
            _saveCount++;
            return _saveCount;
        }

        public void Restore()
        {
            if (_totalMatrices.Count == 0)
            {
                Debug.WriteLine($"Invalid Save and Restore balance.");
            }
            else
            {
                TotalMatrix = _totalMatrices.Pop();
                _saveCount--;
            }
            Commands?.Add(new RestoreCanvasCommand(_saveCount));
        }

        public void Dispose()
        {
        }
    }
}
