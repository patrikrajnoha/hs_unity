using UnityEngine;
using TMPro; // Make sure TextMeshPro is imported

[System.Serializable]
public enum Mood
{
    Neutral,
    Good,
    Bad,
    Angry,
    Happy,
    Sad
}

[System.Serializable]
public class PlayerStats : MonoBehaviour
{
    //private GameObject panel;
    private TextMeshProUGUI statsText;

    private GameMaster gm;

    private string lastStatsText = ""; // cache to avoid unnecessary updates

    private void Start()
    {
        gm = GetComponent<GameMaster>();
        /*
        panel = GameObject.FindGameObjectWithTag("statPan");
        if (panel != null)
            statsText = panel.GetComponentInChildren<TextMeshProUGUI>();

        panel.SetActive(false);
        */
        UpdateStatsUI(); // initialize once
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.R))
        {
            //panel.SetActive(true);
            UpdateStatsUI();
        }
        else
        {
            //panel.SetActive(false);
        }
    }

    private void UpdateStatsUI()
    {
        if (statsText == null) return;

        string newStats =
            $"{Colorize("Health", health)}\n" +
            $"{Colorize("Energy", energy)}\n" +
            $"{Colorize("Hunger", hunger, invert: true)}\n" +
            $"Mood: {currentMood}\n" +
            $"{Colorize("Stealth", stealth)}\n" +
            $"{Colorize("Suspicion", suspicion, invert: true)}\n" +
            $"{Colorize("Reputation", reputation, custom: true)}\n" +
            $"Crime Level: {crimeLevel}\n" +
            $"{Colorize("Money", money)}\n" +
            $"{Colorize("Tool Quality", toolQuality)}\n" +
            $"{Colorize("Addiction", addiction, invert: true)}\n" +
            $"{Colorize("Stress", stress, invert: true)}";

        if (newStats != lastStatsText) // only update when it changes
        {
            statsText.text = newStats;
            lastStatsText = newStats;
        }
    }

    // Apply TMP rich text color depending on value
    private string Colorize(string label, float value, bool invert = false, bool custom = false)
    {
        string color = "white";

        if (custom) // reputation (-100 to 100)
        {
            if (value < -50) color = "red";
            else if (value < 0) color = "orange";
            else if (value > 50) color = "green";
            else color = "yellow";
        }
        else
        {
            float v = invert ? (100 - value) : value;

            if (v > 70) color = "green";
            else if (v > 30) color = "yellow";
            else color = "red";
        }

        return $"<color={color}>{label}: {value:F0}</color>";
    }

    // --- Stats ---
    [Header("Core Stats")] public float health = 100f;
    public float energy = 100f;
    public float hunger = 0f;
    public Mood currentMood = Mood.Neutral;

    [Header("Skills / Abilities")][Range(0, 100)] public float stealth = 0f;

    [Header("Reputation / Risk")][Range(0, 100)] public float suspicion = 0f;
    [Range(-100, 100)] public float reputation = 0f;
    public int crimeLevel = 0;

    [Header("Resources")] public int money = 0;
    public float toolQuality = 100f;
    public float addiction = 0f;
    public float stress = 0f;

    // Methods to update stats
    public void ChangeHealth(float amount)
    {
        float prevHealth = health;
        health = Mathf.Clamp(health + amount, 0f, 100f);
        if (health <= 0f && prevHealth > 0f)
        {
            GameLogger.LogWarning("player", "player_downed", "Player health depleted to 0");
        }
    }

    public void ChangeEnergy(float amount)
    {
        energy = Mathf.Clamp(energy + amount, 0f, 100f);
    }

    public void ChangeHunger(float amount)
    {
        hunger = Mathf.Clamp(hunger + amount, 0f, 100f);
    }

    public void ChangeStealth(float amount)
    {
        stealth = Mathf.Clamp(stealth + amount, 0f, 100f);
    }

    public void ChangeSuspicion(float amount)
    {
        suspicion = Mathf.Clamp(suspicion + amount, 0f, 100f);
    }

    public void ChangeReputation(float amount)
    {
        reputation = Mathf.Clamp(reputation + amount, -100f, 100f);
    }

    public void ChangeCrimeLevel(int amount)
    {
        crimeLevel += amount;
        if (crimeLevel < 0) crimeLevel = 0;
    }

    public void ChangeMoney(int amount)
    {
        money += amount;
        if (money < 0) money = 0;
    }
    public bool CanAfford(int price)
    {
        return money >= price;
    }

    public void ChangeToolQuality(float amount)
    {
        toolQuality = Mathf.Clamp(toolQuality + amount, 0f, 100f);
    }

    public void ChangeAddiction(float amount)
    {
        addiction = Mathf.Clamp(addiction + amount, 0f, 100f);
    }

    public void ChangeStress(float amount)
    {
        stress = Mathf.Clamp(stress + amount, 0f, 100f);
    }

    // Mood setter
    public void SetMood(Mood newMood)
    {
        currentMood = newMood;
    }

    public void HasIllegalItem()
    {
        gm.HeDoes();
    }

}
