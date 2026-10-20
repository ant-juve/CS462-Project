using UnityEngine;

// The player's EPD (electronic personal dosimeter). Added to the player by FirstPersonPlayer.
// Each frame it adds up the dose rate from every RadiationSource around the player and
// accumulates it into a total dose. EPDDisplay shows the numbers; it doesn't do anything to
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

    public event System.Action<DoseLevel> LevelChanged;

    void Update()
    {
        Vector3 position = transform.position + Vector3.up * measureHeight;
        float rate = backgroundRate;
        foreach (RadiationSource source in RadiationSource.Active)
            rate += source.RateAt(position);
        CurrentRate = rate;

        // deltaTime is zero while paused, so no dose builds up behind the pause menu
        TotalDose += rate / 3600f * timeScale * Time.deltaTime;
        UpdateLevel();
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
        UpdateLevel();
    }
}
