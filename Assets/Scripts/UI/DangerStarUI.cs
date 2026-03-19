using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DangerStarUI : MonoBehaviour
{
    [Header("Star Icons")]
    [Tooltip("Assign star Image components in order (star 1 to star 5). Use a filled/unfilled sprite swap.")]
    [SerializeField] private List<Image> starImages = new List<Image>();

    [Header("Sprites")]
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;

    [Header("Animation")]
    [SerializeField] private bool pulseOnLevelUp = true;
    [SerializeField] private float pulseScale = 1.3f;
    [SerializeField] private float pulseDuration = 0.2f;

    [Header("Visibility")]
    [SerializeField] private GameObject starContainer;
    [SerializeField] private bool hideWhenZeroStars = true;
    [SerializeField] private float hideDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool logUIChanges = false;

    private int _lastStarLevel;
    private float _hideTimer;
    private bool _isHiding;
    private bool _isSubscribed;

    // Pulse animation tracking
    private Dictionary<int, float> _pulseTimers = new Dictionary<int, float>();

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (DangerStarManager.Instance != null)
        {
            DangerStarManager.Instance.OnStarLevelChanged -= HandleStarLevelChanged;
            _isSubscribed = false;
        }
    }

    private void Start()
    {
        TrySubscribe();

        // Initial state: hide container if zero stars
        if (hideWhenZeroStars && starContainer != null)
            SetContainerVisible(false);
    }

    private void Update()
    {
        // Retry subscription if DangerStarManager wasn't ready yet
        if (!_isSubscribed)
            TrySubscribe();

        UpdatePulseAnimations();
        UpdateHideTimer();
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (DangerStarManager.Instance == null)
            return;

        // Unsubscribe first to prevent double subscription
        DangerStarManager.Instance.OnStarLevelChanged -= HandleStarLevelChanged;
        DangerStarManager.Instance.OnStarLevelChanged += HandleStarLevelChanged;
        _isSubscribed = true;

        // Sync with current state
        UpdateStarVisuals(DangerStarManager.Instance.CurrentStarLevel);

        if (logUIChanges)
            Debug.Log($"[DangerStarUI] Subscribed to DangerStarManager. Current stars={DangerStarManager.Instance.CurrentStarLevel}", this);
    }

    private void HandleStarLevelChanged(int newLevel, int maxLevel)
    {
        if (logUIChanges)
            Debug.Log($"[DangerStarUI] Star level changed: {_lastStarLevel} -> {newLevel}", this);

        // Trigger pulse on newly filled stars
        if (pulseOnLevelUp && newLevel > _lastStarLevel)
        {
            for (int i = _lastStarLevel; i < newLevel && i < starImages.Count; i++)
            {
                _pulseTimers[i] = pulseDuration;
            }
        }

        UpdateStarVisuals(newLevel);

        // Show container when stars > 0
        if (newLevel > 0)
        {
            _isHiding = false;
            _hideTimer = 0f;
            SetContainerVisible(true);
        }

        _lastStarLevel = newLevel;
    }

    private void UpdateStarVisuals(int starLevel)
    {
        if (logUIChanges)
            Debug.Log($"[DangerStarUI] Updating visuals: starLevel={starLevel} | imageCount={starImages.Count}", this);

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null)
            {
                if (logUIChanges)
                    Debug.LogWarning($"[DangerStarUI] starImages[{i}] is NULL!", this);
                continue;
            }

            bool isFilled = i < starLevel;

            // Swap sprite if both are assigned
            if (starFilledSprite != null && starEmptySprite != null)
            {
                starImages[i].sprite = isFilled ? starFilledSprite : starEmptySprite;
                starImages[i].color = isFilled ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                starImages[i].gameObject.SetActive(true);
            }
            else
            {
                // No empty sprite — simply show/hide each star
                starImages[i].gameObject.SetActive(isFilled);

                if (isFilled && starFilledSprite != null)
                    starImages[i].sprite = starFilledSprite;

                starImages[i].color = Color.white;
            }
        }

        // Handle container visibility for zero stars
        if (hideWhenZeroStars && starLevel <= 0)
        {
            if (!_isHiding)
            {
                _isHiding = true;
                _hideTimer = hideDelay;
            }
        }
    }

    private void UpdatePulseAnimations()
    {
        if (_pulseTimers.Count == 0)
            return;

        List<int> finished = new List<int>();

        foreach (var kvp in _pulseTimers)
        {
            int index = kvp.Key;
            float remaining = kvp.Value - Time.deltaTime;

            if (index >= 0 && index < starImages.Count && starImages[index] != null)
            {
                float t = remaining / pulseDuration;
                float scale = Mathf.Lerp(1f, pulseScale, t);
                starImages[index].transform.localScale = Vector3.one * scale;
            }

            if (remaining <= 0f)
            {
                finished.Add(index);
                if (index >= 0 && index < starImages.Count && starImages[index] != null)
                    starImages[index].transform.localScale = Vector3.one;
            }
            else
            {
                _pulseTimers[index] = remaining;
            }
        }

        for (int i = 0; i < finished.Count; i++)
            _pulseTimers.Remove(finished[i]);
    }

    private void UpdateHideTimer()
    {
        if (!hideWhenZeroStars || !_isHiding)
            return;

        _hideTimer -= Time.deltaTime;

        if (_hideTimer <= 0f)
        {
            _isHiding = false;
            SetContainerVisible(false);
        }
    }

    private void SetContainerVisible(bool visible)
    {
        if (starContainer != null)
        {
            starContainer.SetActive(visible);

            if (logUIChanges)
                Debug.Log($"[DangerStarUI] Container visible={visible}", this);
        }
    }
}