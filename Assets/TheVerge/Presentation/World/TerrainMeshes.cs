using System;
using System.Collections.Generic;
using TheVerge.Core.MapView;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Карта вокруг персонажа: пять сеток — по одной на цвет (<see cref="CellClass"/>), каждая со своим материалом.
    /// Пересобирается целиком, когда <see cref="SceneTracker"/> скажет (редко: шаг, смена карты, дом).
    /// </summary>
    public sealed class TerrainMeshes : IDisposable
    {
        private readonly BoxMeshData[] _data = new BoxMeshData[DebugMaterials.ClassCount];
        private readonly Mesh[] _meshes = new Mesh[DebugMaterials.ClassCount];
        private readonly GameObject[] _objects = new GameObject[DebugMaterials.ClassCount];

        public TerrainMeshes(Transform parent, DebugMaterials materials)
        {
            for (var i = 0; i < DebugMaterials.ClassCount; i++)
            {
                _data[i] = new BoxMeshData();
                _meshes[i] = new Mesh { name = "Terrain-" + (CellClass)i };
                _objects[i] = new GameObject("Terrain-" + (CellClass)i, typeof(MeshFilter), typeof(MeshRenderer));
                _objects[i].transform.SetParent(parent, false);
                _objects[i].GetComponent<MeshFilter>().sharedMesh = _meshes[i];
                _objects[i].GetComponent<MeshRenderer>().sharedMaterial = materials.ForClass((CellClass)i);
            }
        }

        public void Rebuild(List<SceneBlock> blocks)
        {
            foreach (var data in _data)
            {
                data.Clear();
            }

            foreach (var block in blocks)
            {
                var size = new Vector3(CellSpace.CellSize, CellSpace.Height(block.ZTop - block.ZBottom), CellSpace.CellSize);
                _data[(int)block.Class].AddBox(CellSpace.CellMin(block.X, block.Y, block.ZBottom), size);
            }

            for (var i = 0; i < _data.Length; i++)
            {
                _data[i].ApplyTo(_meshes[i]);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _data.Length; i++)
            {
                _data[i].Clear();
                _data[i].ApplyTo(_meshes[i]);
            }
        }

        public void Dispose()
        {
            foreach (var mesh in _meshes)
            {
                UnityEngine.Object.Destroy(mesh);
            }

            foreach (var gameObject in _objects)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
