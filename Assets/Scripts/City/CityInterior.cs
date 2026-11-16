using UnityEngine;

namespace HighStakes.City
{
    // Bounds are queried rather than triggers, avoiding trigger/Rigidbody setup dependencies.
    public sealed class CityInterior : MonoBehaviour
    {
        public string roomName;
        public Vector3 size;
        public bool Contains(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            return Mathf.Abs(local.x) < size.x * 0.5f && Mathf.Abs(local.z) < size.z * 0.5f && local.y >= -0.5f && local.y < 3.8f;
        }
        public static string RoomAt(Vector3 position)
        {
            foreach (var room in FindObjectsByType<CityInterior>()) if (room.Contains(position)) return room.roomName;
            return "Old Quarter / Streets";
        }
    }
}
