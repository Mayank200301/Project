using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SoundToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private Image icon;

    [Header("Icons")]
    [SerializeField] private Sprite soundIcon;
    [SerializeField] private Sprite muteIcon;

    [Header("Colors")]
    [SerializeField] private Color onColor = new Color(0.15f, 0.8f, 0.3f);
    [SerializeField] private Color offColor = new Color(0.9f, 0.2f, 0.2f);

    [Header("Handle Position")]
    [SerializeField] private float offPosition = -25f;
    [SerializeField] private float onPosition = 25f;

    [Header("Animation")]
    [SerializeField] private float slideSpeed = 12f;

    private Coroutine animationCoroutine;
    private bool isUpdating = false;

    private void Awake()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void Start()
    {
        // Load saved sound setting
        bool soundOn = PlayerPrefs.GetInt("Sound", 1) == 1;

        // Set Toggle state without triggering OnToggleChanged
        isUpdating = true;
        toggle.isOn = soundOn;
        isUpdating = false;

        // Update visual
        UpdateVisuals(soundOn, true);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (isUpdating)
            return;

        // Update visual
        UpdateVisuals(isOn, false);

        // Tell GameManager to change sound
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSound(isOn);
        }
    }

    private void UpdateVisuals(bool isOn, bool instant)
    {
        // Background
        if (background != null)
            background.color = isOn ? onColor : offColor;

        // Icon
        if (icon != null)
            icon.sprite = isOn ? soundIcon : muteIcon;

        // Handle
        if (handle != null)
        {
            float targetX = isOn ? onPosition : offPosition;

            if (instant)
            {
                Vector2 pos = handle.anchoredPosition;
                pos.x = targetX;
                handle.anchoredPosition = pos;
            }
            else
            {
                if (animationCoroutine != null)
                    StopCoroutine(animationCoroutine);

                animationCoroutine = StartCoroutine(
                    AnimateHandle(targetX)
                );
            }
        }
    }

    private IEnumerator AnimateHandle(float targetX)
    {
        while (Mathf.Abs(handle.anchoredPosition.x - targetX) > 0.1f)
        {
            Vector2 pos = handle.anchoredPosition;

            pos.x = Mathf.Lerp(
                pos.x,
                targetX,
                Time.unscaledDeltaTime * slideSpeed
            );

            handle.anchoredPosition = pos;

            yield return null;
        }

        Vector2 finalPos = handle.anchoredPosition;
        finalPos.x = targetX;
        handle.anchoredPosition = finalPos;
    }
}