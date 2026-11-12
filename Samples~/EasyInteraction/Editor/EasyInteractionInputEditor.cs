using EasyInput.Editor;
using UnityEditor;
using UnityEngine;
namespace EasyInput.Integrations.Editor
{
    [CustomEditor(typeof(EasyInteractionInput))]
    public sealed class EasyInteractionInputEditor : UnityEditor.Editor
    {
        private bool advanced;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string name in new[] { "Player", "Actor", "toggleInsteadOfHold", "ItemControls" }) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            advanced = EditorGUILayout.Foldout(advanced, "Advanced: bindings and item controls", true);
            if (advanced) foreach (string name in new[] { "ConsumeLookWhileRotating", "ConsumeLookWhileAnalog", "MouseRotationMultiplier", "GamepadRotationMultiplier", "MouseDistancePerUnit", "GamepadDistancePerSecond", "MouseAnalogScale", "GamepadAnalogScale", "Interact", "CancelAction", "NextAction", "Secondary", "NextTarget", "ThrowItem", "RotateItem", "ItemRotation", "ItemDistance", "AnalogValue", "Look" }) EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
            serializedObject.ApplyModifiedProperties();
            var value = (EasyInteractionInput)target; var player = value.Player ? value.Player : value.GetComponentInParent<PlayerInputSettings>();
            InputAdapterDiagnostics.Player(player,value); var source = InputAdapterDiagnostics.Source(player);
            InputAdapterDiagnostics.Binding(source,value.Interact,"Interact",true);
            foreach (var binding in new[] { value.CancelAction,value.NextAction,value.Secondary,value.NextTarget,value.ThrowItem,value.RotateItem,value.ItemRotation,value.ItemDistance,value.AnalogValue,value.Look }) InputAdapterDiagnostics.Binding(source,binding,binding.Name ?? "Action");
            EditorGUILayout.HelpBox("Interaction Presenter automatically reads this adapter's text and icon. R / Left Shoulder rotates a held item; wheel / D-pad Up-Down changes distance; G / D-pad Left throws it. ItemRotation and InteractionValue must be separate actions from Look, even when they share controls.",MessageType.Info);
            if (Application.isPlaying) EditorGUILayout.LabelField("Current binding",value.BindingLabel);
        }
    }
}
