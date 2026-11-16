using UnityEngine;

/// <summary>Presentation-only bridge between the heat state and the minimap frame mesh.</summary>
public sealed class PoliceHeatHUD : MonoBehaviour
{
    private PoliceHeatController controller;
    private MinimapFrameGraphic frameGraphic;

    public void Configure(PoliceHeatController source, MinimapFrameGraphic frame)
    {
        controller = source;
        frameGraphic = frame;
        frameGraphic?.Configure(source);
    }

    private void Update()
    {
        if (controller == null || frameGraphic == null)
            return;

        frameGraphic.SetState();
    }
}
