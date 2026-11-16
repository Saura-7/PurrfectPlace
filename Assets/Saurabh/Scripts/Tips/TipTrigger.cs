using UnityEngine;

/// <summary>
/// Shows a tip with no code at all. Drop it on any object and pick when:
///   When Enabled       : the moment this object switches on (a new station, a new room)
///   Player Enters Zone : the player walks into this object's trigger collider (tick Is Trigger)
/// </summary>
public class TipTrigger : MonoBehaviour
{
    private enum When { WhenEnabled, PlayerEntersZone }

    [SerializeField] private TipData tip;
    [SerializeField] private When when = When.WhenEnabled;

    private void OnEnable()
    {
        if (when == When.WhenEnabled) TipCard.Show(tip);
    }

    private void OnTriggerEnter(Collider other)
    {
        // The player is whatever has PlayerMovement on it (or on a parent)
        if (when == When.PlayerEntersZone && other.GetComponentInParent<PlayerMovement>() != null)
            TipCard.Show(tip);
    }
}