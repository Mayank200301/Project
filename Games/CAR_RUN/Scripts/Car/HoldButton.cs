using UnityEngine;
using UnityEngine.EventSystems;

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum ButtonType
    {
        Left,
        Right,
        Accelerate,
        Brake
    }

    public ButtonType buttonType;
    public ButtonVisual buttonVisual;
    

    public void OnPointerDown(PointerEventData eventData)
    {
        if (buttonVisual != null)
            buttonVisual.Press();

        switch (buttonType)
        {
            case ButtonType.Left:
                MobileInput.Instance.LeftDown();
                break;

            case ButtonType.Right:
                MobileInput.Instance.RightDown();
                break;

            case ButtonType.Accelerate:
                MobileInput.Instance.AcceleratePress();
                break;

            case ButtonType.Brake:
                MobileInput.Instance.BrakePress();
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (buttonVisual != null)
            buttonVisual.Release();

        switch (buttonType)
        {
            case ButtonType.Left:
            case ButtonType.Right:
                MobileInput.Instance.SteerUp();
                break;

            case ButtonType.Accelerate:
                MobileInput.Instance.AccelerateRelease();
                break;

            case ButtonType.Brake:
                MobileInput.Instance.BrakeRelease();
                break;
        }
    }
}