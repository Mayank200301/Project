using UnityEngine;

public class AutomaticHeadlight : MonoBehaviour
{
    [Header("Day Night Manager")]
    public DayNightManager dayNightManager;

    [Header("Headlights")]
    public Light leftHeadlight;
    public Light rightHeadlight;

    [Header("Night Settings")]
    [Range(0f, 24f)]
    public float headlightsOnTime = 18.5f;

    [Range(0f, 24f)]
    public float headlightsOffTime = 6.0f;

    private bool headlightsOn;

    private void Start()
    {
        UpdateHeadlights(true);
    }

    private void Update()
    {
        if (dayNightManager == null)
            return;

        UpdateHeadlights(false);
    }

    private void UpdateHeadlights(bool forceUpdate)
    {
        float currentTime = dayNightManager.timeOfDay;

        bool shouldBeOn;

        // Night:
        // 18:30 → 24:00
        // 00:00 → 06:00

        if (headlightsOnTime > headlightsOffTime)
        {
            shouldBeOn =
                currentTime >= headlightsOnTime ||
                currentTime < headlightsOffTime;
        }
        else
        {
            shouldBeOn =
                currentTime >= headlightsOnTime &&
                currentTime < headlightsOffTime;
        }

        // Only change the lights when the state changes
        if (forceUpdate || shouldBeOn != headlightsOn)
        {
            headlightsOn = shouldBeOn;

            SetHeadlights(headlightsOn);
        }
    }

    private void SetHeadlights(bool state)
    {
        if (leftHeadlight != null)
            leftHeadlight.enabled = state;

        if (rightHeadlight != null)
            rightHeadlight.enabled = state;

        Debug.Log(
            "Automatic Headlights: " +
            (state ? "ON" : "OFF")
        );
    }
}