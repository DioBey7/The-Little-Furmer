#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Level.LevelObjective))]
public class LevelObjectiveDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty typeProp = property.FindPropertyRelative("type");
        SerializedProperty itemTypeProp = property.FindPropertyRelative("itemType");
        SerializedProperty targetAmountProp = property.FindPropertyRelative("targetAmount");

        var objectiveType = (Level.ObjectiveType)typeProp.enumValueIndex;

        float singleLineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect typeRect = new Rect(position.x, position.y, position.width, singleLineHeight);
        EditorGUI.PropertyField(typeRect, typeProp);

        float nextY = position.y + singleLineHeight + spacing;

        bool needsItem = objectiveType == Level.ObjectiveType.HarvestItem ||
                         objectiveType == Level.ObjectiveType.ProduceItem ||
                         objectiveType == Level.ObjectiveType.SellItem;
      

        if (needsItem)
        {
            Rect itemTypeRect = new Rect(position.x, nextY, position.width, singleLineHeight);
            EditorGUI.PropertyField(itemTypeRect, itemTypeProp);
            nextY += singleLineHeight + spacing;
        }

        Rect targetRect = new Rect(position.x, nextY, position.width, singleLineHeight);
        EditorGUI.PropertyField(targetRect, targetAmountProp);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProp = property.FindPropertyRelative("type");
        var objectiveType = (Level.ObjectiveType)typeProp.enumValueIndex;

        bool needsItem = objectiveType == Level.ObjectiveType.HarvestItem ||
                         objectiveType == Level.ObjectiveType.ProduceItem ||
                         objectiveType == Level.ObjectiveType.SellItem;

        float singleLineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        return needsItem ? (singleLineHeight * 3 + spacing * 2) : (singleLineHeight * 2 + spacing);
    }
}
#endif
