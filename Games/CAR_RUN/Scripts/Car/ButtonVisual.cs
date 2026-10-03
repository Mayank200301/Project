using UnityEngine;
using UnityEngine.UI;

public class ButtonVisual : MonoBehaviour
{
    public Image buttonImage;
    
    public Sprite normalSprite;
    public Sprite pressedSprite;   // Used as the Reverse ON sprite
    
    public void Press()
    {
        buttonImage.sprite = pressedSprite;
    }
    
    public void Release()
    {
        buttonImage.sprite = normalSprite;
    }
    
    // Reverse Button
    public void Toggle()
    {
        MobileInput.Instance.ToggleReverse();
    
        if (MobileInput.Instance.reverseMode)
            buttonImage.sprite = pressedSprite;   // Reverse ON
        else
            buttonImage.sprite = normalSprite;    // Reverse OFF
    }
}