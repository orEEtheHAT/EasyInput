using EasyInput.Editor;
using UnityEditor;
using UnityEngine;
namespace EasyInput.Integrations.Editor
{
    [CustomEditor(typeof(EasyMovementInput))]
    public sealed class EasyMovementInputEditor : UnityEditor.Editor
    {
        private bool advanced;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in new[] { "Player", "Character" }) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            advanced = EditorGUILayout.Foldout(advanced, "Advanced: bindings and pitch limits", true);
            if (advanced) foreach (string name in new[] { "MinimumPitch", "MaximumPitch", "Move", "Look", "JumpAction", "CrouchAction", "SprintAction", "NoclipAction" }) EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
            serializedObject.ApplyModifiedProperties();
            var value = (EasyMovementInput)target; var player = value.Player ? value.Player : value.GetComponentInParent<PlayerInputSettings>();
            InputAdapterDiagnostics.Player(player,value); var source = InputAdapterDiagnostics.Source(player);
            InputAdapterDiagnostics.Binding(source,value.Move,"Move",true); InputAdapterDiagnostics.Binding(source,value.Look,"Look",true);
            InputAdapterDiagnostics.Binding(source,value.JumpAction,"Jump"); InputAdapterDiagnostics.Binding(source,value.CrouchAction,"Crouch"); InputAdapterDiagnostics.Binding(source,value.SprintAction,"Sprint"); InputAdapterDiagnostics.Binding(source,value.NoclipAction,"Noclip");
            EditorGUILayout.HelpBox("Assign the same Player as the other adapters. Approach and scripted motion must use Character.AcquireInputBlock(owner). Re-enable this adapter after changing binding links in Play Mode.",MessageType.Info);
        }
    }
}
