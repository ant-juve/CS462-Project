using UnityEngine;

public class LightFlash : MonoBehaviour
{
    [SerializeField]
    [Range(0.01f, 5f)]
    float fadeDuration = 1f; // Seconds for each fade direction

    Light[] lights;
    bool fadingDown = true;
    float brightness = 10f;

    void Start()
    {
        lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        ApplyBrightness();
    }

    void Update()
    {
        if (fadingDown)
        {
            fadeOut();
        }
        else
        {
            fadeIn();
        }

        ApplyBrightness();
    }

    void fadeOut()
    {
        brightness -= (9f / fadeDuration) * Time.deltaTime;
        brightness = Mathf.Clamp(brightness, 1f, 10f);

        if (brightness <= 1f)
        {
            fadingDown = false;
        }
    }

    void fadeIn()
    {
        brightness += (9f / fadeDuration) * Time.deltaTime;
        brightness = Mathf.Clamp(brightness, 1f, 10f);

        if (brightness >= 10f)
        {
            fadingDown = true;
        }
    }

    void ApplyBrightness()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].intensity = brightness;
        }
    }
}
