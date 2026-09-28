using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects the lymphatic HUD buttons to their corresponding cell spawners.
/// </summary>
public sealed class LymphaticOrdersController : MonoBehaviour
{
    private const string BCellButtonName = "B Cell Button";
    private const string TCellButtonName = "T Cell Button";
    private const string LabelObjectName = "Label";

    [SerializeField] private LymphaticSystemController lymphaticSystemController;

    private void Start()
    {
        if (lymphaticSystemController == null)
        {
            lymphaticSystemController = FindFirstObjectByType<LymphaticSystemController>();
        }

        if (lymphaticSystemController == null)
        {
            Debug.LogError("LymphaticOrdersController requires a LymphaticSystemController reference.", this);
            return;
        }

        ConfigureButton(BCellButtonName, "B Cell", () => lymphaticSystemController.SpawnBCell());
        ConfigureButton(TCellButtonName, "T Cell", () => lymphaticSystemController.SpawnTCell());
    }

    private void ConfigureButton(string buttonName, string labelText, UnityEngine.Events.UnityAction spawnCell)
    {
        Transform buttonTransform = transform.Find(buttonName);
        if (buttonTransform == null)
        {
            Debug.LogError($"Could not find lymphatic order button '{buttonName}'.", this);
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        Transform labelTransform = buttonTransform.Find(LabelObjectName);
        TextMeshProUGUI label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
        if (button == null || label == null)
        {
            Debug.LogError($"Lymphatic order button '{buttonName}' requires a Button and a child TextMeshProUGUI component.", buttonTransform);
            return;
        }

        label.text = labelText;
        button.onClick.AddListener(spawnCell);
    }
}
