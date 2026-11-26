using System;
using UnityEngine;

/// <summary>
/// Per-player wanted/heat state. PoliceBehavior only marks a frame as observed;
/// this component applies gain once in LateUpdate, preventing multiple police
/// officers from multiplying the heat gain in the same frame.
/// </summary>
public sealed class PoliceHeatController : MonoBehaviour
{
    [Header("Heat Tuning")]
    [SerializeField, Min(1f)] private float maxHeat = 100f;
    [SerializeField, Min(0f)] private float heatGainPerSecond = 28f;
    [SerializeField, Min(0f)] private float heatDecayPerSecond = 12f;
    [SerializeField, Min(0f)] private float decayDelay = 3f;
    [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.8f;
    [SerializeField] private bool resetAfterPursuit = true;

    private GameMaster gameMaster;
    private int lastObservedFrame = -1;
    private float timeSinceObservation;
    private float heat;
    private bool pursuitActive;

    public event Action<float> HeatChanged;
    public event Action<bool> PursuitStateChanged;

    public float Heat => heat;
    public float MaxHeat => maxHeat;
    public float NormalizedHeat => maxHeat <= 0f ? 0f : Mathf.Clamp01(heat / maxHeat);
    public float WarningThreshold01 => warningThreshold;
    public bool IsPursuitActive => pursuitActive;
    public bool IsSaleInProgress => gameMaster != null && gameMaster.IsPlayerSelling(gameObject);

    private void Awake()
    {
        NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();
        if (networkPlayer != null && !networkPlayer.isLocalPlayer)
        {
            enabled = false;
            return;
        }

        gameMaster = GameMaster.Instance != null ? GameMaster.Instance : FindAnyObjectByType<GameMaster>();
    }

    private void OnEnable()
    {
        PoliceHeatRegistry.Register(this);
    }

    private void OnDisable()
    {
        PoliceHeatRegistry.Unregister(this);
    }

    private void Start()
    {
        if (gameMaster == null)
            gameMaster = GameMaster.Instance != null ? GameMaster.Instance : FindAnyObjectByType<GameMaster>();

        RaiseHeatChanged();
    }

    private void LateUpdate()
    {
        if (GameMaster.GameOverActive)
            return;

        bool wasObservedThisFrame = lastObservedFrame == Time.frameCount && IsSaleInProgress;
        if (wasObservedThisFrame)
        {
            timeSinceObservation = 0f;
            if (!pursuitActive)
                SetHeat(heat + heatGainPerSecond * Time.deltaTime);
        }
        else if (!pursuitActive)
        {
            timeSinceObservation += Time.deltaTime;
            if (timeSinceObservation >= decayDelay)
                SetHeat(heat - heatDecayPerSecond * Time.deltaTime);
        }

        if (!pursuitActive && NormalizedHeat >= 1f)
            ActivatePursuit();
    }

    /// <summary>Called by PoliceBehavior after all visibility checks pass.</summary>
    public void ReportPoliceObservation(PoliceBehavior observer)
    {
        if (!enabled || observer == null || !IsSaleInProgress)
            return;

        // A frame is a single gameplay observation regardless of the number
        // of police officers that can currently see the same deal.
        lastObservedFrame = Time.frameCount;
    }

    public void EndPursuit()
    {
        pursuitActive = false;
        timeSinceObservation = 0f;
        lastObservedFrame = -1;

        if (resetAfterPursuit)
            SetHeat(0f);

        PursuitStateChanged?.Invoke(false);
    }

    public void RegisterWitnessedCrime()
    {
        if (pursuitActive) return;
        SetHeat(maxHeat);
        pursuitActive = true;
        PursuitStateChanged?.Invoke(true);
    }

    private void ActivatePursuit()
    {
        if (gameMaster == null)
            gameMaster = GameMaster.Instance != null ? GameMaster.Instance : FindAnyObjectByType<GameMaster>();

        if (gameMaster == null || !gameMaster.BeginPoliceChase(gameObject))
            return;

        pursuitActive = true;
        PursuitStateChanged?.Invoke(true);
        Debug.Log($"[PoliceHeat] {name} reached 100% heat; police pursuit started.");
    }

    private void SetHeat(float value)
    {
        float next = Mathf.Clamp(value, 0f, maxHeat);
        if (Mathf.Approximately(next, heat))
            return;

        heat = next;
        RaiseHeatChanged();
    }

    private void RaiseHeatChanged()
    {
        HeatChanged?.Invoke(NormalizedHeat);
    }
}
