using UnityEngine;
using TMPro;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [Range(0f, 24f)] public float currentTime = 12f;
    public float timeSpeed = 60f;

    [Header("References")]
    public Light sunLight;
    public Transform moonObject;  // Your sphere for the moon
    public TextMeshProUGUI timeDisplay;

    [Header("Light Settings")]
    public float dayIntensity = 1f;
    public float nightIntensity = 0.1f;

    [Header("Sky Colors")]
    public Color dayColor = new Color(0.5f, 0.7f, 1f);
    public Color sunsetColor = new Color(1f, 0.6f, 0.3f);
    public Color nightColor = new Color(0.1f, 0.1f, 0.2f);

    [Header("Orbit Settings")]
    public float orbitRadius = 500f; // Distance of sun/moon from world center

    private float timeOfDay;

    [Header("Day/Night Settings")]
    [Range(0f, 24f)] public float sunriseTime = 6f;   // when sun starts rising
    [Range(0f, 24f)] public float sunsetTime = 22f;   // when sun starts setting


    void Update()
    {
        currentTime += (Time.deltaTime / timeSpeed);
        if (currentTime >= 24f) currentTime = 0f;

        timeOfDay = currentTime / 24f;

        UpdateSun();
        UpdateMoon();
        UpdateLighting();
        UpdateSkyColor();
        UpdateTimeDisplay();
    }

    void UpdateSun()
    {
        if (sunLight == null) return;

        float dayLength = (sunsetTime - sunriseTime + 24f) % 24f; // handle wrap around midnight
        float nightLength = 24f - dayLength;

        // Find where we are in the day/night cycle
        float angle;
        if (IsDaytime())
        {
            // Progress of day between sunrise and sunset
            float t = (currentTime - sunriseTime + 24f) % 24f / dayLength;
            angle = Mathf.Lerp(0f, 180f, t); // horizon east -> horizon west
        }
        else
        {
            // Progress of night between sunset and sunrise
            float t = (currentTime - sunsetTime + 24f) % 24f / nightLength;
            angle = Mathf.Lerp(180f, 360f, t); // horizon west -> horizon east
        }

        Vector3 sunPos = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * orbitRadius,
            Mathf.Sin(angle * Mathf.Deg2Rad) * orbitRadius,
            0f
        );

        sunLight.transform.position = sunPos;
        sunLight.transform.LookAt(Vector3.zero);
    }

    void UpdateMoon()
    {
        if (moonObject == null) return;

        // Moon is always opposite sun
        Vector3 moonPos = -sunLight.transform.position.normalized * orbitRadius;
        moonObject.position = moonPos;
        moonObject.LookAt(Vector3.zero);
    }

    bool IsDaytime()
    {
        if (sunriseTime < sunsetTime)
        {
            return currentTime >= sunriseTime && currentTime < sunsetTime;
        }
        else // wraps over midnight
        {
            return currentTime >= sunriseTime || currentTime < sunsetTime;
        }
    }



    void UpdateLighting()
    {
        if (sunLight == null) return;

        float intensity = nightIntensity;

        bool isDay;

        // Case 1: normal (sunrise before sunset)
        if (sunriseTime < sunsetTime)
        {
            isDay = currentTime >= sunriseTime && currentTime < sunsetTime;
        }
        // Case 2: crosses midnight (e.g. sunset at 23, sunrise at 6)
        else
        {
            isDay = currentTime >= sunriseTime || currentTime < sunsetTime;
        }

        if (isDay)
        {
            // Sunrise transition
            if (currentTime >= sunriseTime && currentTime < sunriseTime + 2f)
            {
                float t = (currentTime - sunriseTime) / 2f;
                intensity = Mathf.Lerp(nightIntensity, dayIntensity, t);
            }
            // Sunset transition
            else if (currentTime >= sunsetTime - 2f && currentTime < sunsetTime)
            {
                float t = (currentTime - (sunsetTime - 2f)) / 2f;
                intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
            }
            else
            {
                intensity = dayIntensity;
            }
        }
        else
        {
            intensity = nightIntensity;
        }

        sunLight.intensity = intensity;
    }



    void UpdateSkyColor()
    {
        Color currentColor;

        // Sunrise blend
        if (currentTime >= sunriseTime && currentTime < sunriseTime + 2f)
        {
            float t = (currentTime - sunriseTime) / 2f;
            currentColor = Color.Lerp(nightColor, sunsetColor, t);
        }
        // Day
        else if (currentTime >= sunriseTime + 2f && currentTime < sunsetTime - 2f)
        {
            float t = Mathf.Clamp01((currentTime - (sunriseTime + 2f)) / 2f);
            currentColor = Color.Lerp(sunsetColor, dayColor, t);
        }
        // Sunset blend
        else if (currentTime >= sunsetTime - 2f && currentTime < sunsetTime)
        {
            float t = (currentTime - (sunsetTime - 2f)) / 2f;
            currentColor = Color.Lerp(dayColor, sunsetColor, t);
        }
        // Dusk
        else if (currentTime >= sunsetTime && currentTime < sunsetTime + 2f)
        {
            float t = (currentTime - sunsetTime) / 2f;
            currentColor = Color.Lerp(sunsetColor, nightColor, t);
        }
        else
        {
            currentColor = nightColor;
        }

        RenderSettings.ambientLight = currentColor;
    }


    void UpdateTimeDisplay()
    {
        if (timeDisplay == null) return;

        int hours = Mathf.FloorToInt(currentTime);
        int minutes = Mathf.FloorToInt((currentTime - hours) * 60f);

        timeDisplay.text = string.Format("{0:00}:{1:00}", hours, minutes);
    }
}
