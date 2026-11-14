using System;
using TheVerge.Core.MapView;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Простые материалы отладочной карты: по цвету на класс клетки, на свой персонаж, чужих мобов и предметы. Освещение —
    /// от света сцены, шейдер по виду конвейера (в проекте сейчас встроенный; если подключат URP — его Lit).
    /// Графика не важна до Стадии 9 (AGENTS.md §4.4).
    /// </summary>
    public sealed class DebugMaterials : IDisposable
    {
        public const int ClassCount = (int)CellClass.Water + 1;

        private readonly Material[] _classes = new Material[ClassCount];
        private readonly Material _player;
        private readonly Material _mobile;
        private readonly Material _item;

        public DebugMaterials()
        {
            var shader = FindShader();
            _classes[(int)CellClass.Other] = Create(shader, new Color(0.80f, 0.80f, 0.78f));
            _classes[(int)CellClass.Surface] = Create(shader, new Color(0.35f, 0.65f, 0.30f));
            _classes[(int)CellClass.Blocked] = Create(shader, new Color(0.85f, 0.25f, 0.20f));
            _classes[(int)CellClass.Wall] = Create(shader, new Color(0.35f, 0.35f, 0.40f));
            _classes[(int)CellClass.Water] = Create(shader, new Color(0.20f, 0.45f, 0.85f));
            _player = Create(shader, new Color(0.10f, 0.90f, 0.90f));
            _mobile = Create(shader, new Color(0.95f, 0.60f, 0.10f));
            _item = Create(shader, new Color(0.95f, 0.90f, 0.40f));
        }

        public Material ForClass(CellClass cellClass) => _classes[(int)cellClass];

        public Material Player => _player;

        public Material Mobile => _mobile;

        public Material Item => _item;

        public void Dispose()
        {
            foreach (var material in _classes)
            {
                UnityEngine.Object.Destroy(material);
            }

            UnityEngine.Object.Destroy(_player);
            UnityEngine.Object.Destroy(_mobile);
            UnityEngine.Object.Destroy(_item);
        }

        private static Shader FindShader()
        {
            var name = GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard";
            var shader = Shader.Find(name);
            return shader != null ? shader : Shader.Find("Sprites/Default");
        }

        private static Material Create(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.color = color;
            material.SetFloat("_Glossiness", 0f);
            material.SetFloat("_Smoothness", 0f);
            return material;
        }
    }
}
