using UnityEngine;
using System.Collections.Generic;

public enum WidgetLayer
{
    Base = 0,      // Login, main screens
    Gameplay = 1,  // Scanning, Investigation
    Overlay = 2,   // Loading, Results
    Modal = 3      // Errors, Confirmations
}

public class WidgetStack : MonoBehaviour
{
    [Header("Layer Settings")]
    [SerializeField] private WidgetLayer layer;
    
    private Stack<ActivatableWidget> widgetStack = new Stack<ActivatableWidget>();
    private ActivatableWidget currentWidget;
    
    public WidgetLayer Layer => layer;
    
    // Push widget to stack
    public void Push(ActivatableWidget widget)
    {
        // Deactivate current widget
        if (currentWidget != null)
        {
            currentWidget.OnDeactivate();
        }
        
        // Push to stack
        widgetStack.Push(widget);
        currentWidget = widget;
        
        // Activate new widget
        currentWidget.OnActivate();
        
        Debug.Log($"[WidgetStack:{layer}] Pushed {widget.name}, Stack depth: {widgetStack.Count}");
    }
    
    // Pop widget from stack
    public void Pop()
    {
        if (widgetStack.Count == 0)
        {
            Debug.LogWarning($"[WidgetStack:{layer}] Cannot pop, stack is empty");
            return;
        }
        
        // Deactivate and remove current
        if (currentWidget != null)
        {
            currentWidget.OnDeactivate();
        }
        
        widgetStack.Pop();
        
        // Activate previous widget
        if (widgetStack.Count > 0)
        {
            currentWidget = widgetStack.Peek();
            currentWidget.OnActivate();
        }
        else
        {
            currentWidget = null;
        }
        
        Debug.Log($"[WidgetStack:{layer}] Popped, Stack depth: {widgetStack.Count}");
    }
    
    // Clear entire stack
    public void Clear()
    {
        while (widgetStack.Count > 0)
        {
            Pop();
        }
    }
    
    // Get current active widget
    public ActivatableWidget GetActiveWidget()
    {
        return currentWidget;
    }
    
    public int GetStackDepth()
    {
        return widgetStack.Count;
    }
}