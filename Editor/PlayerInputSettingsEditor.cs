using UnityEditor;
using UnityEngine;

namespace EasyInput.Editor
{
    [InitializeOnLoad]
    internal static class ProcessorRegistration
    { static ProcessorRegistration() { EasyInputStickProcessor.Register(); EasyInputAxisProcessor.Register(); } }

    [CustomEditor(typeof(PlayerInputSettings))]
    public sealed class PlayerInputSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Start with this component alone: empty Source Actions and Profile use the standard controls. Add Input Settings Panel to edit them. Consumers must read Actions/Resolve and leave map lifetime to this component.", MessageType.Info);
            EditorGUILayout.HelpBox("For custom assets, use Gameplay / UI / Building maps or matching context requests. Put easyInputStick on stick bindings so the user's deadzone is applied exactly once.", MessageType.None);
            if (Application.isPlaying)
            {
                var player = (PlayerInputSettings)target;
                EditorGUILayout.LabelField("Active device", player.ActiveDevice?.displayName ?? "No active device");
                EditorGUILayout.LabelField("Context requests", player.ContextRequestCount.ToString());
                EditorGUILayout.LabelField("Cursor", player.EffectiveCursor.ToString());
            }
        }
    }
}
