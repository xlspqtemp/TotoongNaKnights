using UnityEngine;

public class InteractionLogger : MonoBehaviour
{
    public void LogInteraction(string label)
    {
        if (ConsoleLogUI.Instance != null)
            ConsoleLogUI.Instance.Log($"{label} interacted with");
    }
}
