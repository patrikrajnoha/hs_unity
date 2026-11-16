using UnityEngine;

/// <summary>
/// Keeps a moving NPC visual planted on the first scene surface below it.
/// This is intentionally separate from NavMeshAgent and the NPC collider so
/// it cannot change navigation or interaction behaviour.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("HighStakes/NPC/Visual Grounding")]
public sealed class NPCVisualGrounding : MonoBehaviour
{
    [SerializeField, Min(0f)] private float footClearance = 0.01f;
    [SerializeField, Min(0.1f)] private float rayStartHeight = 2.5f;
    [SerializeField, Min(0.1f)] private float rayDistance = 5f;

    private Renderer[] renderers;
    private Transform ownerRoot;
    private float footOffset;
    private bool calibrated;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        ownerRoot = transform.parent;
    }

    private void LateUpdate()
    {
        if (ownerRoot == null || renderers == null || renderers.Length == 0)
            return;

        float groundY;
        if (!TryFindGround(out groundY))
            return;

        float visualMinY = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                visualMinY = Mathf.Min(visualMinY, renderers[i].bounds.min.y);
        }

        if (float.IsPositiveInfinity(visualMinY))
            return;

        if (!calibrated)
        {
            footOffset = visualMinY - transform.position.y;
            calibrated = true;
        }
        // Keep a stable sole offset. Animated renderer bounds change as knees
        // bend and previously caused the entire visual to bounce each frame.
        float correction = groundY + footClearance - footOffset - transform.position.y;
        if (Mathf.Abs(correction) > 0.001f)
            transform.position += Vector3.up * Mathf.Clamp(correction, -0.5f, 0.5f);
    }

    private bool TryFindGround(out float groundY)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            transform.position + Vector3.up * rayStartHeight,
            Vector3.down,
            rayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        groundY = 0f;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.transform == ownerRoot || collider.transform.IsChildOf(ownerRoot))
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                groundY = hits[i].point.y;
                found = true;
            }
        }

        return found;
    }
}
