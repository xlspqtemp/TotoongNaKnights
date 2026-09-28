using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TacticalOrdersController : MonoBehaviour
{
    private const float DefaultCooldownDuration = 15f;
    private const string OrderButtonSuffix = " Order";

    [SerializeField] private CirculatorySystemController circulatorySystemController;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float cooldownDuration = DefaultCooldownDuration;

    private void Start()
    {
        if (circulatorySystemController == null)
        {
            Debug.LogError("TacticalOrdersController requires a CirculatorySystemController reference.", this);
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("TacticalOrdersController requires at least one spawn point.", this);
            return;
        }

        foreach (Transform spawnPoint in spawnPoints)
        {
            ConfigureButton(spawnPoint);
        }
    }

    private void ConfigureButton(Transform spawnPoint)
    {
        if (spawnPoint == null)
        {
            return;
        }

        Transform buttonTransform = transform.Find(spawnPoint.name + OrderButtonSuffix);
        if (buttonTransform == null)
        {
            Debug.LogError($"Could not find an order button for spawn point '{spawnPoint.name}'.", this);
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        TextMeshProUGUI label = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        if (button == null || label == null)
        {
            Debug.LogError($"Order button '{buttonTransform.name}' requires a Button and a child TextMeshProUGUI component.", buttonTransform);
            return;
        }

        label.text = spawnPoint.name;
        button.onClick.AddListener(() => SpawnPair(button, label, spawnPoint));
    }

    private void SpawnPair(Button button, TextMeshProUGUI label, Transform spawnPoint)
    {
        if (!button.interactable || !circulatorySystemController.SpawnPairAt(spawnPoint))
        {
            return;
        }

        StartCoroutine(CooldownRoutine(button, label, spawnPoint.name));
    }

    private IEnumerator CooldownRoutine(Button button, TextMeshProUGUI label, string spawnPointName)
    {
        button.interactable = false;
        float remainingTime = Mathf.Max(0f, cooldownDuration);

        while (remainingTime > 0f)
        {
            label.text = $"{spawnPointName}\n{Mathf.CeilToInt(remainingTime)}s";
            yield return null;
            remainingTime -= Time.deltaTime;
        }

        label.text = spawnPointName;
        button.interactable = true;
    }
}
