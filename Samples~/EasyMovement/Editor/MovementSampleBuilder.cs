using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EasyMovement.FirstPerson;

namespace EasyInput.Integrations.Editor
{
    public static class MovementSampleBuilder
    {
        [MenuItem("Tools/EasyInput/Create EasyMovement Scene")]
        public static void Create() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CreateAt("Assets/EasyInput Example/EasyMovement.unity"); }
        public static string CreateAt(string path)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.oreethehat.easy-movement/FirstPerson/Prefabs/FP_Player_Sample.prefab");
            var cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.oreethehat.easy-movement/FirstPerson/Prefabs/FP_MainCamera_Sample.prefab");
            if (!playerPrefab || !cameraPrefab) throw new System.InvalidOperationException("Install EasyMovement 5.0.1 and ECM2 before creating its input example.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool untitled = UnityEngine.SceneManagement.SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path);
            if (untitled && active.isDirty && !Application.isBatchMode) throw new System.InvalidOperationException("Save the current untitled scene before creating the EasyMovement example.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive); UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Ground"; ground.transform.position = new Vector3(0,-.5f,0); ground.transform.localScale = new Vector3(30,1,30);
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45,-25,0);
            var settingsObject = new GameObject("Persistent player settings"); var settings = settingsObject.AddComponent<PlayerInputSettings>();
            var panel = settingsObject.AddComponent<InputSettingsPanel>(); panel.Player = settings;
            PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene); instance.transform.position = Vector3.up;
            var controller = instance.GetComponentInChildren<FirstPersonCharacterController>(true);
            var adapter = controller.gameObject.AddComponent<EasyMovementInput>(); adapter.Player = settings; adapter.Character = controller;
            EditorSceneManager.SaveScene(scene, path); return path;
        }
    }
}
