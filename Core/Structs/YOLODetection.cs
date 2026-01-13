using UnityEngine;

[System.Serializable]
public class YOLODetection
{
    public string objectName;
    public Vector2 centerPosition;      // Screen coordinates (pixels)
    public Rect boundingBox;            // Normalized (0-1) from YOLO
    public float confidence;            // Detection confidence (0-1)
    
    public YOLODetection(string name, Vector2 center, Rect bbox, float conf)
    {
        objectName = name;
        centerPosition = center;
        boundingBox = bbox;
        confidence = conf;
    }
    
    public override string ToString()
    {
        return $"{objectName} ({confidence:P0}) at screen({centerPosition.x:F0}, {centerPosition.y:F0})";
    }
}