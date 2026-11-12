using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// A pause-menu slider that changes how fast the camera turns.
/// It multiplies the camera's starting look speeds, so the up/down direction never flips.
/// Put this on the same always-active object as PauseMenu.
/// </summary>
public class MouseSensitivity : MonoBehaviour
{
    [SerializeField] private Slider slider;                              // the slider in the pause panel
    [SerializeField] private CinemachineInputAxisController lookInput;   // on the FP CinemachineCam
    [SerializeField] private float defaultSensitivity = 0.7f;            // first-launch value (testers found 1.0 too fast)

    private const string SaveKey = "MouseSensitivity";                   // the name it is saved under

    // The starting gain of each look direction (e.g. 100 and -100), remembered once
    private readonly List<float> baseGains = new List<float>();

    private void Start()
    {
        if (lookInput == null)
            lookInput = FindFirstObjectByType<CinemachineInputAxisController>();

        // Remember the gains the camera was built with
        foreach (var controller in lookInput.Controllers)
            baseGains.Add(controller.Input.Gain);

        // Load the saved value, or use the default on first launch
        float saved = PlayerPrefs.GetFloat(SaveKey, defaultSensitivity);

        // Show it on the slider without triggering the event, then apply it once
        slider.SetValueWithoutNotify(saved);
        Apply(saved);

        // From now on, react whenever the player drags the slider
        slider.onValueChanged.AddListener(Apply);
    }

    private void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Apply);
    }

    // Sets every look direction to: starting gain × slider value, and saves the value
    private void Apply(float value)
    {
        for (int i = 0; i < lookInput.Controllers.Count && i < baseGains.Count; i++)
            lookInput.Controllers[i].Input.Gain = baseGains[i] * value;

        PlayerPrefs.SetFloat(SaveKey, value);
    }
}