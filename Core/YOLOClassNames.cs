using UnityEngine;

[CreateAssetMenu(fileName = "YOLOClassNames", menuName = "YOLO/Class Names", order = 1)]
public class YOLOClassNames : ScriptableObject
{
    [Header("Custom Model Classes")]
    [Tooltip("Enter your class names, one per line")]
    [TextArea(5, 20)]
    public string classNamesText = "key\nknife\nscissors\nscrewdriver";
    
    [Header("Auto-Generated")]
    [SerializeField]
    private string[] cachedClassNames;
    
    public string[] GetClassNames()
    {
        if (cachedClassNames == null || cachedClassNames.Length == 0)
        {
            cachedClassNames = classNamesText.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            // Trim whitespace
            for (int i = 0; i < cachedClassNames.Length; i++)
            {
                cachedClassNames[i] = cachedClassNames[i].Trim();
            }
        }
        return cachedClassNames;
    }
    
    public int GetClassCount()
    {
        return GetClassNames().Length;
    }
    
    private void OnValidate()
    {
        // Update cached names when text changes
        cachedClassNames = null;
        GetClassNames();
    }
    
    // Editor testing
    #if UNITY_EDITOR
    public void SetClassNamesForTesting(string[] names)
    {
        // Update the text field
        classNamesText = string.Join("\n", names);
        
        // Clear cache to force regeneration
        cachedClassNames = null;
        
        // Regenerate
        GetClassNames();
        
        // Mark as dirty
        UnityEditor.EditorUtility.SetDirty(this);
    }
    #endif
}