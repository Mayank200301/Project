using System.Collections;
using TMPro;
using UnityEngine;

public class MissionUI : MonoBehaviour
{
    public static MissionUI Instance;

    [SerializeField] private TextMeshProUGUI missionText;
    [SerializeField] private float showTime = 2f;
    [SerializeField] private float fadeSpeed = 3f;

    [Header("Message Colors")]
    public Color goColor = Color.red;
    public Color pickupColor = Color.yellow;
    public Color deliveryColor = Color.green;

    [Header("Timer")]
    [SerializeField]private TextMeshProUGUI timerText;

    Coroutine currentRoutine;

    private void Awake()
    {
        Instance = this;
        missionText.alpha = 0;
    }

    public void ShowMessage(string message, Color color)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowRoutine(message, color));
    }

    IEnumerator ShowRoutine(string message, Color color)
    {
        missionText.text = message;

        // Set text color (keep current alpha)
        missionText.color = new Color(color.r, color.g, color.b, 0);

        // Fade In
        while (missionText.alpha < 1)
        {
            missionText.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(showTime);

        // Fade Out
        while (missionText.alpha > 0)
        {
            missionText.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        missionText.alpha = 0;
    }

    public void ShowTimer(float time)
    {
        System.TimeSpan timeSpan =
            System.TimeSpan.FromSeconds(time);

        timerText.text = string.Format("Time : {0:00}:{1:00}",timeSpan.Minutes,timeSpan.Seconds);

        timerText.alpha = 1;
    }

    public void HideTimer()
    {
        timerText.text = "";
        timerText.alpha = 0;
    }

}