using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class NewYOLOv8Detector : MonoBehaviour
{
    [Header("Model Settings")]
    [Tooltip("Drag the best.onnx file here")]
    [SerializeField] private ModelAsset modelAsset;

    [Tooltip("Class names for your custom model")]
    [SerializeField] private YOLOClassNames classNames;

    [Header("Detection Settings")]
    [SerializeField] private int inputSize = 512;
    [SerializeField] private float confidenceThreshold = 0.40f;
    [SerializeField] private float iouThreshold = 0.45f;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // References
    private YOLODebugImageSaver debugSaver;
    private Model runtimeModel;
    private Worker worker;
    private int debugImageCounter = 0;
    private string[] currentClassNames;

    // Coordinate transformation data
    private float lastScale;
    private int lastPadLeft;
    private int lastPadTop;
    private int lastOriginalWidth;
    private int lastOriginalHeight;

    #region Initialization
    void Start()
    {
        InitializeModel();
    }

    public void SetDebugSaver(YOLODebugImageSaver saver)
    {
        debugSaver = saver;
    }

    public void InitializeForTesting(ModelAsset model, YOLOClassNames classes, int size, float confThresh, float iouThresh)
    {
        modelAsset = model;
        classNames = classes;
        inputSize = size;
        confidenceThreshold = confThresh;
        iouThreshold = iouThresh;
        enableDebugLogs = true;

        InitializeModel();
        LogDebugPath();
    }

    private void LogDebugPath()
    {
        TaggedLogger.LogDetection("[DEBUG] Image Save Location:");
        TaggedLogger.LogDetection($"Path: {Application.persistentDataPath}");
    }

    private void InitializeModel()
    {
        if (modelAsset == null || classNames == null)
        {
            TaggedLogger.LogNEWYOLODetection("[YOLO] Model or Classes not assigned!");
            return;
        }

        currentClassNames = classNames.GetClassNames();

        try
        {
            runtimeModel = ModelLoader.Load(modelAsset);
            // GPUCompute is usually best for mobile; fallback to CPU if needed
            worker = new Worker(runtimeModel, BackendType.GPUCompute);
            TaggedLogger.LogNEWYOLODetection($"[YOLO] Model Loaded. Input: {inputSize}x{inputSize}");
        }
        catch (System.Exception e)
        {
            TaggedLogger.LogNEWYOLODetection($"[YOLO] Load Failed: {e.Message}");
        }
    }
    #endregion

    #region Inference Pipeline
    public List<YOLODetection> DetectObjects(Texture2D inputTexture, int screenWidth, int screenHeight)
    {
        if (worker == null || inputTexture == null) return new List<YOLODetection>();

        //NEW: Log input characteristics
        TaggedLogger.LogNEWYOLODetection("═══════════════════════════════════════");
        TaggedLogger.LogNEWYOLODetection($"[DETECT] Input Texture: {inputTexture.width}x{inputTexture.height}");
        TaggedLogger.LogNEWYOLODetection($"[DETECT] Format: {inputTexture.format}");
        TaggedLogger.LogNEWYOLODetection($"[DETECT] Screen Dims: {screenWidth}x{screenHeight}");
        TaggedLogger.LogNEWYOLODetection($"[DETECT] Backend: {worker.backendType}");

        try
        {
            // SAVE: Input texture before preprocessing
            debugSaver?.SaveImage(inputTexture, "02_input_original");

            // 1. Preprocess (Resize -> Letterbox -> Tensor)
            using (Tensor<float> inputTensor = PreprocessImageLetterboxPythonStyle(inputTexture, debugImageCounter))
            {
                // 2. Inference
                worker.Schedule(inputTensor);
            } // inputTensor automatically disposed here

            // 3. Output - PeekOutput doesn't transfer ownership, so need to clone and dispose
            using (Tensor<float> cpuTensor = (worker.PeekOutput() as Tensor<float>).ReadbackAndClone())
            {
                // 4. Parse Results (pass screen dimensions)
                List<YOLODetection> detections = PostProcessOutput(cpuTensor, screenWidth, screenHeight);

                // SAVE: Final detections drawn on original image
                debugSaver?.SaveImageWithDetections(inputTexture, detections, "05_final_detections");

                debugImageCounter++;
                return detections;
            } // cpuTensor automatically disposed here
        }
        catch (System.Exception e)
        {
            TaggedLogger.LogNEWYOLODetection($" [YOLO] Detection Error: {e.Message}");
            return new List<YOLODetection>();
        }
    }
    #endregion

    #region Preprocessing (Python-Style)
    private Tensor<float> PreprocessImageLetterboxPythonStyle(Texture2D source, int debugIndex)
    {
        lastOriginalWidth = source.width;
        lastOriginalHeight = source.height;

        // Calculate scale (matches Python)
        lastScale = Mathf.Min((float)inputSize / source.width, (float)inputSize / source.height);

        int newWidth = Mathf.RoundToInt(source.width * lastScale);
        int newHeight = Mathf.RoundToInt(source.height * lastScale);

        // Calculate padding WITH PYTHON'S round(x - 0.1) logic
        float padWidth = (inputSize - newWidth) / 2f;
        float padHeight = (inputSize - newHeight) / 2f;

        lastPadTop = Mathf.RoundToInt(padHeight - 0.1f);
        lastPadLeft = Mathf.RoundToInt(padWidth - 0.1f);

        if (enableDebugLogs)
        {
            TaggedLogger.LogNEWYOLODetection($"[LETTERBOX] Original: {source.width}x{source.height}");
            TaggedLogger.LogNEWYOLODetection($"[LETTERBOX] Scale: {lastScale:F4}");
            TaggedLogger.LogNEWYOLODetection($"[LETTERBOX] Scaled size: {newWidth}x{newHeight}");
            TaggedLogger.LogNEWYOLODetection($"[LETTERBOX] Padding (left, top): ({lastPadLeft}, {lastPadTop})");
        }

        // Resize
        Texture2D scaledTexture = ResizeTextureHighQuality(source, newWidth, newHeight);
        debugSaver?.SaveImage(scaledTexture, "03_resized");

        // Letterbox with gray padding
        Texture2D letterboxedTexture = CreateLetterboxedTexturePythonStyle(scaledTexture, newWidth, newHeight);
        debugSaver?.SaveImage(letterboxedTexture, "04_letterboxed_model_input");

        // Convert to Tensor
        Tensor<float> inputTensor = TextureToTensorPythonStyle(letterboxedTexture);

#if UNITY_EDITOR
        DestroyImmediate(scaledTexture);
        DestroyImmediate(letterboxedTexture);
#else
        Destroy(scaledTexture);
        Destroy(letterboxedTexture);
#endif

        return inputTensor;
    }

    private Texture2D CreateLetterboxedTexturePythonStyle(Texture2D scaled, int scaledW, int scaledH)
    {
        Texture2D result = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);

        // Fill with gray (114, 114, 114) - YOLO standard
        Color32[] fill = new Color32[inputSize * inputSize];
        Color32 gray = new Color32(114, 114, 114, 255);
        for (int i = 0; i < fill.Length; i++) fill[i] = gray;
        result.SetPixels32(fill);

        // Place scaled image at (lastPadLeft, lastPadTop)
        result.SetPixels(lastPadLeft, lastPadTop, scaledW, scaledH, scaled.GetPixels());
        result.Apply();

        return result;
    }

    private Tensor<float> TextureToTensorPythonStyle(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels(); // Bottom-to-top in Unity
        int pixelCount = pixels.Length;
        float[] tensorData = new float[pixelCount * 3];

        // Python expects top-to-bottom, so we need to flip Y
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                // Flip Y coordinate to match Python (top-to-bottom)
                int srcY = inputSize - 1 - y;
                int srcIndex = srcY * inputSize + x;
                int dstIndex = y * inputSize + x;

                Color pixel = pixels[srcIndex];

                // NCHW: [R plane][G plane][B plane]
                tensorData[dstIndex] = pixel.r;                    // R
                tensorData[pixelCount + dstIndex] = pixel.g;       // G
                tensorData[pixelCount * 2 + dstIndex] = pixel.b;   // B
            }
        }

        return new Tensor<float>(new TensorShape(1, 3, inputSize, inputSize), tensorData);
    }
    #endregion

    #region Postprocessing (Parsing & NMS)
    private List<YOLODetection> PostProcessOutput(Tensor<float> output, int screenWidth, int screenHeight)
    {
        var detections = new List<YOLODetection>();

        int numClasses = currentClassNames.Length;
        int numBoxes = output.shape[2];

        TaggedLogger.LogNEWYOLODetection($" [POSTPROCESS] Output shape: {output.shape}");
        TaggedLogger.LogNEWYOLODetection($" [POSTPROCESS] NumClasses: {numClasses}, NumBoxes: {numBoxes}");

        var rawCandidates = new List<(int boxIdx, int classIdx, float conf, float cx, float cy, float w, float h)>();

        for (int boxIdx = 0; boxIdx < numBoxes; boxIdx++)
        {
            float maxConf = 0f;
            int maxClass = -1;

            for (int c = 0; c < numClasses; c++)
            {
                float conf = output[0, 4 + c, boxIdx];
                if (conf > maxConf)
                {
                    maxConf = conf;
                    maxClass = c;
                }
            }

            // Capture raw candidates regardless of threshold for debugging
            float cx = output[0, 0, boxIdx];
            float cy = output[0, 1, boxIdx];
            float w = output[0, 2, boxIdx];
            float h = output[0, 3, boxIdx];
            rawCandidates.Add((boxIdx, maxClass, maxConf, cx, cy, w, h));

            if (maxConf < confidenceThreshold) continue;

            // Extract box (already have these values from above)
            float cx_det = cx;
            float cy_det = cy;
            float w_det = w;
            float h_det = h;

            // Convert to top-left corner
            float x = cx_det - (w_det / 2f);
            float y = cy_det - (h_det / 2f);

            // Reverse letterboxing using lastPadLeft and lastPadTop
            x = (x - lastPadLeft) / lastScale;
            y = (y - lastPadTop) / lastScale;
            w_det = w_det / lastScale;
            h_det = h_det / lastScale;

            // Clamp to original image
            x = Mathf.Clamp(x, 0, lastOriginalWidth);
            y = Mathf.Clamp(y, 0, lastOriginalHeight);

            // Calculate center in image space
            float imageCenterX = x + (w_det / 2f);
            float imageCenterY = y + (h_det / 2f);

            // Convert to screen space (flip Y-axis: image Y-down → screen Y-up)
            float screenX = (imageCenterX / lastOriginalWidth) * screenWidth;
            float screenY = screenHeight - ((imageCenterY / lastOriginalHeight) * screenHeight);

            // Normalize bounding box to 0-1 range
            Rect normalizedBox = new Rect(
                x / lastOriginalWidth,
                y / lastOriginalHeight,
                w_det / lastOriginalWidth,
                h_det / lastOriginalHeight
            );

            detections.Add(new YOLODetection(
                currentClassNames[maxClass],        // objectName
                new Vector2(screenX, screenY),      // centerPosition (screen space)
                normalizedBox,                      // boundingBox (normalized 0-1)
                maxConf                             // confidence
            ));
        }

        // Sort and display top 20 raw candidates
        rawCandidates.Sort((a, b) => b.conf.CompareTo(a.conf));
        TaggedLogger.LogNEWYOLODetection(" Top 20 RAW candidates (before NMS):");
        TaggedLogger.LogNEWYOLODetection("═══════════════════════════════════════");
        for (int i = 0; i < Mathf.Min(20, rawCandidates.Count); i++)
        {
            var c = rawCandidates[i];
            TaggedLogger.LogNEWYOLODetection($"{i + 1:D2}. {currentClassNames[c.classIdx],-12} | Conf: {c.conf:F4} | " +
                    $"cx={c.cx:F1}, cy={c.cy:F1}, w={c.w:F1}, h={c.h:F1}");
        }

        TaggedLogger.LogNEWYOLODetection($"\n Candidates >= conf {confidenceThreshold}: {detections.Count}");

        var nmsResults = NMS(detections);

        TaggedLogger.LogNEWYOLODetection($" Post-NMS detections: {nmsResults.Count}");
        TaggedLogger.LogNEWYOLODetection("═══════════════════════════════════════");
        for (int i = 0; i < Mathf.Min(20, nmsResults.Count); i++)
        {
            var d = nmsResults[i];
            TaggedLogger.LogNEWYOLODetection($"{i + 1:D2}. {d.objectName,-12} | Conf: {d.confidence:F4} | " +
                    $"Screen: ({d.centerPosition.x:F1}, {d.centerPosition.y:F1}) | " +
                    $"NormBox: ({d.boundingBox.x:F3}, {d.boundingBox.y:F3}, {d.boundingBox.width:F3}, {d.boundingBox.height:F3})");
        }

        return nmsResults;
    }

    private List<YOLODetection> NMS(List<YOLODetection> input)
    {
        var sorted = input.OrderByDescending(x => x.confidence).ToList();
        var result = new List<YOLODetection>();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            // Remove overlapping boxes
            sorted.RemoveAll(x =>
                x.objectName == best.objectName &&
                GetIoU(best.boundingBox, x.boundingBox) > iouThreshold
            );
        }

        return result;
    }

    private float GetIoU(Rect a, Rect b)
    {
        float intersection = Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
        float union = (a.width * a.height) + (b.width * b.height) - intersection;
        return (union > 0) ? intersection / union : 0;
    }
    #endregion

    #region Helpers (Texture & Files)
    private Texture2D ResizeTextureHighQuality(Texture2D source, int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
    #endregion

    #region Lifecycle
    void OnDestroy()
    {
        worker?.Dispose();
    }
    #endregion
}