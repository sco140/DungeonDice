using GameUtility;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "Activate", menuName = "Scriptable Objects/Skills/Activate")]
public class Activate : BaseSkill
{
    public Mana m_manaCost = new()
    {
        m_generic = 0,
        m_red = 0,
        m_blue = 0,
        m_yellow = 0
    };
}

[CustomEditor(typeof(Activate))]
public class ActivateSkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("m_manaCost"));

        EditorGUILayout.Space();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "m_manaCost");

        serializedObject.ApplyModifiedProperties();
    }
}