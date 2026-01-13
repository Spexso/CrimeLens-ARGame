using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[Serializable]
public class ObjectListEntry : MonoBehaviour
{
    [SerializeField] public Image icon;
    [SerializeField] public TextMeshProUGUI label;

    public void Setup(DetectedObjectItem item)
    {
        label.text = item.objectName;
        icon.sprite = item.objectIcon;
    }

    public string GetText()
    {
        return label.text;
    }

    public Sprite GetIcon()
    {
        return icon.sprite;
    }
}