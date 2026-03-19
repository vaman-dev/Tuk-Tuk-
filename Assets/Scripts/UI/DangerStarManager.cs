using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DangerStarManager : MonoBehaviour
{
    public static DangerStarManager Instance { get; private set; }

    [Header("Star Settings")]
    [SerializeField] private int maxStarLevel = 5;

    [Header("Heat Thresholds (per star level)")]
    [SerializeField] private float[] starThresholds = new float[] { 20f, 50f, 100f, 160f, 250f };

    [Header("Heat Settings")]
    [SerializeField] private float baseHeatPerHit = 25f;
    [SerializeField] private float velocityHeatMultiplier = 2f;
    [SerializeField] private float minImpactVelocity = 3f;

    [Header("Decay Settings")]
    [SerializeField] private float decayDelayAfterHit = 5f;
    [SerializeField] private float heatDecayPerSecond = 8f;

    [Header("Debug")]
    [SerializeField] private bool logHeatChanges = false;

    private float _currentHeat;
    private int _currentStarLevel;
    private float _lastHitTime;

    // ── Public Accessors ──
    public float CurrentHeat => _currentHeat;
    public int CurrentStarLevel => _currentStarLevel;
    public int MaxStarLevel => maxStarLevel;

    /// <summary>
    /// Fired when the star level changes. Passes (newLevel, maxLevel).
    /// </summary>
    public event Action<int, int> OnStarLevelChanged;

    /// <summary>
    /// Fired whenever heat value changes. Passes (currentHeat, thresholdForNextStar).
    /// </summary>
    public event Action<float, float> OnHeatChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DangerStarManager] Duplicate instance destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Validate thresholds array matches max star level
        if (starThresholds == null || starThresholds.Length != maxStarLevel)
        {
            Debug.LogWarning($"[DangerStarManager] starThresholds length ({starThresholds?.Length}) doesn't match maxStarLevel ({maxStarLevel}). Generating defaults.", this);
            starThresholds = new float[maxStarLevel];
            for (int i = 0; i < maxStarLevel; i++)
                starThresholds[i] = (i + 1) * 40f;
        }
    }

    private void Update()
    {
        HandleHeatDecay();
    }

    /// <summary>
    /// Called by VehicleNPCCollisionDetector when the vehicle hits an NPC.
    /// </summary>
    /// <param name="impactVelocity">The relative velocity magnitude of the collision.</param>
    public void ReportNPCHit(float impactVelocity)
    {
        if (impactVelocity < minImpactVelocity)
            return;

        float heatToAdd = baseHeatPerHit + (impactVelocity * velocityHeatMultiplier);
        AddHeat(heatToAdd);

        _lastHitTime = Time.time;

        if (logHeatChanges)
            Debug.Log($"[DangerStarManager] NPC hit! velocity={impactVelocity:F1} | heatAdded={heatToAdd:F1} | totalHeat={_currentHeat:F1} | stars={_currentStarLevel}", this);
    }

    /// <summary>
    /// Manually add heat (for external systems).
    /// </summary>
    public void AddHeat(float amount)
    {
        float maxHeat = starThresholds[maxStarLevel - 1];
        _currentHeat = Mathf.Clamp(_currentHeat + amount, 0f, maxHeat);

        _lastHitTime = Time.time;

        RecalculateStarLevel();
        BroadcastHeatChanged();
    }

    /// <summary>
    /// Manually remove heat.
    /// </summary>
    public void RemoveHeat(float amount)
    {
        _currentHeat = Mathf.Max(0f, _currentHeat - amount);

        RecalculateStarLevel();
        BroadcastHeatChanged();
    }

    /// <summary>
    /// Reset heat and stars to zero.
    /// </summary>
    public void ResetHeat()
    {
        _currentHeat = 0f;
        int previousLevel = _currentStarLevel;
        _currentStarLevel = 0;

        if (previousLevel != 0)
            OnStarLevelChanged?.Invoke(0, maxStarLevel);

        BroadcastHeatChanged();

        if (logHeatChanges)
            Debug.Log("[DangerStarManager] Heat reset to 0.", this);
    }

    private void HandleHeatDecay()
    {
        if (_currentHeat <= 0f)
            return;

        // Don't decay until the delay has passed since the last hit
        if (Time.time - _lastHitTime < decayDelayAfterHit)
            return;

        _currentHeat = Mathf.MoveTowards(_currentHeat, 0f, heatDecayPerSecond * Time.deltaTime);

        RecalculateStarLevel();
        BroadcastHeatChanged();
    }

    private void RecalculateStarLevel()
    {
        int newLevel = 0;

        for (int i = 0; i < starThresholds.Length; i++)
        {
            if (_currentHeat >= starThresholds[i])
                newLevel = i + 1;
            else
                break;
        }

        newLevel = Mathf.Clamp(newLevel, 0, maxStarLevel);

        if (newLevel != _currentStarLevel)
        {
            int previousLevel = _currentStarLevel;
            _currentStarLevel = newLevel;

            if (logHeatChanges)
                Debug.Log($"[DangerStarManager] Star level changed: {previousLevel} -> {newLevel} | heat={_currentHeat:F1}", this);

            OnStarLevelChanged?.Invoke(newLevel, maxStarLevel);
        }
    }

    private void BroadcastHeatChanged()
    {
        float nextThreshold = _currentStarLevel < maxStarLevel
            ? starThresholds[_currentStarLevel]
            : starThresholds[maxStarLevel - 1];

        OnHeatChanged?.Invoke(_currentHeat, nextThreshold);
    }

    /// <summary>
    /// Returns the heat threshold required to reach a given star level.
    /// </summary>
    public float GetThresholdForStar(int starLevel)
    {
        if (starLevel <= 0 || starLevel > maxStarLevel)
            return 0f;

        return starThresholds[starLevel - 1];
    }
}