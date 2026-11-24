using UnityEngine;
namespace HighStakes.City
{
    public sealed class CityActor : MonoBehaviour
    {
        public string id;
        public string displayName;
        public string defaultDialogue;
        public bool IsPerson { get; set; }
        internal float NextSaleOfferTime { get; set; }
    }
}
