using System;
using System.IO;
using System.Numerics;
using IxMilia.Stl;

namespace IxMilia.ModelExplorer
{
    public class Model
    {
        public Vector3[] Vertices { get; }
        public Triangle[] Triangles { get; }

        public Model(Vector3[] vertices, Triangle[] triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
        }

        public static Model FromNameAndStream(string name, Stream stream)
        {
            var extension = Path.GetExtension(name).ToLowerInvariant();
            switch (extension)
            {
                case ".stl":
                    {
                        // TODO: cache/reuse vertices
                        var stl = StlFile.Load(stream);
                        var vertices = new Vector3[stl.Triangles.Count * 3];
                        var triangles = new Triangle[stl.Triangles.Count];
                        var vertexIndex = 0;
                        var triangleIndex = 0;
                        foreach (var triangle in stl.Triangles)
                        {
                            var vertexStart = vertexIndex;
                            vertices[vertexIndex++] = ToVector(triangle.Vertex1);
                            vertices[vertexIndex++] = ToVector(triangle.Vertex2);
                            vertices[vertexIndex++] = ToVector(triangle.Vertex3);
                            triangles[triangleIndex++] = new Triangle(vertexStart, vertexStart + 1, vertexStart + 2);
                        }

                        var model = new Model(vertices, triangles);
                        return model;
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private static Vector3 ToVector(StlVertex v) => new Vector3(v.X, v.Y, v.Z);
    }
}
