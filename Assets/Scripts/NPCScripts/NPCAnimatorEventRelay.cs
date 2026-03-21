using UnityEngine;

/// <summary>
/// Place this on the Visual Root child (same GameObject as the Animator).
/// Forwards Animation Event callbacks to the NPCAnimator on the parent.
///
/// Setup:
///   1. Add this script to the NPC mesh/Visual Root child.
///   2. On your SitDown animation clip, add an Animation Event on the last frame
///      calling "OnSitDownComplete".
///   3. On your StandUp animation clip, add an Animation Event on the last frame
///      calling "OnStandUpComplete".
///   4. These events hit this relay, which forwards them to NPCAnimator on the parent.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class NPCAnimatorEventRelay : MonoBehaviour
{
    private NPCAnimator _npcAnimator;

    private void Awake()
    {
        _npcAnimator = GetComponentInParent<NPCAnimator>();

        if (_npcAnimator == null)
        {
            Debug.LogError($"[NPCAnimatorEventRelay] {name} | No NPCAnimator found on parent! " +
                           $"Ensure the NPC parent has an NPCAnimator component.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Called by Animation Event on the last frame of the SitDown clip.
    /// </summary>
    public void OnSitDownComplete()
    {
        if (_npcAnimator != null)
            _npcAnimator.OnSitDownComplete();
    }

    /// <summary>
    /// Called by Animation Event on the last frame of the StandUp clip.
    /// </summary>
    public void OnStandUpComplete()
    {
        if (_npcAnimator != null)
            _npcAnimator.OnStandUpComplete();
    }
}