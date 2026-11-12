using EasyInput.Editor;
using UnityEditor;
using UnityEngine;
namespace EasyInput.Integrations.Editor
{
    [CustomEditor(typeof(EasyBuildingInput))]
    public sealed class EasyBuildingInputEditor : UnityEditor.Editor
    {
        private bool advanced;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in new[] { "Player", "Placer", "Mode", "AutoEnterOnEnable" }) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            advanced = EditorGUILayout.Foldout(advanced, "Advanced: bindings and context", true);
            if (advanced) foreach (string name in new[] { "EnterBuildingContext", "GameplayMap", "ExtraBlockedActions", "Confirm", "CancelAction", "RotateRight", "RotateLeft", "Demolish" }) EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
            serializedObject.ApplyModifiedProperties();
            var value = (EasyBuildingInput)target; var player = value.Player ? value.Player : value.GetComponentInParent<PlayerInputSettings>();
            if (!Application.isPlaying && GUILayout.Button("Use First Person Preset"))
            {
                Undo.RecordObject(value,"Use First Person Preset");
                var placer = value.Placer ? value.Placer : value.GetComponent<EasyBuilding.BuildingPlacer>();
                if (placer) Undo.RecordObject(placer,"Use First Person Preset");
                value.UseFirstPersonPreset(); EditorUtility.SetDirty(value); if (placer) EditorUtility.SetDirty(placer);
            }
            InputAdapterDiagnostics.Player(player,value); var source = InputAdapterDiagnostics.Source(player);
            InputAdapterDiagnostics.Binding(source,value.Confirm,"Confirm",true); InputAdapterDiagnostics.Binding(source,value.CancelAction,"Cancel",true);
            InputAdapterDiagnostics.Binding(source,value.RotateRight,"Rotate Right"); InputAdapterDiagnostics.Binding(source,value.RotateLeft,"Rotate Left"); InputAdapterDiagnostics.Binding(source,value.Demolish,"Demolish");
            EditorGUILayout.HelpBox("For FPS: apply the preset, select a building in the shared Building Actor session, then call Enter(). Exit() cancels selection and releases this adapter's context. The Placer needs View Camera and the same Building Actor used by interactions. Strategy preserves the original full Gameplay block.",MessageType.Info);
            if (Application.isPlaying) { EditorGUILayout.LabelField("Building mode",value.IsBuilding ? "Active" : "Inactive"); if (GUILayout.Button(value.IsBuilding ? "Exit" : "Enter")) { if (value.IsBuilding) value.Exit(); else value.Enter(); } }
        }
    }
}
