using TMPro;
using UnityEngine;

/// <summary>
/// Fills the pause menu's Controls list. The care and interact keys come from InputPrompts,
/// the same place the on-screen prompts read from, so when rebinding arrives (MVP) this list
/// updates itself. Put this on the Controls text object inside the pause panel.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]   // the concrete UI text type (TMP_Text itself is abstract)
public class ControlsList : MonoBehaviour
{
    // OnEnable runs every time the pause panel opens, so the list is always current
    private void OnEnable()
    {
        string K(PromptAction action) => InputPrompts.Get(action);   // a tiny local helper to keep the lines short

        GetComponent<TMP_Text>().text =
            Line("Move", "WASD") +
            Line("Sprint / Jump", "Shift / Space") +
            Line("Use, talk, buy", K(PromptAction.Click)) +
            Line("Feed a cat", K(PromptAction.Feed)) +
            Line("Clean a cat", K(PromptAction.Clean)) +
            Line("Pet a cat", K(PromptAction.Pet)) +
            Line("Rare cat options", K(PromptAction.CatOptions)) +
            Line("Pause / leave PC", "Esc");
    }

    // Action on the left, key lined up at 60% (the same <pos> trick as the day receipt)
    private static string Line(string action, string key)
    {
        return $"{action}<pos=66%><b>{key}</b>\n";
    }
}