using System;
using UnityEngine;

namespace HighStakes.City
{
    [DisallowMultipleComponent]
    public sealed class PlayerProductEffects : MonoBehaviour
    {
        internal enum Effect { Concealment, WiderView, Sprint, Walk, Count }

        internal const float Duration = 180f;
        private readonly ActiveEffect[] active = new ActiveEffect[(int)Effect.Count];
        private Camera playerCamera;
        private float baseFieldOfView;
        private bool fieldOfViewWidened;

        private struct ActiveEffect
        {
            public double EndsAt;
            public float Potency;
        }

        public float PoliceSightMultiplier => 1f - 0.35f * Strength(Effect.Concealment);
        public float SprintSpeedMultiplier => 1f + 0.30f * Strength(Effect.Sprint);
        public float WalkSpeedMultiplier => 1f + 0.22f * Strength(Effect.Walk);

        private void Awake() => playerCamera = GetComponentInChildren<Camera>();

        internal bool TryConsume(Inventory inventory, Item product)
        {
            if (!isActiveAndEnabled || !ProductItemFactory.IsDrugProduct(product) ||
                inventory.GetItemAmount(product) < 1 || !inventory.RemoveItem(product))
                return false;

            string baseId = product.baseProductId;
            if (string.IsNullOrEmpty(baseId))
                baseId = string.IsNullOrEmpty(product.productId) ? product.name : product.productId;

            Effect effect = baseId switch
            {
                "GreenBud" => Effect.Concealment,
                "Mushroom" => Effect.WiderView,
                "CocaLeaf" => Effect.Sprint,
                _ => Effect.Walk
            };
            float qualityBonus = (ProductQualityRules.Multiplier(product.quality) - 1f) * 0.4f;
            float mixBonus = Mathf.Clamp(product.effectValue * 0.005f, 0f, 0.2f);
            active[(int)effect] = new ActiveEffect { EndsAt = Time.timeAsDouble + Duration, Potency = 1f + qualityBonus + mixBonus };
            var visuals = GetComponent<PlayerPerceptionEffects>();
            if (visuals == null) visuals = gameObject.AddComponent<PlayerPerceptionEffects>();
            visuals.Consume(baseId);
            return true;
        }

        internal float SecondsRemaining(Effect effect) => isActiveAndEnabled
            ? (float)Math.Max(0, active[(int)effect].EndsAt - Time.timeAsDouble)
            : 0f;

        private float Strength(Effect effect) => active[(int)effect].Potency * (SecondsRemaining(effect) / Duration);

        private void LateUpdate()
        {
            if (playerCamera == null) return;

            float widening = 14f * Strength(Effect.WiderView);
            if (widening <= 0)
            {
                RestoreFieldOfView();
                return;
            }

            if (!fieldOfViewWidened) baseFieldOfView = playerCamera.fieldOfView;
            fieldOfViewWidened = true;
            playerCamera.fieldOfView = baseFieldOfView + widening;
        }

        private void OnDisable() => RestoreFieldOfView();

        internal void ResetProductEffects()
        {
            Array.Clear(active, 0, active.Length);
            RestoreFieldOfView();
        }

        private void RestoreFieldOfView()
        {
            if (!fieldOfViewWidened) return;
            if (playerCamera != null) playerCamera.fieldOfView = baseFieldOfView;
            fieldOfViewWidened = false;
        }
    }
}
