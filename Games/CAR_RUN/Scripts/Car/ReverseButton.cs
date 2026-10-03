using UnityEngine;
using UnityEngine.UI;

public class ReverseButton : MonoBehaviour
{
    public Image buttonImage;
    public Sprite normalSprite;
    public Sprite activeSprite;

    public void Toggle()
    {
        MobileInput.Instance.ToggleReverse();

        buttonImage.sprite = MobileInput.Instance.reverseMode
            ? activeSprite
            : normalSprite;
    }
}