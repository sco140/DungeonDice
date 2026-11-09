using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameUtility
{
    public enum DiceFaceValue
    {
        RED,
        BLUE,
        YELLOW,
        BLANK,
    }

    public enum ManaColor
    {
        Generic,
        Red,
        Yellow,
        Blue,
    }

    [Serializable]
    public struct Mana
    {
        private Dictionary<ManaColor, uint> m_dicMana;

        public Mana(Dictionary<ManaColor, uint> dicMana)
        {
            m_dicMana = dicMana;
        }

        public Mana(uint generic, uint red, uint yellow, uint blue)
        {
            m_dicMana = new Dictionary<ManaColor, uint>
            {
                { ManaColor.Generic, blue },
                { ManaColor.Red, red },
                { ManaColor.Yellow, yellow },
                { ManaColor.Blue, blue },
            };
        }
    }

    [Serializable]
    public struct EffectInfo
    {
        UnitController owner;
        UnitController target;
        Dictionary<string, int> effectParams;
    }
    public enum StatusEffect
    {
        None = 0,
        Armor = 1 << 0,
        Burn = 1 << 1,
        Bleeding = 1 << 2,
        CarrionArmor = 1 << 3,
        Cooldown = 1 << 4,
        Usage_Cooldown = 1 << 5,
        Fear = 1 << 6,
        Freeze = 1 << 7,
        Poison = 1 << 8,
        Stun = 1 << 9,
        MAX,
    }

    [Serializable]
    public struct StatusPackage
    {
        UnitController target;
        StatusEffect statusEffect;
        Dictionary<string, int> statusParams;
    }
    public enum TriggerType
    {
        BeforeTurnStart,
        AfterTurnStart,
        BeforeTurnEnd,
        AfterTurnEnd,
        BeforeSkillActivate,
        AfterSkillActivate,
        BeforeResourceGain,
        AfterResourceGain,
        BeforeRollDice,
        AfterRollDice,
        BeforeDamageTaken,
        AfterDamageTaken,
        MAX
    }

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError(
                    $"SerializableDictionary mismatch. Keys:{keys.Count} Values:{values.Count}");
            }

            int count = Mathf.Min(keys.Count, values.Count);

            for (int i = 0; i < count; i++)
            {
                var key = keys[i];

                if (key == null)
                    continue;

                if (ContainsKey(key))
                {
                    Debug.LogWarning($"Duplicate key: {key}");
                    continue;
                }

                Add(key, values[i]);
            }
        }
    }

    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 25f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            SerializedProperty keys = property.FindPropertyRelative("keys");

            return EditorGUIUtility.singleLineHeight +
                   (keys.arraySize + 1) * (EditorGUIUtility.singleLineHeight + 2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty keys = property.FindPropertyRelative("keys");
            SerializedProperty values = property.FindPropertyRelative("values");

            property.isExpanded = EditorGUI.Foldout(
                new Rect(
                    position.x,
                    position.y,
                    position.width,
                    EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                label,
                true);

            if (!property.isExpanded)
                return;

            EditorGUI.indentLevel++;

            float y = position.y + EditorGUIUtility.singleLineHeight + 2;
            float lineHeight = EditorGUIUtility.singleLineHeight;

            int maxSize = Mathf.Max(keys.arraySize, values.arraySize);

            while (keys.arraySize < maxSize)
                keys.InsertArrayElementAtIndex(keys.arraySize);

            while (values.arraySize < maxSize)
                values.InsertArrayElementAtIndex(values.arraySize);

            for (int i = 0; i < maxSize; i++)
            {
                Rect keyRect = new Rect(
                    position.x,
                    y,
                    position.width * 0.45f,
                    lineHeight);

                Rect valueRect = new Rect(
                    position.x + position.width * 0.5f,
                    y,
                    position.width * 0.40f,
                    lineHeight);

                Rect removeRect = new Rect(
                    position.x + position.width - ButtonWidth,
                    y,
                    ButtonWidth,
                    lineHeight);

                EditorGUI.PropertyField(
                    keyRect,
                    keys.GetArrayElementAtIndex(i),
                    GUIContent.none);

                EditorGUI.PropertyField(
                    valueRect,
                    values.GetArrayElementAtIndex(i),
                    GUIContent.none);

                if (GUI.Button(removeRect, "-"))
                {
                    keys.DeleteArrayElementAtIndex(i);
                    values.DeleteArrayElementAtIndex(i);
                    break;
                }

                y += lineHeight + 2;
            }

            Rect addRect = new Rect(
                position.x,
                y,
                position.width,
                lineHeight);

            if (GUI.Button(addRect, "Add Entry"))
            {
                keys.arraySize++;
                values.arraySize++;
                SetDefaults(ref keys);
                SetDefaults(ref values);
            }

            EditorGUI.indentLevel--;
        }
        
        private void SetDefaults(ref SerializedProperty property)
        {
            var keyProp = property.GetArrayElementAtIndex(property.arraySize - 1);

            switch (keyProp.propertyType)
            {
                case SerializedPropertyType.String:
                    keyProp.stringValue = "";
                    break;

                case SerializedPropertyType.Integer:
                    keyProp.intValue = 0;
                    break;

                case SerializedPropertyType.Float:
                    keyProp.floatValue = 0f;
                    break;

                case SerializedPropertyType.Boolean:
                    keyProp.boolValue = false;
                    break;

                case SerializedPropertyType.ObjectReference:
                    keyProp.objectReferenceValue = null;
                    break;
                default:
                    Debug.LogWarning($"Unsupported property type: {keyProp.propertyType}");
                    break;
            }
        }
    }
}
