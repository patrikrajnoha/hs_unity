using UnityEngine;

namespace HighStakes.City
{
    public static class CityInteractionTargeting
    {
        public static bool TryScore(Transform player, Camera camera, Transform target, float range, out float score)
        {
            score = float.MaxValue;
            if (camera == null || target == null) return false;
            Collider shape = target.GetComponent<Collider>();
            Vector3 point = shape != null ? shape.bounds.center : target.position;
            Vector3 offset = point - player.position;
            offset.y = 0;
            if (offset.magnitude > range) return false;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0 || Mathf.Abs(viewport.x - .5f) > .24f || Mathf.Abs(viewport.y - .5f) > .28f) return false;
            Vector3 origin = player.position + Vector3.up * 1.35f;
            Vector3 ray = point - origin;
            foreach (var hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == player || hit.transform.IsChildOf(player)) continue;
                if (hit.transform == target || hit.transform.IsChildOf(target) || target.IsChildOf(hit.transform)) continue;
                return false;
            }
            score = new Vector2(viewport.x - .5f, viewport.y - .5f).sqrMagnitude + offset.magnitude * .001f;
            return true;
        }
    }
}
