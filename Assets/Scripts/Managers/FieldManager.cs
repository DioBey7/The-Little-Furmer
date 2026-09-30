using System.Collections.Generic;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    public static FieldManager Instance { get; private set; }

    [HideInInspector] public List<Field> activeFields = new List<Field>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeFields(List<Field> fields)
    {
        if (activeFields == null) activeFields = new List<Field>();
        else activeFields.Clear();

        if (fields != null)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i] != null)
                {
                    activeFields.Add(fields[i]);
                    fields[i].ResetField();
                }
            }
        }
    }

    private void EnsureFieldsAreInitialized()
    {
        if (activeFields != null)
        {
            activeFields.RemoveAll(f => f == null);
        }

        if (activeFields == null || activeFields.Count == 0)
        {
            activeFields = new List<Field>(FindObjectsOfType<Field>(true));
        }
    }

    public void ReportItemCollected(GameObject itemObject)
    {
        if (itemObject == null) return;

        if (itemObject.TryGetComponent(out Item itemComp) && itemComp.OriginDirtPoint != null)
        {
            Field parentField = itemComp.OriginDirtPoint.GetComponentInParent<Field>();
            if (parentField != null)
            {
                parentField.ReportItemCollected(itemObject);
            }
        }
    }

    public GameObject GetNearestReadyCrop(Vector3 seekerPosition, out ItemType foundType)
    {
        foundType = ItemType.Tomato;
        GameObject nearestCrop = null;
        float minDistance = Mathf.Infinity;

        EnsureFieldsAreInitialized();

        for (int i = 0; i < activeFields.Count; i++)
        {
            Field mainField = activeFields[i];
            if (mainField == null || !mainField.gameObject.activeInHierarchy || mainField.cropAreas == null) continue;

            for (int c = 0; c < mainField.cropAreas.Count; c++)
            {
                Field.CropArea area = mainField.cropAreas[c];
                if (area == null || area.dirtPoints == null) continue;

                for (int j = 0; j < area.dirtPoints.Count; j++)
                {
                    Transform dirt = area.dirtPoints[j];
                    if (dirt == null || !dirt.gameObject.activeInHierarchy) continue;

                    if (area.dirtOccupancy.TryGetValue(dirt, out GameObject crop) && crop != null && crop.activeInHierarchy)
                    {
                        if (crop.TryGetComponent(out Item itemComp) && !itemComp.isCollected)
                        {
                            float dist = (seekerPosition - crop.transform.position).sqrMagnitude;
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                nearestCrop = crop;
                                foundType = itemComp.ItemType;
                            }
                        }
                    }
                }
            }
        }
        return nearestCrop;
    }

    public GameObject GetNearestReadyCropOfType(Vector3 seekerPosition, ItemType targetType)
    {
        GameObject nearestCrop = null;
        float minDistance = Mathf.Infinity;

        EnsureFieldsAreInitialized();

        for (int i = 0; i < activeFields.Count; i++)
        {
            Field mainField = activeFields[i];
            if (mainField == null || !mainField.gameObject.activeInHierarchy || mainField.cropAreas == null) continue;

            for (int c = 0; c < mainField.cropAreas.Count; c++)
            {
                Field.CropArea area = mainField.cropAreas[c];
                if (area == null || area.dirtPoints == null || area.cachedItemType != targetType) continue;

                for (int j = 0; j < area.dirtPoints.Count; j++)
                {
                    Transform dirt = area.dirtPoints[j];
                    if (dirt == null || !dirt.gameObject.activeInHierarchy) continue;

                    if (area.dirtOccupancy.TryGetValue(dirt, out GameObject crop) && crop != null && crop.activeInHierarchy)
                    {
                        if (crop.TryGetComponent(out Item itemComp) && !itemComp.isCollected && itemComp.ItemType == targetType)
                        {
                            float dist = (seekerPosition - crop.transform.position).sqrMagnitude;
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                nearestCrop = crop;
                            }
                        }
                    }
                }
            }
        }
        return nearestCrop;
    }
}
