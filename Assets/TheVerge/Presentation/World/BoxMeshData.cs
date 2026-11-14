using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Набор прямоугольных блоков в одну сетку: верх и четыре боковые стороны с плоскими нормалями (низ не виден).
    /// Лицевая сторона — по часовой стрелке, как у Unity. Индексы 32-битные: блоков в обзоре тысячи.
    /// </summary>
    internal sealed class BoxMeshData
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();

        public int BoxCount { get; private set; }

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _triangles.Clear();
            BoxCount = 0;
        }

        public void AddBox(Vector3 min, Vector3 size)
        {
            var max = min + size;
            Quad(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up);
            Quad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.forward);
            Quad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.back);
            Quad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.right);
            Quad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.left);
            BoxCount++;
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            if (_vertices.Count == 0)
            {
                return;
            }

            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
        }

        // Углы по часовой стрелке при взгляде на лицевую сторону: слева-снизу, слева-сверху, справа-сверху, справа-снизу.
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            var first = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _vertices.Add(d);
            for (var i = 0; i < 4; i++)
            {
                _normals.Add(normal);
            }

            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
        }
    }
}
