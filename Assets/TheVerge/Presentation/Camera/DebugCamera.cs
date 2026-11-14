using UnityEngine;
using UnityEngine.InputSystem;

namespace TheVerge.Presentation.Camera
{
    /// <summary>
    /// Отладочная камера (ADR 0015 п. 6: настоящая камера — Стадия 9): смотрит на персонажа сверху под наклоном, север
    /// карты вверху экрана; колесо мыши — приближение. Поворота нет. Ввод — только Input System.
    /// </summary>
    public sealed class DebugCamera
    {
        private const float PitchDegrees = 55f;
        private const float MinDistance = 6f;
        private const float MaxDistance = 60f;
        private const float ZoomStep = 2f;
        private const float FieldOfView = 45f;

        private readonly UnityEngine.Camera _camera;
        private float _distance = 20f;

        public DebugCamera(UnityEngine.Camera camera)
        {
            _camera = camera;
            _camera.orthographic = false;
            _camera.fieldOfView = FieldOfView;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 300f;
        }

        /// <summary>Раз в кадр: зум по колесу и место камеры; <paramref name="target"/> — точка сцены, на которую смотрим.</summary>
        public void Update(Vector3 target)
        {
            var mouse = Mouse.current;
            var scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (scroll != 0f)
            {
                _distance = Mathf.Clamp(_distance - Mathf.Clamp(scroll, -1f, 1f) * ZoomStep, MinDistance, MaxDistance);
            }

            var rotation = Quaternion.Euler(PitchDegrees, 0f, 0f);
            _camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
