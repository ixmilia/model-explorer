using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using IxMilia.Stl;
using IxMilia.ThreeMf;

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
                case ".3mf":
                    {
                        // cache/reuse vertices
                        var file = ThreeMfFile.Load(stream);
                        var vertices = new List<Vector3>();
                        var vertexToIndex = new Dictionary<Vector3, int>();
                        var triangles = new List<Triangle>();
                        foreach (var model in file.Models)
                        {
                            foreach (var modelItem in model.Items)
                            {
                                if (modelItem.Object is ThreeMfObject obj)
                                {
                                    foreach (var triangle in obj.Mesh.Triangles)
                                    {
                                        foreach (var threeMfVertex in new[] { triangle.V1, triangle.V2, triangle.V3 })
                                        {
                                            var vertex = threeMfVertex.ToVector3();
                                            if (!vertexToIndex.ContainsKey(vertex))
                                            {
                                                vertexToIndex.Add(vertex, vertices.Count);
                                                vertices.Add(vertex);
                                            }
                                        }

                                        var createdTriangle = new Triangle(
                                            vertexToIndex[triangle.V1.ToVector3()],
                                            vertexToIndex[triangle.V2.ToVector3()],
                                            vertexToIndex[triangle.V3.ToVector3()]);
                                        triangles.Add(createdTriangle);
                                    }
                                }
                            }
                        }

                        var m = new Model(vertices.ToArray(), triangles.ToArray());
                        return m;
                    }
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
