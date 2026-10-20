using UnityEngine;

// Makes the player cough as their radiation dose builds up. Nothing below the Dosimeter's
// warning level; from there, coughing fits come more often and last longer the closer the dose
// gets to lethal, and near the top each fit can start with a wheezing breath in.
// FirstPersonArms listens to FitStarted/Coughed to raise a fist to the mouth and jolt the head.
// Added to the player by FirstPersonPlayer, on its own child object with its own AudioSource.
//
// Sounds are loaded from Resources, so just drop audio files into these folders:
//   Assets/Resources/Audio/Coughs/   one cough per file works best (a fit is several of them)
//   Assets/Resources/Audio/Breaths/  optional wheezing breaths in, used when badly dosed
// Without any sounds the player still coughs, silently.
public class PlayerCough : MonoBehaviour
{
    private const string CoughFolder = "Audio/Coughs";
    private const string BreathFolder = "Audio/Breaths";

    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("How often")]
    [Tooltip("Seconds between coughing fits right at the warning dose")]
    public float intervalAtWarning = 15f;
    [Tooltip("Seconds between coughing fits at the lethal dose")]
    public float intervalAtLethal = 4f;
    [Tooltip("Most coughs in one fit, reached at the lethal dose. Set to 1 if each file is already a whole fit")]
    public int maxCoughsPerFit = 6;
    [Tooltip("Pause at the start of a fit before the first cough, so the fist can come up to the mouth")]
    public float leadIn = 0.3f;

    // True from the start of a fit until its last cough finishes
    public bool InFit { get; private set; }
    // How bad the coughing is: 0 below the warning dose, rising to 1 at lethal. Drives how often
    // and how long the fits are, how loud, and (in FirstPersonArms) how hard the head jerks
    public float Severity { get; private set; }
    public event System.Action FitStarted;
    public event System.Action Coughed;

    private Dosimeter dosimeter;
    private AudioSource source;
    private AudioClip[] coughs;
    private AudioClip[] breaths;

    private float nextFitTimer = -1f; // counts down to the next fit; negative while dose is below warning
    private int coughsLeft;
    private float nextCoughTimer;     // until the next cough, or until the fit ends after the last one
    private float fitVolume;
    private int lastCough = -1;

    void Awake()
    {
        dosimeter = GetComponentInParent<Dosimeter>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // it's the player, so no 3D positioning

        coughs = Resources.LoadAll<AudioClip>(CoughFolder);
        breaths = Resources.LoadAll<AudioClip>(BreathFolder);
        if (coughs.Length == 0)
            Debug.Log($"PlayerCough: no cough sounds in Assets/Resources/{CoughFolder}, coughing will be silent");
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
            PlayCough();
        else
            InFit = false;
    }

    void StartFit(float severity)
    {
        int most = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, maxCoughsPerFit, severity)));
        coughsLeft = Random.Range(Mathf.Max(1, most - 1), most + 1);
        fitVolume = volume * Mathf.Lerp(0.55f, 1f, severity);
        nextCoughTimer = leadIn;
        InFit = true;

        // Badly dosed: a wheezing breath in before the fit
        if (severity > 0.5f && breaths.Length > 0)
        {
            AudioClip breath = breaths[Random.Range(0, breaths.Length)];
            source.PlayOneShot(breath, fitVolume * 0.7f);
            nextCoughTimer = Mathf.Max(leadIn, breath.length * 0.85f);
        }

        float interval = Mathf.Lerp(intervalAtWarning, intervalAtLethal, severity);
        nextFitTimer = interval * Random.Range(0.7f, 1.3f);
        FitStarted?.Invoke();
    }

    void PlayCough()
    {
        float length = Random.Range(0.4f, 0.5f); // how long a silent cough lasts
        if (coughs.Length > 0)
        {
            // Avoid the same cough twice in a row when there's a choice, it sounds robotic
            int index = Random.Range(0, coughs.Length);
            if (index == lastCough && coughs.Length > 1)
                index = (index + 1) % coughs.Length;
            lastCough = index;

            source.PlayOneShot(coughs[index], fitVolume);
            length = coughs[index].length;
        }

        coughsLeft--;
        fitVolume *= 0.85f; // each cough in a fit runs a little more out of breath
        // Next one starts just before this one finishes, so they run together like a real fit;
        // after the last one, wait for it to finish before ending the fit
        // Worse dose, less time to catch a breath between coughs
        float gap = length * Random.Range(0.75f, 0.9f) * Mathf.Lerp(1f, 0.7f, Severity);
        nextCoughTimer = coughsLeft > 0 ? Mathf.Max(0.25f, gap) : length;
        Coughed?.Invoke();
    }
}
