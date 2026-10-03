using UnityEngine;
using UnityEngine.UI;

namespace OMG.ArrowGo
{
    public class SlideToggle : MonoBehaviour
    {
        // ============================================================
        // REFERENCES
        // ============================================================

        [Header("References")]
        [SerializeField] private Toggle toggle;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image background;

        // ============================================================
        // SLIDE POSITIONS
        // ============================================================

        [Header("Slide Positions")]
        [SerializeField] private float offX = -40f;
        [SerializeField] private float onX = 40f;

        // ============================================================
        // ANIMATION
        // ============================================================

        [Header("Animation")]
        [SerializeField] private float speed = 12f;

        // ============================================================
        // COLORS
        // ============================================================

        [Header("Colors")]
        [SerializeField]
        private Color onColor = new Color(0.2f, 0.8f, 0.3f, 1f);

        [SerializeField]
        private Color offColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Color Animation")]
        [SerializeField] private float colorSpeed = 12f;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            FindReferences();
        }

        private void OnEnable()
        {
            FindReferences();

            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(OnToggleChanged);
                toggle.onValueChanged.AddListener(OnToggleChanged);
            }

            UpdateVisuals(true);
        }

        private void OnDisable()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(OnToggleChanged);
            }
        }

        private void Update()
        {
            if (toggle == null)
                return;

            // --------------------------------------------------------
            // KNOB
            // --------------------------------------------------------

            if (knob != null)
            {
                float targetX = toggle.isOn ? onX : offX;

                Vector2 position = knob.anchoredPosition;

                position.x = Mathf.Lerp(
                    position.x,
                    targetX,
                    Time.unscaledDeltaTime * Mathf.Max(0f, speed)
                );

                knob.anchoredPosition = position;
            }

            // --------------------------------------------------------
            // BACKGROUND COLOR
            // --------------------------------------------------------

            if (background != null)
            {
                Color targetColor = toggle.isOn
                    ? onColor
                    : offColor;

                background.color = Color.Lerp(
                    background.color,
                    targetColor,
                    Time.unscaledDeltaTime * Mathf.Max(0f, colorSpeed)
                );
            }
        }

        // ============================================================
        // FIND REFERENCES
        // ============================================================

        private void FindReferences()
        {
            if (toggle == null)
            {
                toggle = GetComponent<Toggle>();
            }
        }

        // ============================================================
        // TOGGLE EVENT
        // ============================================================

        private void OnToggleChanged(bool value)
        {
            UpdateVisuals(false);
        }

        // ============================================================
        // UPDATE VISUALS
        // ============================================================

        private void UpdateVisuals(bool instant)
        {
            if (toggle == null)
                return;

            bool value = toggle.isOn;

            // --------------------------------------------------------
            // KNOB
            // --------------------------------------------------------

            if (knob != null && instant)
            {
                Vector2 position = knob.anchoredPosition;

                position.x = value ? onX : offX;

                knob.anchoredPosition = position;
            }

            // --------------------------------------------------------
            // BACKGROUND
            // --------------------------------------------------------

            if (background != null && instant)
            {
                background.color = value
                    ? onColor
                    : offColor;
            }
        }

        // ============================================================
        // PUBLIC SET VALUE
        // ============================================================

        public void SetValue(bool value, bool instant = true)
        {
            FindReferences();

            if (toggle == null)
                return;

            // IMPORTANT:
            // Do not call Toggle event listeners when GameManager
            // is only updating the visual state.
            toggle.SetIsOnWithoutNotify(value);

            if (instant)
            {
                UpdateVisuals(true);
            }
        }

        // ============================================================
        // GET VALUE
        // ============================================================

        public bool GetValue()
        {
            return toggle != null && toggle.isOn;
        }

        // ============================================================
        // SET POSITIONS
        // ============================================================

        public void SetPositions(float offPosition, float onPosition)
        {
            offX = offPosition;
            onX = onPosition;

            UpdateVisuals(true);
        }
    }
}