using System.Numerics;
using Avalonia;
using IxMilia.Stl;

namespace IxMilia.ModelExplorer
{
    public static class VectorExtensions
    {
        public static Point Transform(this Vector3 vector, Matrix4x4 transform)
        {
            var transformed = Vector3.Transform(vector, transform);
            return transformed.ToPoint();
        }

        public static Point ToPoint(this Vector3 vector) => new Point(vector.X, vector.Y);

        public static Vector3 ToVector3(this Point point) => new Vector3((float)point.X, (float)point.Y, 0.0f);

        public static Vector3 ToVector3(this StlVertex vertex) => new Vector3(vertex.X, vertex.Y, vertex.Z);
    }
}
