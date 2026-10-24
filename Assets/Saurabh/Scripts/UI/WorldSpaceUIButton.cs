using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class WorldSpaceUIButton : MonoBehaviour
{
    [Header("Button Event")]
    public UnityEvent onClick;

    public void PressButton()
    {
        AudioManager.Play(Sfx.ButtonClick);
        onClick?.Invoke();
    }
}