using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Developer console, like Minecraft's or Schedule 1's. Press / to open it,
/// type a command such as "money 500" and press Enter. Esc closes it.
///
/// Any script can add its own command with one line:
///     DevConsole.Add("money", "money <amount> · add GP", args => { ...; return "result text"; });
///
/// Only works in the Editor and in Development builds, so outside testers never see it.
/// Put this on one always-active object in the scene (for example Managers).
/// </summary>
public class DevConsole : MonoBehaviour
{
    // ==========================================
    // THE SHARED COMMAND LIST
    // ==========================================

    // One command: how to use it, and the method that runs it
    private class Command
    {
        public string usage;                  // shown by "help"
        public Func<string[], string> run;    // takes the words after the name, returns text to print
    }

    // name -> command, like a phone book. "static" = one list shared by the whole game.
    private static readonly Dictionary<string, Command> commands = new Dictionary<string, Command>();

    // Other scripts can check this to ignore keys while the player is typing
    public static bool IsOpen { get; private set; }

    // The frame the console last closed on. See BlocksPause.
    private static int closedOnFrame = -10;

    // True while open AND for one frame after closing.
    // Why: Unity's text UI (OnGUI) sees a key press at the END of a frame, but the Input System
    // (which the pause menu uses) only sees the same press at the START of the next frame.
    // Without this, Esc would close the console and then open the pause menu one frame later.
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedOnFrame + 1;

    /// <summary>Adds (or replaces) a command. Safe to call again after a scene reload.</summary>
    public static void Add(string name, string usage, Func<string[], string> run)
    {
        commands[name.ToLowerInvariant()] = new Command { usage = usage, run = run };
    }

    // ---------- helpers for commands ----------

    // Reads args[index] as a whole number. False if it's missing or not a number.
    public static bool TryGetInt(string[] args, int index, out int value)
    {
        value = 0;
        return index < args.Length && int.TryParse(args[index], out value);
    }

