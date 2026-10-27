using UnityEngine;

// The player's EPD (electronic personal dosimeter). Added to the player by FirstPersonPlayer.
// Each frame it adds up the dose rate from every RadiationSource around the player and
// accumulates it into a total dose; RemoveDose (used by RadiationShot) takes some back off. EPDDisplay shows the numbers; it doesn't do anything to
// the player by itself, other scripts can watch Level for that.
public class Dosimeter : MonoBehaviour
{
    public enum DoseLevel { Normal, Warning, Danger, Lethal }

    [Tooltip("Game seconds that pass per real second. Rates are per hour but a run lasts minutes, " +
             "so at 60 a 5,000 mrem/hr spill adds about 83 mrem every real second")]
    public float timeScale = 60f;
    [Tooltip("Natural background radiation everywhere, in mrem/hr")]
    public float backgroundRate = 0.5f;
    [Tooltip("Height above the player's feet where dose is measured (roughly the chest)")]
    public float measureHeight = 1.2f;

    [Header("Alarm set points")]
    [Tooltip("Dose rate that sets off the rate alarm, in mrem/hr")]
    public float rateAlarm = 1000f;
    [Tooltip("Total dose levels, in mrem")]
    public float doseWarning = 500f;
    public float doseDanger = 2000f;
    public float doseLethal = 5000f;

    // mrem/hr at the player right now
    public float CurrentRate { get; private set; }
    // mrem accumulated so far
    public float TotalDose { get; private set; }
    public DoseLevel Level { get; private set; }
    public bool RateAlarm => CurrentRate >= rateAlarm;
    // True while a shot (or anything else calling RemoveDose) is still clearing dose
    public bool IsClearing => clearRemaining > 0f;

    public event System.Action<DoseLevel> LevelChanged;

    private float clearRemaining; // mrem still to be taken off
    private float clearRate;      // mrem taken off per real second
    private float clearDelay;     // seconds before clearing starts

    void Update()
    {
        Vector3 position = transform.position + Vector3.up * measureHeight;
        float rate = backgroundRate;
        foreach (RadiationSource source in RadiationSource.Active)
            rate += source.RateAt(position);
        CurrentRate = rate;

        // deltaTime is zero while paused, so no dose builds up behind the pause menu
        float change = rate / 3600f * timeScale * Time.deltaTime;
        if (clearDelay > 0f)
            clearDelay -= Time.deltaTime;
        else if (clearRemaining > 0f)
        {
            float cleared = Mathf.Min(clearRemaining, clearRate * Time.deltaTime);
            clearRemaining -= cleared;
            change -= cleared;
        }
        TotalDose = Mathf.Max(0f, TotalDose + change);
        UpdateLevel();
    }

    // Takes `amount` mrem off the total dose, spread evenly over `duration` real seconds, starting
    // after `delay` seconds. A second call while one is still running adds to what's left instead
    // of replacing it, and keeps whichever clearing speed is faster
    public void RemoveDose(float amount, float duration, float delay = 0f)
    {
        if (amount <= 0f)
            return;
        if (duration <= 0f && delay <= 0f)
        {
            TotalDose = Mathf.Max(0f, TotalDose - amount);
            UpdateLevel();
            return;
        }

        // Already clearing: don't hold up what's running for the new delay
        if (!IsClearing)
            clearDelay = delay;
        duration = Mathf.Max(duration, 0.01f);
        clearRate = Mathf.Max(IsClearing ? clearRate : 0f, amount / duration);
        clearRemaining += amount;
    }

    void UpdateLevel()
    {
        DoseLevel level = TotalDose >= doseLethal ? DoseLevel.Lethal
                        : TotalDose >= doseDanger ? DoseLevel.Danger
                        : TotalDose >= doseWarning ? DoseLevel.Warning
                        : DoseLevel.Normal;
        if (level == Level)
            return;

        Level = level;
        LevelChanged?.Invoke(level);
    }

    public void ResetDose()
    {
        TotalDose = 0f;
        clearRemaining = 0f;
        UpdateLevel();
    }
}
