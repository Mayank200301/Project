using UnityEngine;

public class DayNightManager : MonoBehaviour
{
[Header("Time")]
[Range(0,24)]
public float timeOfDay = 8f;

public float dayLength = 300f;

[Header("Sun")]
public Light sun;

[Header("Colors")]
public Gradient lightColor;

[Header("Intensity")]
public AnimationCurve lightIntensity;

[Header("Fog")]
public Gradient fogColor;

public float dayFogDensity = 0.002f;
public float nightFogDensity = 0.015f;

void Update()
{
    timeOfDay += Time.deltaTime * (24f / dayLength);

    if (timeOfDay >= 24)
        timeOfDay = 0;

    UpdateSun();
}

void UpdateSun()
{
    float t = timeOfDay / 24f;

    float sunAngle = t * 360f - 90f;

    sun.transform.rotation =
        Quaternion.Euler(sunAngle,170,0);

    sun.color = lightColor.Evaluate(t);

    sun.intensity =
        lightIntensity.Evaluate(t);

    RenderSettings.fog = true;

    RenderSettings.fogColor =
        fogColor.Evaluate(t);

    RenderSettings.fogDensity =
        Mathf.Lerp(
            nightFogDensity,
            dayFogDensity,
            sun.intensity
        );
}


}