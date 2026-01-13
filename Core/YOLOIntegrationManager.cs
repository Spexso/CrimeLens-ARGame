using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using TMPro;
using System;

public class YOLOIntegrationManager : MonoBehaviour
{
    public static YOLOIntegrationManager Instance;

    [Header("Components")]
    [SerializeField] public NewYOLOv8Detector yoloDetector;
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private TextMeshProUGUI debugText;
    private Texture2D _reusableTexture;

    [Header("Detection Settings")]
    [SerializeField] private int captureWidth = 512;
    [SerializeField] private int captureHeight = 512;

    private bool isScanning = false;
    private YOLODebugImageSaver debugSaver;

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        debugSaver = new YOLODebugImageSaver(true);
    }

    private void Start()
    {
        if (yoloDetector != null)
        {
            yoloDetector.SetDebugSaver(debugSaver);
        }
    }

    private void OnDestroy()
    {
        if (_reusableTexture != null) Destroy(_reusableTexture);
    }
    #endregion

    #region Public API
    public void StartYOLODetection()
    {
        if (isScanning) return;
        StartCoroutine(RunScanningSession());
    }
    #endregion

    #region Core Scanning Logic (POOLED APPROACH)
    private IEnumerator RunScanningSession()
    {
        isScanning = true;
        TaggedLogger.LogDetection("═══════════════════════════════════════");
        TaggedLogger.LogDetection($" [SCANNER] Looking for murder weapon (single frame test)...");

        if (!ValidateComponents())
        {
            isScanning = false;
            yield break;
        }

        // Single frame capture and detection
        debugSaver.StartNewCapture();
        Texture2D frame = CaptureCameraNew();

        if (frame == null)
        {
            TaggedLogger.LogDetectionWarning(" Camera buffer empty - scan failed");
            HandleFailedScan();
            isScanning = false;
            yield break;
        }

        // Run YOLO detection
        List<YOLODetection> detections = yoloDetector.DetectObjects(frame, frame.width, frame.height);

        // Cleanup frame immediately after detection
        Destroy(frame);

        // Log results
        TaggedLogger.LogDetection("═══════════════════════════════════════");
        TaggedLogger.LogDetection($" Detection complete. Found {detections.Count} objects");

        // Check if detections list is empty
        if (detections == null || detections.Count == 0)
        {
            TaggedLogger.LogDetectionWarning(" No objects detected - empty detection list");
            debugText.text = "No objects detected";
            HandleFailedScan();
            isScanning = false;
            yield break;
        }

        // Log all detections
        foreach (var d in detections)
        {
            TaggedLogger.LogDetection($"  • {d.objectName}: {d.confidence:P1}");
        }

        // Select best detection
        YOLODetection bestDetection = SelectBestFromPool(detections);

        if (bestDetection != null)
        {
            TaggedLogger.LogDetection($" Best detection: {bestDetection.objectName} ({bestDetection.confidence:P1})");
            debugText.text = $"{bestDetection.confidence:P1} {bestDetection.objectName}";
            HandleSuccessfulScan(bestDetection);
        }
        else
        {
            TaggedLogger.LogDetectionWarning(" No valid detection after filtering");
            debugText.text = "No valid detection";
            HandleFailedScan();
        }

        isScanning = false;
    }
    #endregion

    #region Pool Analysis
    private YOLODetection SelectBestFromPool(List<YOLODetection> allDetections)
    {
        // Group by object name and calculate statistics
        var groupedByObject = allDetections
            .GroupBy(d => d.objectName)
            .Select(group => new
            {
                ObjectName = group.Key,
                Count = group.Count(),
                AvgConfidence = group.Average(d => d.confidence),
                MaxConfidence = group.Max(d => d.confidence),
                BestDetection = group.OrderByDescending(d => d.confidence).First()
            })
            .OrderByDescending(g => g.Count)
            .ThenByDescending(g => g.AvgConfidence)
            .ToList();

        return groupedByObject.First().BestDetection;
    }
    #endregion

    #region Camera Handling
    private Texture2D CaptureCameraNew()
    {
        if (ARSession.state != ARSessionState.SessionTracking) return null;
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return null;

        try
        {
            // Validation
            if (image.width <= 0 || image.height <= 0) return null;
            int minDimension = Mathf.Min(image.width, image.height);
            if (minDimension <= 0) return null;

            // Calculate Dimensions
            int finalWidth = (minDimension < captureWidth) ? minDimension : captureWidth;
            int finalHeight = (minDimension < captureWidth) ? minDimension : captureHeight;

            // Crop logic
            int xOffset = (image.width - minDimension) / 2;
            int yOffset = (image.height - minDimension) / 2;
            var cropRect = new RectInt(xOffset, yOffset, minDimension, minDimension);

            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = cropRect,
                outputDimensions = new Vector2Int(finalWidth, finalHeight),
                outputFormat = TextureFormat.RGB24,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            // Texture Management
            if (_reusableTexture == null || _reusableTexture.width != finalWidth || _reusableTexture.height != finalHeight)
            {
                if (_reusableTexture != null) Destroy(_reusableTexture);
                _reusableTexture = new Texture2D(finalWidth, finalHeight, TextureFormat.RGB24, false);
            }

            // Convert
            image.Convert(conversionParams, _reusableTexture.GetRawTextureData<byte>());
            _reusableTexture.Apply();
            debugSaver.SaveImage(_reusableTexture, "01_camera_raw");

            return _reusableTexture;
        }
        catch (System.Exception ex)
        {
            TaggedLogger.LogDetectionError($"[YOLO] Conversion Error: {ex.Message}");
            return null;
        }
        finally
        {
            image.Dispose();
        }
    }
    #endregion

    #region Helpers (Private)
    private bool ValidateComponents()
    {
        if (yoloDetector == null || arCameraManager == null)
        {
            if (arCameraManager == null && Camera.main != null) arCameraManager = Camera.main.GetComponent<ARCameraManager>();
            if (arCameraManager == null)
            {
                TaggedLogger.LogDetectionError("[YOLO] Critical: Missing Components");
                return false;
            }
        }
        return true;
    }

    public void HandleSuccessfulScan(YOLODetection validDetection)
    {
        TaggedLogger.LogDetection($" [SCANNER] Detected: {validDetection.objectName}");

        // Check if already detected
        if (GameManager.Instance.HasDetectedObject(validDetection.objectName))
        {
            TaggedLogger.LogDetectionWarning($" [SCANNER] {validDetection.objectName} already detected, skipping");
            InvestigationManager.Instance?.OnSingleScanComplete(); // Re-enable scan button
            return;
        }

        TaggedLogger.LogDetection($" [SCANNER] New object found: {validDetection.objectName}! Placing marker.");
        ObjectDetectionManager.Instance.PlaceMarkerFromYOLO(validDetection);
        GameManager.Instance.OnScanComplete(validDetection);
        InvestigationManager.Instance?.OnSingleScanComplete();
    }

    private void HandleFailedScan()
    {
        TaggedLogger.LogDetectionWarning(" [SCANNER] Timed out. No murder weapon detected.");
        InvestigationManager.Instance?.OnScanFailed("Could not detect murder weapon. Try scanning again!.");
    }
    #endregion
}