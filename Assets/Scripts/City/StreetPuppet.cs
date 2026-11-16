using UnityEngine;

namespace HighStakes.City
{
    // Original articulated fallback: works without external FBX files or Animator clips.
    // A valid imported humanoid can replace PlayerVisual without changing gameplay.
    public sealed class StreetPuppet : MonoBehaviour
    {
        public Transform leftArm, rightArm, leftLeg, rightLeg, body;
        private Vector3 lastPosition;
        private float phase, speed;
        private Transform owner;
        private CharacterController character;
        private void OnEnable()
        {
            owner = transform.parent;
            if (owner == null) owner = transform;
            character = owner.GetComponent<CharacterController>();
            lastPosition = owner.position;
        }
        private void LateUpdate()
        {
            float delta = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 displacement = owner.position - lastPosition;
            lastPosition = owner.position;
            float measured = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude / delta;
            if (measured > 20f) measured = 0f;
            speed = Mathf.Lerp(speed, measured, 1f - Mathf.Exp(-12f * delta));
            phase += speed * delta * 3.8f;
            float amplitude = Mathf.Min(speed / 4f, 1f) * 36f;
            bool airborne = character != null && !character.isGrounded;
            float swing = Mathf.Sin(phase) * amplitude;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(airborne ? -48f : -swing, 0f, -6f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(airborne ? -48f : swing, 0f, 6f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(airborne ? -24f : swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(airborne ? 18f : -swing, 0f, 0f);
            if (body != null) body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * Mathf.Min(speed, 4f) * 0.012f);
        }
    }
}
