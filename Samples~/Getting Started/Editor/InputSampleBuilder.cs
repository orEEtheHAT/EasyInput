using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EasyInput.Samples.Editor
{
    public static class InputSampleBuilder
    {
        [MenuItem("Tools/EasyInput/Create Getting Started Scene")]
        public static void Create() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CreateAt("Assets/EasyInput Example/GettingStarted.unity"); }
        public static string CreateAt(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool untitled = UnityEngine.SceneManagement.SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path);
            if (untitled && active.isDirty && !Application.isBatchMode) throw new System.InvalidOperationException("Save the current untitled scene before creating the EasyInput example.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("Camera"); var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera"; cameraObject.transform.position = new Vector3(0, 2, -7); cameraObject.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .08f, .12f);
            var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40, -30, 0);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Look preview (not a player controller)"; cube.transform.rotation = Quaternion.Euler(15, 30, 0);
            var root = new GameObject("EasyInput player"); root.SetActive(false);
            var player = root.AddComponent<PlayerInputSettings>();
            var panel = root.AddComponent<InputSettingsPanel>(); panel.Player = player; panel.OpenOnStart = true;
            var prompt = root.AddComponent<InputPrompt>(); prompt.Player = player;
            var showcase = root.AddComponent<InputShowcase>(); showcase.Player = player; showcase.Preview = cube.transform; showcase.Prompt = prompt;
            root.SetActive(true); EditorSceneManager.SaveScene(scene, path); AssetDatabase.Refresh();
            return path;
        }
    }
}
