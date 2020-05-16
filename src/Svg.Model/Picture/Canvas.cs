using System;
using System.Collections.Generic;

namespace Svg.Model
{
    public class Canvas : IDisposable
    {
        private int _count = 0;
        private Matrix _oldTotalMatrix;

        public IList<PictureCommand>? Commands;
        public Matrix TotalMatrix; // TODO:

        public Canvas()
        {
            Commands = new List<PictureCommand>();
            TotalMatrix = Matrix.Identity;
        }

        public void ClipPath(Path path, ClipOperation operation = ClipOperation.Intersect, bool antialias = false)
        {
            Commands?.Add(new ClipPathPictureCommand(path, operation, antialias));
        }

        public void ClipRect(Rect rect, ClipOperation operation = ClipOperation.Intersect, bool antialias = false)
        {
            Commands?.Add(new ClipRectPictureCommand(rect, operation, antialias));
        }

        public void DrawImage(Image image, Rect source, Rect dest, Paint? paint = null)
        {
            Commands?.Add(new DrawImagePictureCommand(image, source, dest, paint));
        }

        public void DrawPath(Path path, Paint paint)
        {
            Commands?.Add(new DrawPathPictureCommand(path, paint));
        }

        public void DrawPositionedText(string text, Point[] points, Paint paint)
        {
            Commands?.Add(new DrawPositionedTextPictureCommand(text, points, paint));
        }

        public void DrawText(string text, float x, float y, Paint paint)
        {
            Commands?.Add(new DrawTextPictureCommand(text, x, y, paint));
        }

        public void DrawTextOnPath(string text, Path path, float hOffset, float vOffset, Paint paint)
        {
            Commands?.Add(new DrawTextOnPathPictureCommand(text, path, hOffset, vOffset, paint));
        }

        public void SetMatrix(Matrix matrix)
        {
            TotalMatrix = matrix;
            // TODO: Update TotalMatrix.
            Commands?.Add(new SetMatrixPictureCommand(matrix));
        }

        public int Save()
        {
            _oldTotalMatrix = TotalMatrix;
            // TODO: Save TotalMatrix.
            Commands?.Add(new SavePictureCommand());
            _count++;
            return _count;
        }

        public int SaveLayer(Paint paint)
        {
            _oldTotalMatrix = TotalMatrix;
            // TODO: Save TotalMatrix.
            Commands?.Add(new SaveLayerPictureCommand(paint));
            _count++;
            return _count;
        }

        public void Restore()
        {
            TotalMatrix = _oldTotalMatrix;
            // TODO: Restore TotalMatrix.
            Commands?.Add(new RestorePictureCommand());
            _count = 0;
        }

        public void Dispose()
        {
        }
    }
}
