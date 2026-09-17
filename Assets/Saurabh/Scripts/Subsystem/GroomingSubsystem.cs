using UnityEngine;

public class GroomingSubsystem : MonoBehaviour
{
    private readonly string systemName = "GroomingSubsystem";
    private int groomingCost = -5; // Negative because it costs money

    // public void PerformGrooming(CatBase targetCat)
    // {
    //     // 1. GET: Check if we have enough money. We pass our systemName for the audit.
    //     int currentFunds = CoreEconomySystem.Instance.GetGP(systemName);

    //     if (currentFunds >= Mathf.Abs(groomingCost))
    //     {
    //         // 2. SET: Deduct the money and log it.
    //         CoreEconomySystem.Instance.ModifyGP(groomingCost, systemName);

    //         // 3. Command the cat to play its animation
    //         targetCat.PlayInteractAnimation();
    //     }
    //     else
    //     {
    //         Debug.LogWarning($"{systemName}: Not enough GP to groom the cat!");
    //     }
    // }
}