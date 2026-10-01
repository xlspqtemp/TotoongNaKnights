using UnityEngine;
using UnityEngine.UI;

public class CharacterPulse : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private float minAlpha = 0.6f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private float speed = 1.5f;

    private float gameplayElapsedTime;

    private void Update()
    {
        gameplayElapsedTime += GameplaySpeed.DeltaTime;
        float t = (Mathf.Sin(gameplayElapsedTime * speed) + 1f) / 2f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        Color c = targetImage.color;
        c.a = alpha;
        targetImage.color = c;
    }
}