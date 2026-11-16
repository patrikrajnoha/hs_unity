using UnityEngine;

/// <summary>
/// Optional, reusable framing target for an NPC conversation camera.
/// Put it on the NPC root or on a child and assign a head/face transform when
/// the model has a reliable bone. When no target is assigned, this component's
/// own transform is used.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("HighStakes/NPC/Dialogue Camera Target")]
public sealed class DialogueCameraTarget : MonoBehaviour
{
    [Tooltip("Transform to frame, usually a head or face bone. If empty, this component's transform is used.")]
    public Transform target;

    [Tooltip("Additional world-space vertical offset applied to the assigned target.")]
    public float verticalOffset;

    public Transform ResolvedTarget => target != null ? target : transform;
}
