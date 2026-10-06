using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class WorldSpaceUIButton : MonoBehaviour
{
    [Header("Button Event")]
    public UnityEvent onClick;

    public void PressButton()
    {
        onClick?.Invoke();
    }
}