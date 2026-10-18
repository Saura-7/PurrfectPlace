using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MessageUI : MonoBehaviour
{
    public static MessageUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float showTime = 2.5f;
    [SerializeField] private int maxLines = 3;

    private readonly List<string> lines = new List<string>();
    private Coroutine hideRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (messageText != null) messageText.text = "";
    }

    // Call this from any script: MessageUI.Notify("Hello");
    public static void Notify(string message)
    {
        if (Instance != null) Instance.Show(message);
    }

    private void Show(string message)
    {
        if (messageText == null) return;

        lines.Add(message);
        if (lines.Count > maxLines) lines.RemoveAt(0);
        messageText.text = string.Join("\n", lines);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(showTime);
        lines.Clear();
        messageText.text = "";
    }
}