using UnityEngine;
using TMPro;

/// <summary>
/// Displays NPC passenger status and drop-off information on the HUD.
/// Listens to DropManager events to update UI automatically.
/// </summary>
[DisallowMultipleComponent]
public class DropZoneUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DropManager dropManager;
    [SerializeField] private TukTukSeat tukTukSeat;

    [Header("UI Elements")]
    [Tooltip("Shows how many NPCs are currently seated (e.g. 'Passengers: 2/3')")]
    [SerializeField] private TextMeshProUGUI seatedCountText;

    [Tooltip("Shows the assigned drop zone name (e.g. 'Drop: Market Square')")]
    [SerializeField] private TextMeshProUGUI dropZoneNameText;

    [Tooltip("Shows the total NPCs dropped across all deliveries (e.g. 'Total Dropped: 12')")]
    [SerializeField] private TextMeshProUGUI totalDroppedText;

    [Tooltip("Shows a notification when NPCs are dropped. Place this OUTSIDE Drop Panel so it stays visible.")]
    [SerializeField] private TextMeshProUGUI dropNotificationText;

    [Header("UI Panels")]
    [Tooltip("Parent panel that shows when passengers are seated.")]
    [SerializeField] private GameObject passengerPanel;

    [Tooltip("Parent panel that shows the drop zone destination.")]
    [SerializeField] private GameObject dropZonePanel;

    [Header("Notification Settings")]
    [SerializeField] private float notificationDuration = 3f;

    private int _totalDroppedCount;
    private float _notificationTimer;

    private void OnEnable()
    {
        if (dropManager != null)
        {
            dropManager.OnDropZoneAssigned += HandleDropZoneAssigned;
            dropManager.OnPassengersDropped += HandlePassengersDropped;
            dropManager.OnDropZoneCleared += HandleDropZoneCleared;
        }
    }

    private void OnDisable()
    {
        if (dropManager != null)
        {
            dropManager.OnDropZoneAssigned -= HandleDropZoneAssigned;
            dropManager.OnPassengersDropped -= HandlePassengersDropped;
            dropManager.OnDropZoneCleared -= HandleDropZoneCleared;
        }
    }

    private void Start()
    {
        _totalDroppedCount = 0;

        UpdateTotalDroppedText();
        HideDropZoneInfo();
        HideNotification();

        if (passengerPanel != null)
            passengerPanel.SetActive(false);
    }

    private void Update()
    {
        UpdateSeatedCount();

        // Handle notification auto-hide
        if (_notificationTimer > 0f)
        {
            _notificationTimer -= Time.deltaTime;
            if (_notificationTimer <= 0f)
                HideNotification();
        }
    }

    // ── Event Handlers ──

    private void HandleDropZoneAssigned(DropZone dropZone)
    {
        if (dropZonePanel != null)
            dropZonePanel.SetActive(true);

        if (dropZoneNameText != null)
            dropZoneNameText.text = $"Drop: {dropZone.ZoneName}";
    }

    private void HandlePassengersDropped(DropZone dropZone, int count)
    {
        _totalDroppedCount += count;
        UpdateTotalDroppedText();

        ShowNotification($"Dropped {count} passenger{(count > 1 ? "s" : "")} at {dropZone.ZoneName}!");
    }

    private void HandleDropZoneCleared()
    {
        HideDropZoneInfo();
    }

    // ── UI Updates ──

    private void UpdateSeatedCount()
    {
        if (tukTukSeat == null)
            return;

        int seated = 0;
        int totalSeats = tukTukSeat.NPCBackSeatCount;

        for (int i = 0; i < tukTukSeat.NPCBackSeats.Count; i++)
        {
            if (tukTukSeat.NPCBackSeats[i].IsOccupied)
                seated++;
        }

        bool hasPassengers = seated > 0;

        if (passengerPanel != null)
            passengerPanel.SetActive(hasPassengers);

        if (seatedCountText != null)
            seatedCountText.text = $"Passengers: {seated}/{totalSeats}";
    }

    private void UpdateTotalDroppedText()
    {
        if (totalDroppedText != null)
            totalDroppedText.text = $"Total Dropped: {_totalDroppedCount}";
    }

    private void ShowNotification(string message)
    {
        if (dropNotificationText == null)
        {
            Debug.LogWarning("[DropZoneUI] dropNotificationText is not assigned in the Inspector!", this);
            return;
        }

        dropNotificationText.text = message;
        dropNotificationText.gameObject.SetActive(true);
        _notificationTimer = notificationDuration;
    }

    private void HideNotification()
    {
        if (dropNotificationText != null)
            dropNotificationText.gameObject.SetActive(false);
    }

    private void HideDropZoneInfo()
    {
        if (dropZonePanel != null)
            dropZonePanel.SetActive(false);

        if (dropZoneNameText != null)
            dropZoneNameText.text = string.Empty;
    }

    /// <summary>
    /// Returns the total number of NPCs dropped across all deliveries.
    /// </summary>
    public int TotalDroppedCount => _totalDroppedCount;

    /// <summary>
    /// Resets the total dropped counter (e.g. on new game).
    /// </summary>
    public void ResetTotalDropped()
    {
        _totalDroppedCount = 0;
        UpdateTotalDroppedText();
    }
}