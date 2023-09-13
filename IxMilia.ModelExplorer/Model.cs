using System;
using System.Collections.Generic;
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
                        // cache/reuse vertices
                        var stl = StlFile.Load(stream);
                        var vertices = new List<Vector3>();
                        var vertexToIndex = new Dictionary<Vector3, int>();
                        var triangles = new List<Triangle>();
                        foreach (var triangle in stl.Triangles)
                        {
                            foreach (var stlVertex in new[] { triangle.Vertex1, triangle.Vertex2, triangle.Vertex3 })
                            {
                                var vertex = stlVertex.ToVector3();
                                if (!vertexToIndex.ContainsKey(vertex))
                                {
                                    vertexToIndex.Add(vertex, vertices.Count);
                                    vertices.Add(vertex);
                                }
                            }

                            var createdTriangle = new Triangle(
                                vertexToIndex[triangle.Vertex1.ToVector3()],
                                vertexToIndex[triangle.Vertex2.ToVector3()],
                                vertexToIndex[triangle.Vertex3.ToVector3()]);
                            triangles.Add(createdTriangle);
                        }

                        var model = new Model(vertices.ToArray(), triangles.ToArray());
                        return model;
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
