using System;
using UnityEngine;

/// <summary>
/// Drives NPC Animator parameters based on NPCMovement velocity.
/// Lives on the NPC parent alongside NPCMovement.
/// The Animator component itself sits on the Visual Root child.
///
/// Animator Controller setup:
///   - Float   "Speed"       : drives Idle ↔ Locomotion blend.
///   - Trigger "StandToSit"  : Any State / Locomotion / Idle → SitDown
///   - Bool    "IsSitting"   : SitDown → SitIdle (or auto-transition)
///   - Trigger "SitToStand"  : SitIdle → StandUp → back to Idle / Locomotion
/// </summary>
[DisallowMultipleComponent]
public class NPCAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator on the Visual Root child. Auto-found in children if not assigned.")]
    [SerializeField] private Animator animator;

    [Tooltip("NPCMovement on this GameObject. Auto-found if not assigned.")]
    [SerializeField] private NPCMovement movement;

    [Header("Smoothing")]
    [Tooltip("How quickly the Speed parameter blends to the target value.")]
    [SerializeField] private float speedDampTime = 0.15f;

    // ── Hashed Parameter IDs (cached once, used every frame) ──
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashStandToSit = Animator.StringToHash("StandToSit");
    private static readonly int HashIsSitting = Animator.StringToHash("IsSitting");
    private static readonly int HashSitToStand = Animator.StringToHash("SitToStand");

    private bool _isSitting;

    /// <summary>Fires when the sit-down animation finishes (use an Animation Event or timed callback).</summary>
    public event Action OnSitComplete;

    /// <summary>Fires when the stand-up animation finishes.</summary>
    public event Action OnStandComplete;

    public bool IsSitting => _isSitting;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<NPCMovement>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError($"[NPCAnimator] {name} | No Animator found on children (Visual Root). " +
                           $"Assign it manually or ensure the Visual Root has an Animator component.", this);
            enabled = false;
            return;
        }

        // Enforce no root motion — NavMeshAgent owns the position.
        animator.applyRootMotion = false;
    }

    private void Update()
    {
        if (animator == null || movement == null)
            return;

        // Don't update locomotion speed while sitting — keep Speed at 0
        if (!_isSitting)
            UpdateLocomotion();
    }

    /// <summary>
    /// Feeds the current NavMeshAgent speed into the Animator's Speed float
    /// using damping so transitions feel smooth.
    /// </summary>
    private void UpdateLocomotion()
    {
        float currentSpeed = movement.CurrentSpeed;

        animator.SetFloat(HashSpeed, currentSpeed, speedDampTime, Time.deltaTime);
    }

    // ──────────────────────────────────────────────
    //  Public API — Sitting Integration
    // ──────────────────────────────────────────────

    /// <summary>
    /// Triggers the stand-to-sit transition. Call this when the NPC reaches the seat.
    /// </summary>
    public void PlayStandToSit()
    {
        if (animator == null)
            return;

        _isSitting = true;
        animator.SetFloat(HashSpeed, 0f);
        animator.SetBool(HashIsSitting, true);
        animator.SetTrigger(HashStandToSit);

        Debug.Log($"[NPCAnimator] {name} | PlayStandToSit triggered.", this);
    }

    /// <summary>
    /// Triggers the sit-to-stand transition. Call this when the NPC is leaving the seat.
    /// </summary>
    public void PlaySitToStand()
    {
        if (animator == null)
            return;

        animator.SetBool(HashIsSitting, false);
        animator.SetTrigger(HashSitToStand);

        Debug.Log($"[NPCAnimator] {name} | PlaySitToStand triggered.", this);
    }

    // ──────────────────────────────────────────────
    //  Animation Event Callbacks
    //  Add these as Animation Events on the last
    //  frame of SitDown and StandUp clips.
    // ──────────────────────────────────────────────

    /// <summary>
    /// Called by an Animation Event on the last frame of the SitDown clip.
    /// </summary>
    public void OnSitDownComplete()
    {
        Debug.Log($"[NPCAnimator] {name} | SitDown animation complete.", this);
        OnSitComplete?.Invoke();
    }

    /// <summary>
    /// Called by an Animation Event on the last frame of the StandUp clip.
    /// </summary>
    public void OnStandUpComplete()
    {
        _isSitting = false;
        Debug.Log($"[NPCAnimator] {name} | StandUp animation complete.", this);
        OnStandComplete?.Invoke();
    }
}