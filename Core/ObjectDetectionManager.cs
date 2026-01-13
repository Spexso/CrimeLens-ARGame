using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ObjectDetectionManager : MonoBehaviour
{
    public static ObjectDetectionManager Instance;

    [Header("Marker Settings")]
    [SerializeField] private GameObject markerPrefab;

    [Header("AR Components")]
    [SerializeField] private ARRaycastManager arRaycastManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private AROcclusionManager occlusionManager;

    private List<GameObject> activeMarkers = new List<GameObject>();
    private Dictionary<string, Vector3> detectedObjectPositions = new Dictionary<string, Vector3>();
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto-assign AR camera
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera != null)
            {
                TaggedLogger.LogDetection($"[ObjectDetection] Auto-assigned camera: {arCamera.name}");
            }
        }

        // Auto-assign AR raycast manager
        if (arRaycastManager == null)
        {
            arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
            if (arRaycastManager != null)
            {
                TaggedLogger.LogDetection($"[ObjectDetection] Auto-assigned ARRaycastManager");
            }
        }

        // Auto-assign occlusion manager
        if (occlusionManager == null)
        {
            occlusionManager = FindFirstObjectByType<AROcclusionManager>();
            if (occlusionManager != null)
            {
                TaggedLogger.LogDetection($"[ObjectDetection] Auto-assigned AROcclusionManager");
            }
        }
    }

    public void Start()
    {
        if (occlusionManager != null)
        {
            // Request environment depth
            occlusionManager.requestedEnvironmentDepthMode = UnityEngine.XR.ARSubsystems.EnvironmentDepthMode.Best;
            TaggedLogger.LogDetection("[ObjectDetection] Depth API requested (will check support at runtime)");
        }
        else
            TaggedLogger.LogDetection("[ObjectDetection]  No AROcclusionManager found, depth disabled");
    }

    public void PlaceMarkerFromYOLO(YOLODetection detection)
    {
        // Raycast from screen center instead of detection position
        Vector3 worldPosition = RaycastFromScreenCenter();

        if (worldPosition != Vector3.zero)
            CreateAndRegisterMarker(detection.objectName, worldPosition);
        else
            TaggedLogger.LogObjectDetectionManager($"[ObjectDetection] Could not find 3D position for {detection.objectName}");
    }

    // Raycasts from screen center to find AR plane
    private Vector3 RaycastFromScreenCenter()
    {
        if (arRaycastManager == null)
        {
            TaggedLogger.LogObjectDetectionManager("[ObjectDetection] ARRaycastManager not assigned!");
            return Vector3.zero;
        }

        // Get screen center point
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        // Perform AR raycast from center of screen
        if (arRaycastManager.Raycast(screenCenter, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            // Get the first hit point (closest plane)
            Pose hitPose = raycastHits[0].pose;
            TaggedLogger.LogDetection($"[ObjectDetection] Raycast HIT plane at {hitPose.position}");
            return hitPose.position;
        }

        // Fallback: Find nearest plane manually
        TaggedLogger.LogDetection("[ObjectDetection] Raycast MISSED, trying fallback");
        return GetNearestPlanePosition();
    }

    private Vector3 GetNearestPlanePosition()
    {
        ARPlane nearestPlane = FindNearestPlaneToCamera();

        if (nearestPlane != null)
        {
            // Place at nearest plane's center
            Vector3 position = nearestPlane.center;
            TaggedLogger.LogDetection($"[ObjectDetection] Using nearest plane at {position}");
            return position;
        }

        // Final fallback: place in front of camera
        if (arCamera != null)
        {
            Vector3 position = arCamera.transform.position + arCamera.transform.forward * 2f;
            TaggedLogger.LogDetection($"[ObjectDetection] No planes found, placing 2m ahead of camera");
            return position;
        }

        return Vector3.zero;
    }

    private ARPlane FindNearestPlaneToCamera()
    {
        if (arCamera == null)
            return null;

        ARPlane[] planes = FindObjectsByType<ARPlane>(FindObjectsSortMode.None);
        ARPlane nearest = null;
        float minDistance = float.MaxValue;

        Vector3 cameraPos = arCamera.transform.position;

        foreach (ARPlane plane in planes)
        {
            float distance = Vector3.Distance(plane.center, cameraPos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = plane;
            }
        }

        return nearest;
    }

    // public void PlaceMarkerFromYOLO(YOLODetection detection)
    // {
    //     // Convert 2D screen position to 3D world position
    //     Vector3 worldPosition = Convert2DTo3DPosition(detection.centerPosition);

    //     if (worldPosition != Vector3.zero)
    //         CreateAndRegisterMarker(detection.objectName, worldPosition);
    //     else
    //         TaggedLogger.LogObjectDetectionManager($"[ObjectDetection] Could not find 3D position for {detection.objectName}");
    // }

    // // Converts 2D screen coordinates to 3D world position using AR raycasting
    // private Vector3 Convert2DTo3DPosition(Vector2 screenPosition)
    // {
    //     if (arRaycastManager == null)
    //     {
    //         TaggedLogger.LogObjectDetectionManager("[ObjectDetection] ARRaycastManager not assigned!");
    //         return Vector3.zero;
    //     }

    //     // Perform AR raycast from screen point
    //     if (arRaycastManager.Raycast(screenPosition, raycastHits, TrackableType.PlaneWithinPolygon))
    //     {
    //         // Get the first hit point (closest surface)
    //         Pose hitPose = raycastHits[0].pose;
    //         return hitPose.position;
    //     }

    //     // Fallback: Use camera forward ray if no AR plane detected
    //     return GetFallback3DPosition(screenPosition);
    // }

    // private Vector3 GetFallback3DPosition(Vector2 screenPosition)
    // {
    //     if (arCamera == null)
    //     {
    //         TaggedLogger.LogObjectDetectionManager("[ObjectDetection] AR Camera not assigned!");
    //         return Vector3.zero;
    //     }

    //     // Try to get depth value from Depth API
    //     float depth = TryGetDepthAtScreenPosition(screenPosition);
    //     Ray ray = arCamera.ScreenPointToRay(screenPosition);

    //     if (depth > 0)
    //     {
    //         // SUCCESS: Use real depth value
    //         Vector3 position = ray.origin + ray.direction * depth;
    //         TaggedLogger.LogDetection($"[ObjectDetection] Using DEPTH API: {depth:F2}m");
    //         return PlaceOnNearestPlane(position);
    //     }
    //     else
    //     {
    //         // FALLBACK: Place on detected plane or at fixed height
    //         float defaultDistance = 2f;
    //         Vector3 position = ray.origin + ray.direction * defaultDistance;
    //         TaggedLogger.LogDetection($"[ObjectDetection] Using FALLBACK: {defaultDistance}m (no depth data)");
    //         return PlaceOnNearestPlane(position);
    //     }
    // }

    // private Vector3 PlaceOnNearestPlane(Vector3 targetPosition)
    // {
    //     // Try to find nearest plane
    //     ARPlane nearestPlane = FindNearestPlane(targetPosition);

    //     if (nearestPlane != null)
    //     {
    //         // Place marker ON the plane surface
    //         targetPosition.y = nearestPlane.transform.position.y + 0.1f; // Slightly above surface
    //         TaggedLogger.LogDetection($"[ObjectDetection] Placed on AR plane at Y={targetPosition.y:F2}");
    //     }
    //     else
    //     {
    //         // No plane found, use camera height as reference
    //         float cameraHeight = arCamera.transform.position.y;
    //         targetPosition.y = Mathf.Max(targetPosition.y, cameraHeight - 1.5f); // Max 1.5m below camera
    //         TaggedLogger.LogDetection($"[ObjectDetection] No plane found, using camera reference Y={targetPosition.y:F2}");
    //     }

    //     return targetPosition;
    // }

    // private ARPlane FindNearestPlane(Vector3 position)
    // {
    //     ARPlane[] planes = FindObjectsByType<ARPlane>(FindObjectsSortMode.None);
    //     ARPlane nearest = null;
    //     float minDistance = float.MaxValue;

    //     foreach (ARPlane plane in planes)
    //     {
    //         float distance = Vector3.Distance(plane.transform.position, position);
    //         if (distance < minDistance)
    //         {
    //             minDistance = distance;
    //             nearest = plane;
    //         }
    //     }

    //     return nearest;
    // }

    // private float TryGetDepthAtScreenPosition(Vector2 screenPosition)
    // {
    //     if (occlusionManager == null)
    //         return -1f;

    //     if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage depthImage))
    //         return -1f;

    //     try
    //     {
    //         // Convert screen position to normalized viewport coordinates (0-1)
    //         Vector2 normalizedPos = new Vector2(
    //             screenPosition.x / Screen.width,
    //             screenPosition.y / Screen.height
    //         );

    //         // Convert to depth texture coordinates
    //         int depthX = Mathf.RoundToInt(normalizedPos.x * (depthImage.width - 1));
    //         int depthY = Mathf.RoundToInt(normalizedPos.y * (depthImage.height - 1));

    //         // Clamp to valid range
    //         depthX = Mathf.Clamp(depthX, 0, depthImage.width - 1);
    //         depthY = Mathf.Clamp(depthY, 0, depthImage.height - 1);

    //         // Get depth plane data
    //         var plane = depthImage.GetPlane(0);
    //         int pixelIndex = depthY * depthImage.width + depthX;
    //         int byteIndex = pixelIndex * 2; // 16-bit = 2 bytes per pixel

    //         // Safety check
    //         if (byteIndex + 1 >= plane.data.Length)
    //             return -1f;

    //         // Read 16-bit depth value (in millimeters)
    //         ushort depthMm = (ushort)(plane.data[byteIndex] | (plane.data[byteIndex + 1] << 8));

    //         // Convert to meters
    //         float depthMeters = depthMm / 1000f;

    //         // Validate depth (ARCore valid range: 0.5m to 65m, most accurate 0.5m-5m)
    //         if (depthMeters >= 0.5f && depthMeters <= 65f)
    //             return depthMeters;

    //         return -1f;
    //     }
    //     catch (System.Exception e)
    //     {
    //         TaggedLogger.LogObjectDetectionManager($"[ObjectDetection] Depth sampling error: {e.Message}");
    //         return -1f;
    //     }
    //     finally
    //     {
    //         depthImage.Dispose();
    //     }
    // }

    private void CreateAndRegisterMarker(string objectName, Vector3 position)
    {
        GameObject marker = CreateMarker(objectName, position);
        activeMarkers.Add(marker);
        detectedObjectPositions[objectName] = position;

        TaggedLogger.LogObjectDetectionManager($"[ObjectDetection] Placed marker for '{objectName}' at {position}");
    }

    private GameObject CreateMarker(string objectName, Vector3 position)
    {
        GameObject marker;

        if (markerPrefab != null)
        {
            marker = Instantiate(markerPrefab, position, Quaternion.identity);
        }
        else
        {
            TaggedLogger.LogObjectDetectionManager("markerPrefab is null!");
            marker = null;
        }

        marker.name = $"Marker_{objectName}";
        marker.tag = "DetectedObject";
        marker.layer = LayerMask.NameToLayer("Default");

        // Add marker component
        DetectedObjectMarker markerComponent = marker.AddComponent<DetectedObjectMarker>();
        markerComponent.objectName = objectName;
        markerComponent.worldPosition = position;

        Debug.Log($"[ObjectDetection] GameObject.name = '{marker.name}'"); 
        Debug.Log($"[ObjectDetection] Component.objectName = '{markerComponent.objectName}'");
        Debug.Log($"[ObjectDetection] Comparison will use: '{markerComponent.objectName}' ✓");

        return marker;
    }

    public void ConfirmMarkers()
    {
        TaggedLogger.LogObjectDetectionManager($"[ObjectDetection] Confirmed {activeMarkers.Count} markers for investigation");
    }

    public void ClearAllMarkers()
    {
        foreach (GameObject marker in activeMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        activeMarkers.Clear();
        detectedObjectPositions.Clear();

        TaggedLogger.LogObjectDetectionManager("[ObjectDetection] Cleared all markers");
    }
}