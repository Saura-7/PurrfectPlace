// Every action the game can show a prompt for.
// Add a new line here when a new prompt is needed (for example Pause).
public enum PromptAction
{
    Feed,
    Clean,
    CompleteDeal,
    Click,          // left mouse button, for world-space buttons (deal panel, catalogue, sign)
    Pet,            // pet the cat you're looking at (free, just for love)
    CatOptions      // open the options for a rare cat (keep as resident / put up for adoption)
}