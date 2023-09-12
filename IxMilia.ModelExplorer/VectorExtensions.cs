using System.Numerics;
using Avalonia;

namespace IxMilia.ModelExplorer
{
    public static class VectorExtensions
    {
        public static Point Transform(this Vector3 vector, Matrix4x4 transform)
        {
            var transformed = Vector3.Transform(vector, transform);
            return new Point(transformed.X, transformed.Y);
        }

        public static Vector3 ToVector3(this Point point) => new Vector3((float)point.X, (float)point.Y, 0.0f);
    }
}
