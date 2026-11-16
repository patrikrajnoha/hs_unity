using UnityEngine;
using UnityEngine.AI;

namespace HighStakes.City
{
    public sealed class CityDoor : MonoBehaviour
    {
        public Transform leaf;
        public int minimumLevel = 1;
        public string roomName;
        public bool IsOpen { get; private set; }
        private float angle;
        private float nextCrowdCheck;
        private Collider leafCollider;
        private void Start() { if (leaf != null) leafCollider = leaf.GetComponentInChildren<Collider>(); }
        public bool Toggle(int level)
        {
            if (level < minimumLevel) return false;
            IsOpen = !IsOpen;
            return true;
        }
        private void Update()
        {
            if (leaf == null) return;
            angle = Mathf.MoveTowards(angle, IsOpen ? -105f : 0, Time.deltaTime * 180);
            leaf.localRotation = Quaternion.Euler(0, angle, 0);
            // The frame is baked; the moving leaf remains a physical player barrier.
            if (leafCollider != null) leafCollider.enabled = angle > -80f;
            if (!IsOpen && Time.time >= nextCrowdCheck)
            {
                nextCrowdCheck = Time.time + 0.5f;
                foreach (var officer in PoliceHeatRegistry.ActivePolice)
                    if (officer != null && officer.isChasing && Vector3.Distance(officer.transform.position, transform.position) < 2.4f)
                    { IsOpen = true; break; }
            }
        }
    }
}