    // Reads args[index] as a decimal number. InvariantCulture means "1.5" works on every PC,
    // even ones set to a language that writes decimals as "1,5".
    public static bool TryGetFloat(string[] args, int index, out float value)
    {
        value = 0f;
        return index < args.Length &&
               float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    // Wraps a message in red so it reads as an error
    public static string Error(string message) => $"<color=#F08070>{message}</color>";

    // ==========================================
    // SETTINGS (Inspector)
    // ==========================================

    [SerializeField] private KeyCode toggleKey = KeyCode.Slash;   // / like Minecraft

    [Header("Look (Minecraft-style chat in the bottom-left)")]
    [SerializeField, Range(0.2f, 0.8f)] private float widthPercent = 0.4f;   // part of the screen width
    [SerializeField] private int fontSize = 15;
    [SerializeField] private int margin = 12;                 // gap from the screen edges (pixels)
    [SerializeField] private int visibleLines = 10;           // lines shown above the input while open
    [SerializeField] private float closedShowSeconds = 6f;    // new lines stay visible this long after closing
    [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.55f;
    [SerializeField] private int maxLogLines = 200;

    // ==========================================
    // WHAT THE CONSOLE REMEMBERS
    // ==========================================

    // One printed line, plus when it was printed (so it can fade out when the console is closed)
    private struct LogLine
    {
        public string text;
        public float time;
    }

    private string input = "";                          // what's typed right now
    private readonly List<LogLine> log = new List<LogLine>();    // lines shown above the input
    private readonly List<string> history = new List<string>();  // past commands, for the arrow keys
    private int historyIndex;
    private int scrollOffset;         // mouse wheel: how many lines we've scrolled up
    private bool focusInput;          // put the cursor in the text box next frame
    private bool moveCaretToEnd;      // after Tab or arrow keys, jump the caret to the end
    private int swallowTypingFrame = -10;   // the frame the console opened on: ignore typed letters from that key

    // Gameplay scripts we switch off while typing (the same ones the pause menu freezes)
    private readonly List<Behaviour> blocked = new List<Behaviour>();

    private GUIStyle logStyle, inputStyle, hintStyle;
    private Texture2D backgroundTexture;   // one dark pixel, stretched behind text

    private const string InputName = "DevConsoleInput";

    // ==========================================
    // UNITY EVENTS
    // ==========================================

    private void Awake()
    {
        IsOpen = false;
        closedOnFrame = -10;

        // Debug.isDebugBuild is true in the Editor and in builds with "Development Build" ticked
        if (!Debug.isDebugBuild)
        {
            enabled = false;
            return;
        }

        AddBuiltInCommands();
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null) Destroy(backgroundTexture);
        if (IsOpen) SetOpen(false);
        IsOpen = false;
    }

    // OnGUI is Unity's code-only UI. It runs several times per frame, once per "event"
    // (a key press, a mouse click, drawing the screen...). Event.current tells us which one.
    private void OnGUI()
    {
        Event e = Event.current;

        // 1. The toggle key opens the console
        if (e.type == EventType.KeyDown && e.keyCode == toggleKey && !IsOpen)
        {
            SetOpen(true);
            swallowTypingFrame = Time.frameCount;
            e.Use();      // "eat" the key so nothing else reacts to it
            return;
        }

        // A key press arrives twice: once as "a key went down" and once as "this letter was typed".
        // The typed "/" from the key that opened the console must not land in the text box.
        if (IsOpen && e.type == EventType.KeyDown && e.character != '\0' && Time.frameCount == swallowTypingFrame)
        {
            e.Use();
            return;
        }

        if (!IsOpen)
        {
            if (e.type == EventType.Repaint) DrawRecentLines();   // recent results fade out, like Minecraft chat
            return;
        }

        // Mouse wheel scrolls through older lines
        if (e.type == EventType.ScrollWheel)
        {
            scrollOffset = Mathf.Clamp(scrollOffset + (e.delta.y < 0 ? 1 : -1), 0, Mathf.Max(0, log.Count - visibleLines));
            e.Use();
        }

        // 2. Special keys, handled BEFORE the text box gets them
        if (e.type == EventType.KeyDown)
        {
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    RecallHistory(-1);
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    RecallHistory(+1);
                    e.Use();
                    break;
                case KeyCode.Tab:
                    AutoComplete();
                    e.Use();
                    break;
                case KeyCode.Escape:
                    SetOpen(false);
                    e.Use();
                    return;
            }
        }

        // 3. Draw it
        DrawConsole();
    }

    // ==========================================
    // DRAWING
    // ==========================================

