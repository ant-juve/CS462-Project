using UnityEngine;

// Makes the player cough as their radiation dose builds up. Nothing below the Dosimeter's
// warning level; from there, coughing fits come more often and last longer the closer the dose
// gets to lethal. There's no sound: FirstPersonArms listens to FitStarted/Coughed to raise a
// fist to the mouth and jolt the head. Added to the player by FirstPersonPlayer.
public class PlayerCough : MonoBehaviour
{
    [Header("How often")]
    [Tooltip("Seconds between coughing fits right at the warning dose")]
    public float intervalAtWarning = 15f;
    [Tooltip("Seconds between coughing fits at the lethal dose")]
    public float intervalAtLethal = 4f;
    [Tooltip("Most coughs in one fit, reached at the lethal dose")]
    public int maxCoughsPerFit = 6;
    [Tooltip("Pause at the start of a fit before the first cough, so the fist can come up to the mouth")]
    public float leadIn = 0.3f;

    // True from the start of a fit until its last cough finishes
    public bool InFit { get; private set; }
    // How bad the coughing is: 0 below the warning dose, rising to 1 at lethal. Drives how often
    // and how long the fits are, and (in FirstPersonArms) how hard the head jerks
    public float Severity { get; private set; }
    public event System.Action FitStarted;
    public event System.Action Coughed;

    private Dosimeter dosimeter;

    private float nextFitTimer = -1f; // counts down to the next fit; negative while dose is below warning
    private int coughsLeft;
    private float nextCoughTimer;     // until the next cough, or until the fit ends after the last one

    void Awake()
    {
        dosimeter = GetComponentInParent<Dosimeter>();
    }

    void Update()
    {
        if (dosimeter == null || PauseMenuController.IsPaused)
            return;

        float dose = dosimeter.TotalDose;
        Severity = dose < dosimeter.doseWarning ? 0f : Mathf.InverseLerp(dosimeter.doseWarning, dosimeter.doseLethal, dose);

        if (InFit)
        {
            UpdateFit();
            return;
        }

        if (dose < dosimeter.doseWarning)
        {
            nextFitTimer = -1f;
            return;
        }

        // First fit comes a few seconds after crossing the warning level, so the player notices the link
        if (nextFitTimer < 0f)
            nextFitTimer = Random.Range(2f, 5f);

        nextFitTimer -= Time.deltaTime;
        if (nextFitTimer <= 0f)
            StartFit(Severity);
    }

    void UpdateFit()
    {
        nextCoughTimer -= Time.deltaTime;
        if (nextCoughTimer > 0f)
            return;

        if (coughsLeft > 0)
            Cough();
        else
            InFit = false;
    }

    void StartFit(float severity)
    {
        int most = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, maxCoughsPerFit, severity)));
        coughsLeft = Random.Range(Mathf.Max(1, most - 1), most + 1);
        nextCoughTimer = leadIn;
        InFit = true;

        float interval = Mathf.Lerp(intervalAtWarning, intervalAtLethal, severity);
        nextFitTimer = interval * Random.Range(0.7f, 1.3f);
        FitStarted?.Invoke();
    }

    void Cough()
    {
        float length = Random.Range(0.4f, 0.5f); // how long one cough lasts
        coughsLeft--;
        // Coughs in a fit run together; worse dose, less time to catch a breath between them.
        // After the last one, wait for it to finish before ending the fit
        float gap = length * Random.Range(0.75f, 0.9f) * Mathf.Lerp(1f, 0.7f, Severity);
        nextCoughTimer = coughsLeft > 0 ? Mathf.Max(0.25f, gap) : length;
        Coughed?.Invoke();
    }
}
