using UnityEngine;

public class DetectedObjectMarker : MonoBehaviour
{
    public string objectName;
    public Vector3 worldPosition;
    private Renderer markerRenderer;
    private Color originalColor;

    void Start()
    {
        markerRenderer = GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            originalColor = markerRenderer.material.color;
        }
    }

    public void OnMarkerTapped()
    {
        Debug.Log($"[Marker] {objectName} was tapped!");
        
        if (markerRenderer != null)
        {
            // Flash green briefly
            StartCoroutine(FlashColor(Color.green, 0.3f));
        }
    }

    private System.Collections.IEnumerator FlashColor(Color color, float duration)
    {
        markerRenderer.material.color = color;
        yield return new WaitForSeconds(duration);
        markerRenderer.material.color = originalColor;
    }
}