    // Makes the styles and the dark background. Styles can only be made inside OnGUI.
    private void MakeStyles()
    {
        if (logStyle != null) return;

        backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, backgroundAlpha));
        backgroundTexture.Apply();

        logStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true,
            wordWrap = true,
            padding = new RectOffset(6, 6, 2, 2)
        };
        logStyle.normal.textColor = Color.white;

        // A text box with no border: just our dark strip
        inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = fontSize,
            padding = new RectOffset(6, 6, 4, 4),
            border = new RectOffset(0, 0, 0, 0)
        };
        inputStyle.normal.background = backgroundTexture;
        inputStyle.focused.background = backgroundTexture;
        inputStyle.hover.background = backgroundTexture;
        inputStyle.normal.textColor = Color.white;
        inputStyle.focused.textColor = Color.white;

        hintStyle = new GUIStyle(inputStyle);
        hintStyle.normal.background = null;
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.35f);
    }

    // The input strip at the very bottom-left
    private Rect InputRect()
    {
        float width = Screen.width * widthPercent;
        float height = fontSize + 12;
        return new Rect(margin, Screen.height - margin - height, width, height);
    }

    private void DrawConsole()
    {
        MakeStyles();
        Rect inputRect = InputRect();

        // 1. The log, stacked upwards from just above the input
        DrawLines(inputRect.y - 4, visibleLines, scrollOffset, fade: false);

        // 2. The input strip
        GUI.SetNextControlName(InputName);          // give the text box a name so we can focus it
        input = GUI.TextField(inputRect, input, inputStyle);

        // Tab also arrives as a typed character; remove it
        input = input.Replace("\t", "");

        // 3. A faint hint while the box is empty
        if (input.Length == 0) GUI.Label(inputRect, "Type a command. help lists them all.", hintStyle);

        if (focusInput)
        {
            GUI.FocusControl(InputName);
            focusInput = false;
        }

        if (moveCaretToEnd && Event.current.type == EventType.Repaint)
        {
            // The text box's caret lives in a TextEditor object; move it after the last letter
            TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            editor.MoveTextEnd();
            moveCaretToEnd = false;
        }
    }

    // While closed: only lines printed in the last few seconds, fading out
    private void DrawRecentLines()
    {
        if (log.Count == 0) return;
        MakeStyles();
        DrawLines(InputRect().yMax, visibleLines, 0, fade: true);
    }

    // Draws up to "count" lines, newest at the bottom, starting at "bottom" and going up
    private void DrawLines(float bottom, int count, int skipNewest, bool fade)
    {
        float width = Screen.width * widthPercent;
        float y = bottom;
        int newest = log.Count - 1 - skipNewest;

        for (int i = newest; i >= 0 && i > newest - count; i--)
        {
            float alpha = 1f;
            if (fade)
            {
                float age = Time.unscaledTime - log[i].time;      // unscaled: still fades while paused
                if (age > closedShowSeconds) break;               // older lines are hidden
                alpha = Mathf.Clamp01((closedShowSeconds - age) / 1f);   // fade out in the last second
            }

            GUIContent content = new GUIContent(log[i].text);
            float height = logStyle.CalcHeight(content, width);  // wrapped lines are taller
            y -= height;

            Rect rect = new Rect(margin, y, width, height);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, backgroundTexture);   // dark strip behind this line only
            GUI.Label(rect, content, logStyle);
        }
        GUI.color = Color.white;   // always reset, or everything drawn after is faded too
    }

    // Adds text to the log. Text with several lines (like help) becomes several entries.
    private void Print(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        foreach (string line in text.Split('\n'))
        {
            log.Add(new LogLine { text = line, time = Time.unscaledTime });
        }
        while (log.Count > maxLogLines) log.RemoveAt(0);   // keep memory small
        scrollOffset = 0;                                   // jump back to the newest line
    }

    // ==========================================
    // OPENING AND CLOSING
    // ==========================================

    private void SetOpen(bool open)
    {
        if (open == IsOpen) return;
        IsOpen = open;

        if (open) focusInput = true;
        else closedOnFrame = Time.frameCount;   // see BlocksPause
        BlockGameInput(open);
    }

    // While typing, W/A/S/D must not walk and Q/E must not feed cats
    private void BlockGameInput(bool block)
    {
        // The pause menu or day summary has already frozen everything: leave it alone
        if (PauseMenu.IsPaused) return;

        if (block)
        {
            blocked.Clear();
            TryBlock(FindFirstObjectByType<PlayerMovement>());
            TryBlock(FindFirstObjectByType<CareSubsystem>());
            TryBlock(FindFirstObjectByType<ReceptionDesk>());
            TryBlock(FindFirstObjectByType<InteractionPromptUI>());
            TryBlock(FindFirstObjectByType<CinemachineInputAxisController>());   // mouse look

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            foreach (Behaviour b in blocked)
            {
                if (b != null) b.enabled = true;
            }
            blocked.Clear();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void TryBlock(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;   // only switch back on what WE switched off
        behaviour.enabled = false;
        blocked.Add(behaviour);
    }

    // ==========================================
    // RUNNING A COMMAND
    // ==========================================

    private void Submit()
    {
        string line = input.Trim();
        input = "";
        if (line.Length == 0) return;

        history.Add(line);
        historyIndex = history.Count;   // one past the newest, i.e. "empty line"

        Print($"<color=#AAAAAA>> {line}</color>");
        Print(Execute(line));
    }

    /// <summary>Runs one line of text as a command and returns what to print.</summary>
    public static string Execute(string line)
    {
        // 1. Split into words: "money  500" -> ["money", "500"] (extra spaces are ignored)
        string[] words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "";

        // 2. The first word is the command name, the rest are its arguments
        string name = words[0].ToLowerInvariant();
        string[] args = new string[words.Length - 1];
        Array.Copy(words, 1, args, 0, args.Length);

        // 3. Look it up
        if (!commands.TryGetValue(name, out Command command))
        {
            string guess = ClosestCommand(name);
            return guess != null
                ? Error($"Unknown command \"{name}\". Did you mean \"{guess}\"?")
                : Error($"Unknown command \"{name}\". Type help to see every command.");
        }

        // 4. Run it. try/catch means a broken command prints an error instead of breaking the game.
        try
        {
            return command.run(args);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return Error($"{name} failed: {ex.Message}");
        }
    }

    // ==========================================
    // QUALITY OF LIFE: history, Tab, "did you mean"
    // ==========================================

    // Up = older command, Down = newer command
    private void RecallHistory(int direction)
    {
        if (history.Count == 0) return;

        historyIndex = Mathf.Clamp(historyIndex + direction, 0, history.Count);
        input = historyIndex < history.Count ? history[historyIndex] : "";
        moveCaretToEnd = true;
    }

    // Completes the command name if only one command starts with what's typed
    private void AutoComplete()
    {
        string typed = input.Trim().ToLowerInvariant();
        if (typed.Length == 0 || typed.Contains(" ")) return;   // only completes the first word

        List<string> matches = new List<string>();
        foreach (string name in commands.Keys)
        {
            if (name.StartsWith(typed)) matches.Add(name);
        }

        if (matches.Count == 1)
        {
            input = matches[0] + " ";
            moveCaretToEnd = true;
        }
        else if (matches.Count > 1)
        {
            matches.Sort();
            Print("<color=#9A9A9A>" + string.Join("   ", matches) + "</color>");
        }
    }

    // Finds the command name with the fewest typos away from what was typed (2 at most)
    private static string ClosestCommand(string typed)
    {
        string best = null;
        int bestDistance = 3;   // anything 3 or more letters off is not a useful guess

        foreach (string name in commands.Keys)
        {
            int distance = EditDistance(typed, name);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = name;
            }
        }
        return best;
    }

    // Levenshtein distance: how many single-letter edits (add, remove, swap) turn a into b.
    // "custmer" -> "customer" is 1 (add an "o").
    private static int EditDistance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1,    // remove a letter
                                              d[i, j - 1] + 1),   // add a letter
                                              d[i - 1, j - 1] + cost);   // swap a letter
            }
        }
        return d[a.Length, b.Length];
    }

    // ==========================================
    // COMMANDS THAT BELONG TO THE CONSOLE ITSELF
    // ==========================================

    private void AddBuiltInCommands()
    {
        Add("help", "help · list every command", args =>
        {
            List<string> names = new List<string>(commands.Keys);
            names.Sort();

            string text = "Commands:";
            foreach (string name in names) text += $"\n  {commands[name].usage}";
            return text;
        });

        Add("clear", "clear · empty the console", args =>
        {
            log.Clear();
            return "";
        });

        Add("time", "time <speed> · game speed (1 = normal, 5 = five times faster)", args =>
        {
            if (!TryGetFloat(args, 0, out float speed) || speed < 0f || speed > 20f)
                return Error("Usage: time <speed>, between 0 and 20");
            if (PauseMenu.IsPaused)
                return Error("The game is paused. Unpause first.");

            Time.timeScale = speed;   // 1 = normal speed. Pausing resets this to 1.
            return $"Game speed set to {speed}x";
        });
    }
}