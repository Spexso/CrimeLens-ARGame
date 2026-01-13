using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem; // NEW

public class MarkerTouchHandler : MonoBehaviour
{
    [SerializeField] public Camera arCamera;

    void Start()
    {
        if (arCamera == null)
        {
            arCamera = FindFirstObjectByType<ARCameraManager>()?.GetComponent<Camera>();
            Debug.Log($"[MarkerTouch] AR Camera found: {arCamera != null}");

            if (arCamera == null)
                Debug.LogError("[MarkerTouch] AR Camera is NULL! Touch detection will fail.");
        }
    }

    void Update()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            // Only process on touch began (not drag)
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                HandleTouch(touchPosition);
            }
        }

        // EDITOR TESTING
#if UNITY_EDITOR
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            HandleTouch(mousePosition);
        }
#endif
    }

    void HandleTouch(Vector2 screenPosition)
    {
        Debug.Log($"[MarkerTouch] Touch detected at screen position: {screenPosition}");

        if (arCamera == null)
        {
            Debug.LogError("[MarkerTouch] AR Camera is NULL - cannot create ray!");
            return;
        }

        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        Debug.Log($"[MarkerTouch] Ray created - Origin: {ray.origin}, Direction: {ray.direction}");

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            Debug.Log($"[MarkerTouch] ✓ Raycast HIT!");
            Debug.Log($"[MarkerTouch]   - Object: {hit.collider.gameObject.name}");
            Debug.Log($"[MarkerTouch]   - Tag: {hit.collider.gameObject.tag}");
            Debug.Log($"[MarkerTouch]   - Distance: {hit.distance:F2}m");

            DetectedObjectMarker marker = hit.collider.GetComponent<DetectedObjectMarker>();

            if (marker == null)
            {
                Debug.Log($"[MarkerTouch] No marker on collider, checking parent...");
                marker = hit.collider.GetComponentInParent<DetectedObjectMarker>();
            }

            if (marker != null)
            {
                Debug.Log($"[MarkerTouch] ✓✓ SUCCESS - Tapped marker: {marker.objectName}");
                GameManager.Instance?.OnMarkerClicked(marker.objectName);
            }
            else
            {
                Debug.LogWarning($"[MarkerTouch] Hit object has no DetectedObjectMarker component in hierarchy");
            }
        }
        else
        {
            Debug.Log($"[MarkerTouch] ✗ Raycast MISSED");
        }
    }
}