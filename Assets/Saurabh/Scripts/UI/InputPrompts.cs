using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ONE place that knows what to show for each action.
/// UI scripts never type key names. They call InputPrompts.Get(PromptAction.Feed).
/// BETA: the text is typed in the Inspector.
/// MVP: only the inside of Lookup() changes (read bindings, detect the device, support rebinding).
/// </summary>
public class InputPrompts : MonoBehaviour
{
    public static InputPrompts Instance { get; private set; }

    public enum Device { Keyboard, Gamepad }

    [Serializable]
    public class PromptEntry
    {
        public PromptAction action;
        public string keyboardText;
        public string gamepadText;
    }

    [Header("Which device the prompts show")]
    [SerializeField] private Device currentDevice = Device.Keyboard;

    [Header("Prompt text per action")]
    [SerializeField] private List<PromptEntry> entries = new List<PromptEntry>();

    // Fires when the prompts change (device switch now, rebinding in the MVP).
    // Static texts, such as a tutorial, can listen to this and refresh themselves.
    public static event Action OnPromptsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Fills in the default entries when the component is first added in the editor
    private void Reset()
    {
        entries = new List<PromptEntry>
        {
            new PromptEntry { action = PromptAction.Feed,         keyboardText = "C", gamepadText = "LB" },
            new PromptEntry { action = PromptAction.Clean,        keyboardText = "X", gamepadText = "RB" },
            new PromptEntry { action = PromptAction.CompleteDeal, keyboardText = "Z", gamepadText = "RT" },
            new PromptEntry { action = PromptAction.Click,        keyboardText = "LMB", gamepadText = "A" },
            // new PromptEntry { action = PromptAction.OpenSign,     keyboardText = "Z", gamepadText = "RT" },
        };
    }

    // The only method other scripts need
    public static string Get(PromptAction action)
    {
        if (Instance == null) return "?";
        return Instance.Lookup(action);
    }

    public void SetDevice(Device device)
    {
        if (device == currentDevice) return;
        currentDevice = device;
        OnPromptsChanged?.Invoke();
    }

    // MVP UPGRADE POINT: replace the body of this method with a lookup of the real binding.
    private string Lookup(PromptAction action)
    {
        foreach (PromptEntry entry in entries)
        {
            if (entry.action != action) continue;

            string text = currentDevice == Device.Gamepad ? entry.gamepadText : entry.keyboardText;
            return string.IsNullOrEmpty(text) ? "?" : text;
        }
        return "?"; // no entry for this action: fix it in the Inspector
    }
}