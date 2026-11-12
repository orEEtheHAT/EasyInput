using EasyInput.Editor;
using EasyFeedbacks;
using UnityEditor;
using UnityEngine;
namespace EasyInput.Integrations.Editor
{
    [CustomEditor(typeof(InputFeedback))]
    public sealed class InputFeedbackEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var value=(InputFeedback)target;
            InputAdapterDiagnostics.Player(value.Player,value);
            if (value.ContextProvider && !(value.ContextProvider is IFeedbackContextProvider)) EditorGUILayout.HelpBox("Context Provider must implement IFeedbackContextProvider. Assign Player Feedback Context to target this player's active gamepad.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(PlayerFeedbackContext))]
    public sealed class PlayerFeedbackContextEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var value=(PlayerFeedbackContext)target; InputAdapterDiagnostics.Player(value.Player,value);
            EditorGUILayout.HelpBox("Assign this component to the optional Context Provider on UI, Interaction or Building feedback adapters. It supplies the player's active gamepad and preserves an explicit target or no-gamepad request.",MessageType.Info);
        }
    }
}

