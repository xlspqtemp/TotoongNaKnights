using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Lets tutorial controls and the active action target receive raycasts while blocking all other input.</summary>
public sealed class TutorialInputGate : MonoBehaviour, ICanvasRaycastFilter
{
    private readonly List<RectTransform> allowedControls = new List<RectTransform>();
    private RectTransform activeTarget;

    /// <summary>Sets the only screen regions that may receive pointer input through the tutorial blocker.</summary>
    public void Configure(IReadOnlyList<RectTransform> cardControls, RectTransform target)
    {
        allowedControls.Clear();
        if (cardControls != null)
        {
            for (int index = 0; index < cardControls.Count; index++)
            {
                if (cardControls[index] != null)
                    allowedControls.Add(cardControls[index]);
            }
        }
        activeTarget = target;
    }

    /// <summary>Returns false only over tutorial controls or the active action target, allowing that hit to continue.</summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (Contains(allowedControls, screenPoint, eventCamera))
            return false;
        if (activeTarget != null && RectTransformUtility.RectangleContainsScreenPoint(activeTarget, screenPoint, eventCamera))
            return false;
        return true;
    }

    private static bool Contains(List<RectTransform> rectangles, Vector2 point, Camera eventCamera)
    {
        for (int index = 0; index < rectangles.Count; index++)
        {
            RectTransform rectangle = rectangles[index];
            if (rectangle != null && rectangle.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(rectangle, point, eventCamera))
                return true;
        }
        return false;
    }
}
