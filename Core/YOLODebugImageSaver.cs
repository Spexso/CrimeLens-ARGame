using UnityEngine;
using System.IO;
using System;

public class YOLODebugImageSaver
{
    private string sessionFolder;
    private int captureIndex = 0;
    private bool isEnabled;

    public YOLODebugImageSaver(bool enabled = true)
    {
        isEnabled = enabled;
        if (isEnabled)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            
            #if UNITY_EDITOR
            // Editor, save local
            string projectPath = Application.dataPath.Replace("/Assets", "");
            sessionFolder = Path.Combine(projectPath, "YOLO_Debug_Sessions", $"Session_{timestamp}");
            #else
            // App, use persistent data path
            sessionFolder = Path.Combine(Application.persistentDataPath, $"YOLO_Debug_{timestamp}");
            #endif
            
            if (!Directory.Exists(sessionFolder))
            {
                Directory.CreateDirectory(sessionFolder);
            }
            
            Debug.Log($" [DEBUG] Session folder created: {sessionFolder}");
        }
    }

    public void StartNewCapture()
    {
        captureIndex++;
    }

    public void SaveImage(Texture2D texture, string stageName)
    {
        if (!isEnabled || texture == null) return;

        try
        {
            // Make texture readable
            Texture2D readableTexture = MakeTextureReadable(texture);
            
            string fileName = $"{captureIndex:D3}_{stageName}.png";
            string fullPath = Path.Combine(sessionFolder, fileName);
            
            byte[] pngData = readableTexture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);
            
            Debug.Log($" [DEBUG] Saved: {fileName} ({readableTexture.width}x{readableTexture.height})");
            
            // Clean up
            if (readableTexture != texture)
            {
                #if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(readableTexture);
                #else
                UnityEngine.Object.Destroy(readableTexture);
                #endif
            }
        }
        catch (Exception e)
        {
            Debug.LogError($" [DEBUG] Failed to save {stageName}: {e.Message}");
        }
    }
    private Texture2D MakeTextureReadable(Texture2D source)
    {
        // Try to read directly first
        try
        {
            source.GetPixel(0, 0);
            return source; // Already readable
        }
        catch
        {
            // Create readable copy via RenderTexture
            RenderTexture tmp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, tmp);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            
            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            
            return readable;
        }
    }

    public void SaveImageWithDetections(Texture2D originalTexture, System.Collections.Generic.List<YOLODetection> detections, string stageName)
    {
        if (!isEnabled || originalTexture == null) return;

        // Make readable first
        Texture2D readableOriginal = MakeTextureReadable(originalTexture);
        
        // Create a copy to draw on
        Texture2D annotated = new Texture2D(readableOriginal.width, readableOriginal.height, TextureFormat.RGB24, false);
        annotated.SetPixels(readableOriginal.GetPixels());
        annotated.Apply();

        // Draw bounding boxes
        foreach (var det in detections)
        {
            DrawBoundingBox(annotated, det.boundingBox, det.objectName, det.confidence);
        }

        annotated.Apply();
        SaveImage(annotated, stageName);
        
        #if UNITY_EDITOR
        UnityEngine.Object.DestroyImmediate(annotated);
        if (readableOriginal != originalTexture)
        {
            UnityEngine.Object.DestroyImmediate(readableOriginal);
        }
        #else
        UnityEngine.Object.Destroy(annotated);
        if (readableOriginal != originalTexture)
        {
            UnityEngine.Object.Destroy(readableOriginal);
        }
        #endif
    }

    private void DrawBoundingBox(Texture2D texture, Rect box, string label, float confidence)
    {
        Color boxColor = Color.green;
        int thickness = 3;

        // Convert normalized coords to pixel coords
        int x = Mathf.RoundToInt(box.x);
        int y = Mathf.RoundToInt(box.y);
        int w = Mathf.RoundToInt(box.width);
        int h = Mathf.RoundToInt(box.height);

        // Draw rectangle
        DrawRect(texture, new Rect(x, y, w, h), boxColor, thickness);

        // Draw label background
        int labelHeight = 20;
        Rect labelBg = new Rect(x, y - labelHeight, 150, labelHeight);
        FillRect(texture, labelBg, new Color(0, 0, 0, 0.7f));

        Debug.Log($"   └─ {label} ({confidence:P1}) at ({x:F0}, {y:F0}, {w:F0}x{h:F0})");
    }

    private void DrawRect(Texture2D texture, Rect rect, Color color, int thickness)
    {
        int x = Mathf.RoundToInt(rect.x);
        int y = Mathf.RoundToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);

        // Top line
        FillRect(texture, new Rect(x, y, w, thickness), color);
        // Bottom line
        FillRect(texture, new Rect(x, y + h - thickness, w, thickness), color);
        // Left line
        FillRect(texture, new Rect(x, y, thickness, h), color);
        // Right line
        FillRect(texture, new Rect(x + w - thickness, y, thickness, h), color);
    }

    private void FillRect(Texture2D texture, Rect rect, Color color)
    {
        int startX = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, texture.width);
        int startY = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, texture.height);
        int endX = Mathf.Clamp(Mathf.RoundToInt(rect.x + rect.width), 0, texture.width);
        int endY = Mathf.Clamp(Mathf.RoundToInt(rect.y + rect.height), 0, texture.height);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    public string GetSessionFolder()
    {
        return sessionFolder;
    }
}