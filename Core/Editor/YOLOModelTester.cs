using UnityEngine;
using UnityEditor;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Linq;

public class YOLOModelTester : EditorWindow
{
    [MenuItem("CrimeLens/YOLO Model Tester")]
    public static void ShowWindow()
    {
        GetWindow<YOLOModelTester>("YOLO Model Tester");
    }

    // Settings
    private ModelAsset modelAsset;
    private YOLOClassNames classNames;
    private Texture2D testImage;
    private int inputSize = 512;
    private float confidenceThreshold = 0.25f;
    private float iouThreshold = 0.45f;
    private bool enableDebugImages = true;
    private string lastDebugFolder = "";
    
    // Results
    private List<YOLODetection> lastDetections;
    private Texture2D resultPreview;
    private Vector2 scrollPosition;
    private bool isProcessing = false;
    private string statusMessage = "";
    
    // Preview settings
    private bool showBoundingBoxes = true;
    private Color boxColor = Color.green;
    private int boxThickness = 2;

    void OnGUI()
    {
        GUILayout.Label("YOLOv8 Custom Model Tester", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Scrollable body
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Model Settings
        DrawSection("Model Settings", () =>
        {
            modelAsset = (ModelAsset)EditorGUILayout.ObjectField(
                "ONNX Model", 
                modelAsset, 
                typeof(ModelAsset), 
                false
            );
            
            classNames = (YOLOClassNames)EditorGUILayout.ObjectField(
                "Class Names", 
                classNames, 
                typeof(YOLOClassNames), 
                false
            );

            if (classNames != null)
            {
                EditorGUILayout.HelpBox(
                    $"Classes ({classNames.GetClassCount()}): {string.Join(", ", classNames.GetClassNames())}", 
                    MessageType.Info
                );
            }
            
            testImage = (Texture2D)EditorGUILayout.ObjectField(
                "Test Image", 
                testImage, 
                typeof(Texture2D), 
                false
            );
        });

        EditorGUILayout.Space(10);

        // Detection Parameters
        DrawSection("Detection Parameters", () =>
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Input Size");
            
            if (GUILayout.Button("256", GUILayout.Width(50))) inputSize = 256;
            if (GUILayout.Button("384", GUILayout.Width(50))) inputSize = 384;
            if (GUILayout.Button("512", GUILayout.Width(50))) inputSize = 512;
            if (GUILayout.Button("640", GUILayout.Width(50))) inputSize = 640;
            
            EditorGUILayout.EndHorizontal();
            
            int newSize = EditorGUILayout.IntSlider("Custom Size", inputSize, 256, 640);
            
            if (newSize % 32 != 0)
            {
                int rounded = Mathf.RoundToInt(newSize / 32f) * 32;
                EditorGUILayout.HelpBox(
                    $"Input size must be divisible by 32. Nearest valid: {rounded}", 
                    MessageType.Warning
                );
                inputSize = Mathf.Clamp(rounded, 256, 640);
            }
            else
            {
                inputSize = newSize;
            }
            
            EditorGUILayout.LabelField($"Current: {inputSize}x{inputSize}", EditorStyles.miniLabel);
            
            confidenceThreshold = EditorGUILayout.Slider("Confidence Threshold", confidenceThreshold, 0.1f, 0.9f);
            iouThreshold = EditorGUILayout.Slider("IoU Threshold", iouThreshold, 0.1f, 0.9f);
        });

        EditorGUILayout.Space(10);

        // Debug Settings - NEW SECTION
        DrawSection("Debug Settings", () =>
        {
            enableDebugImages = EditorGUILayout.Toggle("Save Debug Images", enableDebugImages);
            
            if (enableDebugImages)
            {
                EditorGUILayout.HelpBox(
                    "Will save images at each pipeline stage:\n" +
                    "• 02_input_original\n" +
                    "• 03_resized\n" +
                    "• 04_letterboxed_model_input\n" +
                    "• 05_final_detections", 
                    MessageType.Info
                );
            }
            
            if (!string.IsNullOrEmpty(lastDebugFolder))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Last Session:", EditorStyles.miniLabel);
                if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                {
                    EditorUtility.RevealInFinder(lastDebugFolder);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.LabelField(lastDebugFolder, EditorStyles.wordWrappedMiniLabel);
            }
        });

        EditorGUILayout.Space(10);

        // Preview Settings
        DrawSection("Preview Settings", () =>
        {
            showBoundingBoxes = EditorGUILayout.Toggle("Show Bounding Boxes", showBoundingBoxes);
            boxColor = EditorGUILayout.ColorField("Box Color", boxColor);
            boxThickness = EditorGUILayout.IntSlider("Box Thickness", boxThickness, 1, 5);
        });

        EditorGUILayout.Space(10);

        // Run Button
        GUI.enabled = !isProcessing && modelAsset != null && testImage != null && classNames != null;
        if (GUILayout.Button(" Run Detection", GUILayout.Height(40)))
        {
            RunDetection();
        }
        GUI.enabled = true;

        // Status
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        EditorGUILayout.Space(10);

        // Results
        if (lastDetections != null && lastDetections.Count > 0)
        {
            DrawResultsSection();
        }
        else if (lastDetections != null && lastDetections.Count == 0)
        {
            EditorGUILayout.HelpBox("No objects detected. Try lowering confidence threshold.", MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        drawContent();
        EditorGUILayout.EndVertical();
    }

    private void DrawResultsSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label($"Detection Results ({lastDetections.Count} objects)", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Preview
        if (resultPreview != null && showBoundingBoxes)
        {
            float previewHeight = 300f;
            float aspect = (float)resultPreview.width / resultPreview.height;
            float previewWidth = previewHeight * aspect;
            
            Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
            GUI.DrawTexture(previewRect, resultPreview, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(10);

        // List
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        
        foreach (var detection in lastDetections.OrderByDescending(d => d.confidence))
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUILayout.Label($" {detection.objectName}", GUILayout.Width(120));
            GUILayout.Label($"{detection.confidence:P1}", GUILayout.Width(60));
            
            var bbox = detection.boundingBox;
            GUILayout.Label($"[{bbox.x:F2}, {bbox.y:F2}, {bbox.width:F2}x{bbox.height:F2}]", 
                EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);

        if (GUILayout.Button(" Copy Results"))
        {
            CopyResultsToClipboard();
        }

        EditorGUILayout.EndVertical();
    }

    private void RunDetection()
    {
        isProcessing = true;
        statusMessage = "Processing...";
        Repaint();

        try
        {
            Debug.Log("═══════════════════════════════════════");
            Debug.Log("    [EDITOR] Starting YOLODetectorTester...");
            Debug.Log($"   Model: {modelAsset.name}");
            Debug.Log($"   Image: {testImage.name} ({testImage.width}x{testImage.height})");
            Debug.Log($"   Input Size: {inputSize}");
            Debug.Log($"   Debug Images: {enableDebugImages}");
            Debug.Log($"   Classes: {string.Join(", ", classNames.GetClassNames())}");
            Debug.Log("═══════════════════════════════════════");

            using (var detector = new YOLODetectorTester(
                modelAsset, 
                classNames.GetClassNames(),
                inputSize, 
                confidenceThreshold, 
                iouThreshold,
                enableDebugImages  // Pass the debug flag
            ))
            {
                lastDetections = detector.DetectObjects(testImage);
                
                // Store debug folder path
                if (enableDebugImages)
                {
                    lastDebugFolder = detector.GetDebugFolder();
                }
                
                if (showBoundingBoxes)
                {
                    resultPreview = DrawBoundingBoxes(testImage, lastDetections);
                }
                
                statusMessage = $" Found {lastDetections.Count} objects at {inputSize}x{inputSize}!";
                
                if (enableDebugImages && !string.IsNullOrEmpty(lastDebugFolder))
                {
                    statusMessage += $"\n Debug images saved to folder.";
                }
                
                Debug.Log($" [EDITOR] Detection complete: {lastDetections.Count} objects found");
            }
        }
        catch (System.Exception e)
        {
            statusMessage = $" Error: {e.Message}";
            Debug.LogError($" [EDITOR] Detection Error: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
        finally
        {
            isProcessing = false;
            Repaint();
        }
    }

    private Texture2D DrawBoundingBoxes(Texture2D original, List<YOLODetection> detections)
    {
        Texture2D readableTexture = MakeTextureReadable(original);
        Texture2D result = new Texture2D(readableTexture.width, readableTexture.height, TextureFormat.RGB24, false);
        
        Color[] pixels = readableTexture.GetPixels();
        result.SetPixels(pixels);
        result.Apply();

        foreach (var detection in detections)
        {
            DrawBox(result, detection.boundingBox, boxColor, boxThickness);
            
            int labelX = Mathf.RoundToInt(detection.boundingBox.x * original.width);
            int labelY = Mathf.RoundToInt(detection.boundingBox.y * original.height);
            DrawText(result, labelX, labelY - 15, $"{detection.objectName} {detection.confidence:P0}", boxColor);
        }

        result.Apply();
        
        if (readableTexture != original)
        {
            Object.DestroyImmediate(readableTexture);
        }
         
        return result;
    }

    private Texture2D MakeTextureReadable(Texture2D source)
    {
        try
        {
            source.GetPixel(0, 0);
            return source;
        }
        catch
        {
            RenderTexture tempRT = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, tempRT);
            
            RenderTexture.active = tempRT;
            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            RenderTexture.active = null;
            
            RenderTexture.ReleaseTemporary(tempRT);
            
            return readable;
        }
    }

    private void DrawBox(Texture2D texture, Rect normalizedBox, Color color, int thickness)
    {
        int x = Mathf.RoundToInt(normalizedBox.x * texture.width);
        int y = Mathf.RoundToInt(normalizedBox.y * texture.height);
        int w = Mathf.RoundToInt(normalizedBox.width * texture.width);
        int h = Mathf.RoundToInt(normalizedBox.height * texture.height);

        for (int t = 0; t < thickness; t++)
        {
            for (int i = x; i < x + w; i++)
            {
                SetPixelSafe(texture, i, y + t, color);
                SetPixelSafe(texture, i, y + h - t, color);
            }
            
            for (int j = y; j < y + h; j++)
            {
                SetPixelSafe(texture, x + t, j, color);
                SetPixelSafe(texture, x + w - t, j, color);
            }
        }
    }

    private void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
        {
            texture.SetPixel(x, y, color);
        }
    }

    private void DrawText(Texture2D texture, int x, int y, string text, Color color)
    {
        int boxWidth = text.Length * 7;
        int boxHeight = 12;
        
        for (int i = 0; i < boxWidth; i++)
        {
            for (int j = 0; j < boxHeight; j++)
            {
                SetPixelSafe(texture, x + i, y + j, new Color(0, 0, 0, 0.7f));
            }
        }
    }

    private void CopyResultsToClipboard()
    {
        string results = $"YOLO Detection Results\n";
        results += $"Model: {modelAsset.name}\n";
        results += $"Image: {testImage.name}\n";
        results += $"Input Size: {inputSize}x{inputSize}\n";
        results += $"Objects: {lastDetections.Count}\n\n";

        foreach (var detection in lastDetections.OrderByDescending(d => d.confidence))
        {
            results += $"- {detection.objectName}: {detection.confidence:P1}\n";
        }

        GUIUtility.systemCopyBuffer = results;
        statusMessage = " Copied!";
    }
}

public class YOLODetectorTester : System.IDisposable
{
    private NewYOLOv8Detector detector;
    private GameObject detectorObject;
    private YOLOClassNames tempClassNames;
    private YOLODebugImageSaver debugSaver;
    private bool disposed = false;

    public YOLODetectorTester(ModelAsset modelAsset, string[] classes, int inputSize, float confThreshold, float iouThreshold, bool enableDebugSaving = true)
    {
        Debug.Log("[TESTER] Creating YOLODetectorTester...");
        Debug.Log($"Classes to set ({classes.Length}): {string.Join(", ", classes)}");
        
        // Initialize debug saver for editor
        if (enableDebugSaving)
        {
            debugSaver = new YOLODebugImageSaver(true);
            Debug.Log($"Debug saver initialized: {debugSaver.GetSessionFolder()}");
        }
        
        // Create temporary GameObject
        detectorObject = new GameObject("TempYOLODetector");
        detectorObject.hideFlags = HideFlags.HideAndDontSave;
        
        detector = detectorObject.AddComponent<NewYOLOv8Detector>();
        
        // Create temporary YOLOClassNames ScriptableObject
        tempClassNames = ScriptableObject.CreateInstance<YOLOClassNames>();
        
        #if UNITY_EDITOR
        tempClassNames.SetClassNamesForTesting(classes);
        Debug.Log($"Set {classes.Length} class names using helper method");
        #endif
        
        // Verify it works
        string[] retrievedClasses = tempClassNames.GetClassNames();
        if (retrievedClasses != null && retrievedClasses.Length > 0)
        {
            Debug.Log($"VERIFIED: Retrieved {retrievedClasses.Length} classes from ScriptableObject");
            for (int i = 0; i < retrievedClasses.Length; i++)
            {
                Debug.Log($"      [{i}] {retrievedClasses[i]}");
            }
            
            if (retrievedClasses.Length != classes.Length)
            {
                Debug.LogError($"WARNING: Expected {classes.Length} classes but got {retrievedClasses.Length}!");
            }
        }
        else
        {
            Debug.LogError($"VERIFICATION FAILED! Got {retrievedClasses?.Length ?? 0} classes");
        }
        
        // Initialize detector
        try
        {
            Debug.Log("Calling InitializeForTesting...");
            detector.InitializeForTesting(modelAsset, tempClassNames, inputSize, confThreshold, iouThreshold);
            
            // Pass debug saver to detector
            if (debugSaver != null)
            {
                detector.SetDebugSaver(debugSaver);
                Debug.Log("Debug saver passed to detector");
            }
            
            Debug.Log("Detector initialized successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Initialization FAILED: {e.Message}");
            Debug.LogError($"Stack: {e.StackTrace}");
            throw;
        }
        
        Debug.Log("═══════════════════════════════════════");
    }

    public List<YOLODetection> DetectObjects(Texture2D inputTexture)
    {
        Debug.Log($"[TESTER] Detecting objects in {inputTexture.name} ({inputTexture.width}x{inputTexture.height})");
        
        // Notify debug saver of new capture
        debugSaver?.StartNewCapture();
        
        try
        {
            var results = detector.DetectObjects(inputTexture, Screen.width, Screen.height);
            Debug.Log($"Detection complete: {results.Count} objects found");
            
            if (results.Count > 0)
            {
                foreach (var det in results)
                {
                    Debug.Log($"-{det.objectName}: {det.confidence:P1}");
                }
            }
            
            return results;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Detection FAILED: {e.Message}");
            Debug.LogError($"Stack: {e.StackTrace}");
            throw;
        }
    }

    public string GetDebugFolder()
    {
        return debugSaver?.GetSessionFolder();
    }

    public void Dispose()
    {
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                Debug.Log("[TESTER] Cleaning up YOLODetectorTester...");
                
                if (debugSaver != null)
                {
                    Debug.Log($"Debug images saved to: {debugSaver.GetSessionFolder()}");
                }
                
                if (detectorObject != null)
                {
                    Object.DestroyImmediate(detectorObject);
                }
                
                if (tempClassNames != null)
                {
                    Object.DestroyImmediate(tempClassNames);
                }
            }
            disposed = true;
        }
    }
}