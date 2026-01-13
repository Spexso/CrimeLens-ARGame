using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Collections.Generic;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.EventSystems;

public class ARPlacementTest : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;

    [Header("Prefab to Place")]
    [SerializeField] private GameObject objectPrefab;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private GameObject spawnedObject;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        // Initialize AR components if not assigned
        if (raycastManager == null)
            raycastManager = FindFirstObjectByType<ARRaycastManager>();

        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>();
    }

    void Update()
    {
        // Check for touch input using new Input System
        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                PlaceObject(touch.screenPosition);
            }
        }

        // For testing in Unity Editor (mouse click)
#if UNITY_EDITOR
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            PlaceObject(mousePosition);
        }
#endif
    }

    void PlaceObject(Vector2 screenPosition)
    {
        // Check if touching UI first
        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(touch.touchId))
            {
                return; // Don't place if over UI
            }
        }

        // Raycast against AR planes
        if (raycastManager != null &&
            raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null && objectPrefab != null)
            {
                // Spawn new object
                spawnedObject = Instantiate(objectPrefab, hitPose.position, hitPose.rotation);
                TaggedLogger.LogAR("Object placed at: " + hitPose.position);
            }
            else if (spawnedObject != null)
            {
                // Move existing object
                spawnedObject.transform.position = hitPose.position;
                spawnedObject.transform.rotation = hitPose.rotation;
                TaggedLogger.LogAR("Object moved to: " + hitPose.position);
            }
            else
            {
                TaggedLogger.LogAR("No prefab assigned to place!");
            }
        }
    }
}
