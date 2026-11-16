using UnityEngine;

/// <summary>
/// One tip card's content, saved as an asset in the Project window.
/// A new mechanic gets a new tip with no new code: right-click in the Project window,
/// Create > Purrfect Place > Tip Card, fill it in, and drag it into the script that
/// should show it (or onto a TipTrigger).
///
/// Key names in curly brackets, like {CatOptions} or {Feed}, are swapped for the player's
/// real key when the card opens, so the text stays right if the controls change.
/// </summary>
[CreateAssetMenu(fileName = "Tip_", menuName = "Purrfect Place/Tip Card")]
public class TipData : ScriptableObject
{
    [SerializeField] private string title = "Something new";
    [SerializeField, TextArea(3, 6)] private string body;   // TextArea: a big multi-line box in the Inspector
    [SerializeField] private Sprite icon;                   // optional: left empty, the card simply has no picture

    public string Title => title;
    public string Body => body;
    public Sprite Icon => icon;
}