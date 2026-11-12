using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput.Editor
{
    [CustomPropertyDrawer(typeof(InputActionLink))]
    public sealed class InputActionLinkDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight * 2 + 4;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var reference = property.FindPropertyRelative("Reference");
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(position, reference, label);
            position.y += position.height + 2;
            using (new EditorGUI.DisabledScope(reference.objectReferenceValue != null))
                EditorGUI.PropertyField(position, property.FindPropertyRelative("Name"), new GUIContent("Map / action", "Used when the source reference above is empty. Empty disables an optional action."));
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Inspector diagnostics read authoring data without creating a runtime player asset in Edit Mode.</summary>
    public static class InputAdapterDiagnostics
    {
        public static InputActionAsset Source(PlayerInputSettings player)
        {
            if (!player) return null;
            if (Application.isPlaying) return player.Actions;
            var source = new SerializedObject(player).FindProperty("sourceActions").objectReferenceValue as InputActionAsset;
            return source ? source : Resources.Load<InputActionAsset>("EasyInputDefaults");
        }
        public static void Binding(InputActionAsset source, InputActionLink link, string label, bool required = false)
        {
            if (!source || link == null) return;
            string path = link.Reference && link.Reference.action != null ? link.Reference.action.id.ToString() : link.Name;
            bool configured = !string.IsNullOrWhiteSpace(path);
            if (configured && source.FindAction(path, false) != null) return;
            if (required || configured)
                EditorGUILayout.HelpBox(label + " is missing from the player's action asset. Assign a matching source reference or map/action name." + (required ? " This action is required." : " Clear its name to disable this optional feature."), required ? MessageType.Error : MessageType.Warning);
        }
        public static void Player(PlayerInputSettings player, Component adapter)
        {
            if (!player && !adapter.GetComponentInParent<PlayerInputSettings>())
                EditorGUILayout.HelpBox("Assign Player Input Settings, or add it on this object or a parent.", MessageType.Error);
        }
    }
}
