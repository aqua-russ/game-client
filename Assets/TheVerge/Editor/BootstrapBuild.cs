using System.IO;
using TheVerge.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheVerge.Editor
{
    public static class BootstrapBuild
    {
        private const string ScenePath = "Assets/TheVerge/Scenes/Bootstrap.unity";
        private const string ScreensLayoutPath = "Assets/TheVerge/UI/Session/LoginScreens.uxml";
        private const string ThemePath = "Assets/TheVerge/UI/TheVergeTheme.tss";

        [MenuItem("The Verge/Prepare Bootstrap Scene")]
        public static void Prepare()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
            PlayerSettings.companyName = "AquaRuss";
            PlayerSettings.productName = "The Verge";

            Directory.CreateDirectory("Assets/TheVerge/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Создаем изометрическую 2D/ортографическую камеру, направленную на точку Минока (2545, 0, -502)
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(2545f, 15f, -512f);
            cameraObject.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.32f, 0.24f);
            camera.orthographic = true;
            camera.orthographicSize = 9f;

            // 2. Создаем направленный свет (Солнце)
            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.0f;

            // 3. Создаем объект GameBootstrap с точкой входа
            var bootstrapObject = new GameObject("GameBootstrap", typeof(GameBootstrap));
            AssignUi(bootstrapObject.GetComponent<GameBootstrap>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BootstrapBuild] 3D-сцена Bootstrap.unity успешно создана!");
        }

        // Ссылки на разметку экранов входа и тему UI Toolkit: по ним GameBootstrap строит интерфейс.
        private static void AssignUi(GameBootstrap bootstrap)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_screensLayout").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ScreensLayoutPath);
            serialized.FindProperty("_theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
