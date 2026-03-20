using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VehicleNPCCollisionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask npcLayer;
    [SerializeField] private float hitCooldownPerNPC = 2f;
    [SerializeField] private float minImpactVelocity = 3f;

    [Header("NPC Reaction")]
    [SerializeField] private float panicAmountOnHit = 50f;
    [SerializeField] private float angerAmountOnHit = 30f;

    [Header("Hit Sound")]
    [SerializeField] private AudioSource hitAudioSource;
    [Tooltip("Assign one or more hit sound clips. A random one will be played on each hit.")]
    [SerializeField] private AudioClip[] hitSoundClips;
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 1f;
    [Tooltip("If true, pitch will vary slightly per hit for natural feel.")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.9f;
    [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.1f;

    [Header("Debug")]
    [SerializeField] private bool logCollisions = false;

    // Tracks cooldown per NPC instance to prevent spam hits
    private Dictionary<int, float> _hitCooldowns = new Dictionary<int, float>();

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.gameObject == null)
            return;

        // Check if the collided object is on the NPC layer
        if (((1 << collision.gameObject.layer) & npcLayer) == 0)
            return;

        // Get NPCBrain to check if NPC is seated (passengers shouldn't count)
        NPCBrain npcBrain = collision.gameObject.GetComponentInParent<NPCBrain>();

        if (npcBrain == null)
        {
            if (logCollisions)
                Debug.Log($"[VehicleNPCCollisionDetector] Hit object '{collision.gameObject.name}' on NPC layer but no NPCBrain found. Ignoring.", this);
            return;
        }

        // Skip seated NPCs (passengers inside the tuk tuk)
        if (npcBrain.IsSeated)
        {
            if (logCollisions)
                Debug.Log($"[VehicleNPCCollisionDetector] Hit NPC '{npcBrain.name}' but they are SEATED. Ignoring.", this);
            return;
        }

        // Check impact velocity
        float impactVelocity = collision.relativeVelocity.magnitude;
        if (impactVelocity < minImpactVelocity)
        {
            if (logCollisions)
                Debug.Log($"[VehicleNPCCollisionDetector] Hit NPC '{npcBrain.name}' but velocity {impactVelocity:F1} < min {minImpactVelocity:F1}. Ignoring.", this);
            return;
        }

        // Check per-NPC cooldown
        int npcID = npcBrain.GetInstanceID();
        if (_hitCooldowns.TryGetValue(npcID, out float lastHitTime))
        {
            if (Time.time - lastHitTime < hitCooldownPerNPC)
            {
                if (logCollisions)
                    Debug.Log($"[VehicleNPCCollisionDetector] Hit NPC '{npcBrain.name}' but still on cooldown. Ignoring.", this);
                return;
            }
        }

        // Register the hit
        _hitCooldowns[npcID] = Time.time;

        if (logCollisions)
            Debug.Log($"[VehicleNPCCollisionDetector] HIT NPC '{npcBrain.name}' | velocity={impactVelocity:F1}", this);

        // Play hit sound
        PlayHitSound();

        // Report to DangerStarManager
        if (DangerStarManager.Instance != null)
        {
            DangerStarManager.Instance.ReportNPCHit(impactVelocity);
        }
        else
        {
            Debug.LogWarning("[VehicleNPCCollisionDetector] DangerStarManager.Instance is null! Cannot report NPC hit.", this);
        }

        // Trigger NPC mood reactions
        npcBrain.AddPanic(panicAmountOnHit);
        npcBrain.AddAnger(angerAmountOnHit);
    }

    private void PlayHitSound()
    {
        if (hitAudioSource == null || hitSoundClips == null || hitSoundClips.Length == 0)
            return;

        AudioClip clip = hitSoundClips[Random.Range(0, hitSoundClips.Length)];
        if (clip == null)
            return;

        if (randomizePitch)
            hitAudioSource.pitch = Random.Range(minPitch, maxPitch);
        else
            hitAudioSource.pitch = 1f;

        hitAudioSource.PlayOneShot(clip, hitSoundVolume);
    }

    /// <summary>
    /// Cleans up expired cooldown entries. Call periodically if many NPCs exist.
    /// </summary>
    public void CleanupCooldowns()
    {
        List<int> expired = new List<int>();

        foreach (var kvp in _hitCooldowns)
        {
            if (Time.time - kvp.Value > hitCooldownPerNPC * 2f)
                expired.Add(kvp.Key);
        }

        for (int i = 0; i < expired.Count; i++)
            _hitCooldowns.Remove(expired[i]);
    }
}