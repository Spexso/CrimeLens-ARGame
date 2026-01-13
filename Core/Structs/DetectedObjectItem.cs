using UnityEngine;

[System.Serializable]
public class DetectedObjectItem
{
    public string objectName;
    public Sprite objectIcon;
    
    public DetectedObjectItem(string name, Sprite icon)
    {
        objectName = name;
        objectIcon = icon;
    }
}