using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Retro PS2-style minimap frame. It draws the bevelled metal rings, the four
/// cardinal braces and the heat segments in one UI mesh so the heat meter is
/// part of the frame rather than a separate modern progress bar.
/// </summary>
public sealed class MinimapFrameGraphic : Graphic
{
    private PoliceHeatController heatController;
    private float normalizedHeat;
    private bool pursuitActive;

    private readonly Color outerShadow = new Color(0.035f, 0.045f, 0.045f, 1f);
    private readonly Color outerMetal = new Color(0.42f, 0.46f, 0.45f, 1f);
    private readonly Color metalHighlight = new Color(0.74f, 0.77f, 0.73f, 1f);
    private readonly Color metalMid = new Color(0.29f, 0.32f, 0.31f, 1f);
    private readonly Color separator = new Color(0.08f, 0.10f, 0.10f, 1f);
    private readonly Color heatBlue = new Color(0.12f, 0.33f, 0.63f, 1f);
    private readonly Color heatLight = new Color(0.78f, 0.84f, 0.78f, 1f);
    private readonly Color heatYellow = new Color(0.92f, 0.68f, 0.08f, 1f);
    private readonly Color heatRed = new Color(0.64f, 0.08f, 0.06f, 1f);

    public void Configure(PoliceHeatController controller)
    {
        heatController = controller;
        SetState();
    }

    public void SetState()
    {
        if (heatController != null)
        {
            normalizedHeat = heatController.NormalizedHeat;
            pursuitActive = heatController.IsPursuitActive;
        }

        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetState();
    }

    private void Update()
    {
        if (heatController == null)
            return;

        normalizedHeat = heatController.NormalizedHeat;
        pursuitActive = heatController.IsPursuitActive;
        if (pursuitActive || normalizedHeat >= heatController.WarningThreshold01)
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        Vector2 center = rect.center;

        DrawRing(vertexHelper, center, radius * 0.89f, radius * 0.995f, 0f, 360f, outerShadow, 72);
        DrawRing(vertexHelper, center, radius * 0.84f, radius * 0.89f, 0f, 360f, metalHighlight, 72);
        DrawRing(vertexHelper, center, radius * 0.77f, radius * 0.84f, 0f, 360f, outerMetal, 72);
        DrawRing(vertexHelper, center, radius * 0.715f, radius * 0.77f, 0f, 360f, metalMid, 72);
        DrawRing(vertexHelper, center, radius * 0.675f, radius * 0.715f, 0f, 360f, separator, 72);

        // A narrow highlight on the upper-left side gives the frame a worn,
        // moulded-metal look instead of a flat vector ring.
        DrawArc(vertexHelper, center, radius * 0.84f, radius * 0.875f, 105f, 225f, new Color(0.9f, 0.91f, 0.86f, 0.72f), 24);
        DrawArc(vertexHelper, center, radius * 0.72f, radius * 0.755f, 285f, 45f, new Color(0.02f, 0.025f, 0.025f, 0.72f), 20);

        DrawHeat(vertexHelper, center, radius);
        DrawCardinalBrace(vertexHelper, center, radius, 90f);
        DrawCardinalBrace(vertexHelper, center, radius, 0f);
        DrawCardinalBrace(vertexHelper, center, radius, 270f);
        DrawCardinalBrace(vertexHelper, center, radius, 180f);
    }

    private void DrawHeat(VertexHelper vertexHelper, Vector2 center, float radius)
    {
        float inner = radius * 0.895f;
        float outer = radius * 0.965f;

        if (pursuitActive || normalizedHeat >= 0.999f)
        {
            bool bluePhase = Mathf.FloorToInt(Time.unscaledTime * 7f) % 2 == 0;
            int count = 12;
            for (int i = 0; i < count; i++)
            {
                float start = 90f - i * 30f + 2f;
                float end = 90f - (i + 1) * 30f + 1f;
                DrawArc(vertexHelper, center, inner, outer, start, end, (i % 2 == 0) == bluePhase ? heatBlue : heatRed, 4);
            }
            return;
        }

        // Small reference cap visible at zero heat: blue / pale / red.
        DrawArc(vertexHelper, center, inner, outer, 103f, 91f, heatRed, 4);
        DrawArc(vertexHelper, center, inner, outer, 91f, 79f, heatLight, 4);
        DrawArc(vertexHelper, center, inner, outer, 79f, 67f, heatBlue, 4);

        if (normalizedHeat <= 0.001f)
            return;

        float endAngle = 90f - normalizedHeat * 360f;
        Color fillColor = normalizedHeat >= 0.8f ? heatRed : normalizedHeat >= 0.35f ? heatYellow : heatLight;
        DrawArc(vertexHelper, center, inner, outer, 90f, endAngle, fillColor, Mathf.Max(4, Mathf.CeilToInt(normalizedHeat * 48f)));
    }

    private void DrawCardinalBrace(VertexHelper vertexHelper, Vector2 center, float radius, float angle)
    {
        Vector2 radial = Direction(angle);
        Vector2 tangent = new Vector2(-radial.y, radial.x);
        float inner = radius * 0.67f;
        float outer = radius * 0.98f;
        float width = radius * 0.035f;

        Vector2 a = center + radial * inner + tangent * width;
        Vector2 b = center + radial * outer + tangent * width;
        Vector2 c = center + radial * outer - tangent * width;
        Vector2 d = center + radial * inner - tangent * width;
        AddQuad(vertexHelper, a, b, c, d, separator);

        Vector2 offset = tangent * (width * 0.38f);
        AddQuad(vertexHelper, a + offset, b + offset, c + offset, d + offset, metalHighlight);
    }

    private static void DrawRing(VertexHelper vh, Vector2 center, float inner, float outer, float start, float end, Color color, int steps)
    {
        DrawArc(vh, center, inner, outer, start, end, color, steps);
    }

    private static void DrawArc(VertexHelper vh, Vector2 center, float inner, float outer, float start, float end, Color color, int steps)
    {
        steps = Mathf.Max(1, steps);
        for (int i = 0; i < steps; i++)
        {
            float t0 = i / (float)steps;
            float t1 = (i + 1) / (float)steps;
            float a0 = start + (end - start) * t0;
            float a1 = start + (end - start) * t1;
            Vector2 p0 = center + Direction(a0) * inner;
            Vector2 p1 = center + Direction(a0) * outer;
            Vector2 p2 = center + Direction(a1) * outer;
            Vector2 p3 = center + Direction(a1) * inner;
            AddQuad(vh, p0, p1, p2, p3, color);
        }
    }

    private static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = a;
        vh.AddVert(vertex);
        vertex.position = b;
        vh.AddVert(vertex);
        vertex.position = c;
        vh.AddVert(vertex);
        vertex.position = d;
        vh.AddVert(vertex);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }
}
