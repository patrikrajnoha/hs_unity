using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public sealed class FPSCounter : MonoBehaviour
{
    [SerializeField, Min(0.05f)]
    private float updateInterval = 0.5f;

    private TMP_Text fpsText;
    private float elapsedTime;
    private int renderedFrames;

    private void Awake()
    {
        fpsText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        elapsedTime = 0f;
        renderedFrames = 0;
        fpsText.SetText("-- FPS");
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;
        renderedFrames++;

        if (elapsedTime < updateInterval)
        {
            return;
        }

        float framesPerSecond = renderedFrames / elapsedTime;
        fpsText.SetText("{0:0} FPS", framesPerSecond);

        elapsedTime = 0f;
        renderedFrames = 0;
    }
}
