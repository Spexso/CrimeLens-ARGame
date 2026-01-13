using UnityEngine;

public class BillboardMarker : MonoBehaviour
{
    private Camera arCamera;
    
    void Start()
    {
        // Find AR camera
        arCamera = Camera.main;
        
        if (arCamera == null)
        {
            Debug.LogWarning("[Billboard] Main camera not found!");
        }
    }
    
    void LateUpdate()
    {
        if (arCamera == null) return;
        
        // Make icon face camera (billboard effect)
        transform.LookAt(transform.position + arCamera.transform.rotation * Vector3.forward,
                        arCamera.transform.rotation * Vector3.up);
    }
}