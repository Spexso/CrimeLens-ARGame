using UnityEngine;

public abstract class ActivatableWidget : MonoBehaviour
{
    [Header("Widget Settings")]
    [SerializeField] protected bool blockRaycastsWhenInactive = true;
    [SerializeField] protected bool hideWhenInactive = true;
    
    protected CanvasGroup canvasGroup;
    protected bool isActive = false;
    
    // Which game state should this widget be active in
    public abstract GameState LinkedGameState { get; }
    
    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    // Called when widget becomes active (shown)
    public virtual void OnActivate()
    {
        isActive = true;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        
        gameObject.SetActive(true);
        
        // Override in child classes
        OnActivated();
    }
    
    // Called when widget becomes inactive (hidden)
    public virtual void OnDeactivate()
    {
        isActive = false;
        
        if (canvasGroup != null)
        {
            if (hideWhenInactive)
            {
                canvasGroup.alpha = 0f;
            }
            
            canvasGroup.interactable = false;
            
            if (blockRaycastsWhenInactive)
            {
                canvasGroup.blocksRaycasts = false;
            }
        }
        
        // Override in child classes
        OnDeactivated();
    }
    
    // Override these in child classes for custom behavior
    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }
    
    public bool IsActive => isActive;
}