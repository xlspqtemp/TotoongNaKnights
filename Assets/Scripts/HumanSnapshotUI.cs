using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HumanSnapshotUI : MonoBehaviour
{
    [SerializeField] private Image bodyStateImage;
    [SerializeField] private TextMeshProUGUI vitalsText;

    public Image BodyStateImage => bodyStateImage;
    public TextMeshProUGUI VitalsText => vitalsText;
}
