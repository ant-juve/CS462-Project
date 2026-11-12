using UnityEngine;

// The robots' motor hum and detection alarm, synthesized at runtime so they need no audio files.
// RobotController adds this automatically when missing. The hum loops continuously and pitches up
// as the robot moves; the alarm blares once when the robot switches from Patrol to Pursue.
public class RobotAudio : MonoBehaviour
{
    [Header("Motor hum")]
    [Tooltip("Hum volume when the robot is standing still")]
    [Range(0f, 1f)] public float idleVolume = 0.25f;
    [Tooltip("Hum volume at full speed")]
    [Range(0f, 1f)] public float moveVolume = 0.55f;
    [Tooltip("Hum pitch when standing still and at full speed")]
    public float idlePitch = 0.75f;
    public float movePitch = 0.9f;
    [Tooltip("How quickly the hum responds to speed changes")]
    public float responseSpeed = 6f;

    [Header("Detection alarm")]
    [Range(0f, 1f)] public float alertVolume = 0.9f;
    [Tooltip("Playback pitch of the alarm; lower values make it deeper and slower")]
    [Range(0.1f, 2f)] public float alertPitch = 0.5f;
    [Tooltip("Seconds between alarms, so a flickering detection doesn't spam it")]
    public float alertCooldown = 2.5f;
    [Tooltip("Distance where the detection alarm becomes inaudible")]
    public float alertMaxDistance = 60f;

    [Header("3D audio")]
    [Tooltip("Distance where the robot is at full volume")]
    public float minDistance = 2f;
    [Tooltip("Distance where the robot becomes inaudible")]
    public float maxDistance = 30f;

    private static AudioClip humClip;
    private static AudioClip alertClip;

    private RobotController robot;
    private CharacterController controller;
    private AudioSource source;
    private AudioSource alertSource;
    private float speedForHum;
    private float nextAlertTime;
    private RobotController.State lastState;

    void Awake()
    {
        robot = GetComponent<RobotController>();
        if (robot == null)
        {
            enabled = false;
            return;
        }

        controller = GetComponent<CharacterController>();

        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.volume = idleVolume;
        source.pitch = idlePitch;
        source.clip = HumClip();
        source.Play();

        alertSource = gameObject.AddComponent<AudioSource>();
        alertSource.playOnAwake = false;
        alertSource.loop = false;
        alertSource.spatialBlend = 1f;
        alertSource.rolloffMode = AudioRolloffMode.Logarithmic;
        alertSource.minDistance = minDistance;
        alertSource.maxDistance = alertMaxDistance;
        alertSource.dopplerLevel = 0f;
        alertSource.volume = 1f;
        alertSource.pitch = alertPitch;

        lastState = robot.CurrentState;
    }

    void Update()
    {
        // CharacterController velocity includes gravity, so only use the flat part
        Vector3 velocity = controller != null ? controller.velocity : Vector3.zero;
        velocity.y = 0f;
        float targetSpeed = Mathf.Clamp01(velocity.magnitude / Mathf.Max(robot.MoveSpeed, 0.01f));
        speedForHum = Mathf.MoveTowards(speedForHum, targetSpeed, responseSpeed * Time.deltaTime);

        source.pitch = Mathf.Lerp(idlePitch, movePitch, speedForHum);
        source.volume = Mathf.Lerp(idleVolume, moveVolume, speedForHum);
        source.mute = PauseMenuController.IsPaused;
        alertSource.pitch = alertPitch;
        alertSource.mute = PauseMenuController.IsPaused;

        // Alert only on the edge into Pursue, with a cooldown in case detection flickers
        RobotController.State state = robot.CurrentState;
        if (state == RobotController.State.Pursue && lastState == RobotController.State.Patrol
            && Time.time >= nextAlertTime)
        {
            alertSource.PlayOneShot(AlertClip(), alertVolume);
            nextAlertTime = Time.time + alertCooldown;
        }
        lastState = state;
    }

    // A low motor tone with a slow wobble and a little filtered noise for mechanical grit
    static AudioClip HumClip()
    {
        if (humClip != null)
            return humClip;

        const float Length = 1f;
        int sampleRate = AudioSettings.outputSampleRate;
        int sampleCount = Mathf.CeilToInt(Length * sampleRate);
        int fade = Mathf.Max(1, sampleRate / 100); // 10 ms crossfade at the loop point
        int total = sampleCount + fade;
        float[] samples = new float[total];
        System.Random random = new System.Random(462);
        float lowPassed = 0f;

        // Whole cycles over the loop so the tone joins back up without a click. The higher
        // harmonics matter: the 55 Hz fundamental alone barely comes out of small speakers.
        float motor = Mathf.Round(55f * Length) / Length;
        float harmonic = Mathf.Round(110f * Length) / Length;
        float third = Mathf.Round(220f * Length) / Length;
        float whine = Mathf.Round(440f * Length) / Length;
        float wobble = Mathf.Round(2f * Length) / Length;
        float chug = Mathf.Round(7f * Length) / Length;

        for (int i = 0; i < total; i++)
        {
            float t = (float)i / sampleRate;
            float tone = 0.35f * Mathf.Sin(2f * Mathf.PI * motor * t)
                       + 0.35f * Mathf.Sin(2f * Mathf.PI * harmonic * t)
                       + 0.2f * Mathf.Sin(2f * Mathf.PI * third * t)
                       + 0.1f * Mathf.Sin(2f * Mathf.PI * whine * t);
            float wobbleAmount = 1f + 0.1f * Mathf.Sin(2f * Mathf.PI * wobble * t);
            float chugAmount = 1f + 0.05f * Mathf.Sin(2f * Mathf.PI * chug * t);

            float noise = (float)random.NextDouble() * 2f - 1f;
            lowPassed += (noise - lowPassed) * 0.015f;

            samples[i] = tone * wobbleAmount * chugAmount + lowPassed * 0.45f;
        }

        // Crossfade the tail into the head so the loop boundary is inaudible
        for (int i = 0; i < fade; i++)
        {
            float a = (float)i / fade;
            samples[i] = samples[i] * a + samples[sampleCount + i] * (1f - a);
        }

        float[] clipSamples = new float[sampleCount];
        System.Array.Copy(samples, clipSamples, sampleCount);
        Normalize(clipSamples, 0.8f);

        humClip = AudioClip.Create("RobotHum", sampleCount, 1, sampleRate, false);
        humClip.SetData(clipSamples, 0);
        return humClip;
    }

    // A two-tone "wee-woo" alarm: alternating high/low notes, sustained and harsh
    static AudioClip AlertClip()
    {
        if (alertClip != null)
            return alertClip;

        const float Length = 1.2f;
        int sampleRate = AudioSettings.outputSampleRate;
        int sampleCount = Mathf.CeilToInt(Length * sampleRate);
        float[] samples = new float[sampleCount];

        const float NoteLength = 0.2f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int noteIndex = Mathf.FloorToInt(t / NoteLength);
            float noteTime = t - noteIndex * NoteLength;
            float frequency = (noteIndex % 2 == 0) ? 1000f : 750f;

            // Short gaps between notes keep the alarm punchy without clicking
            float attack = Mathf.Clamp01(noteTime / 0.008f);
            float release = Mathf.Clamp01((NoteLength - noteTime) / 0.015f);
            float envelope = attack * release;

            float sine = Mathf.Sin(2f * Mathf.PI * frequency * t);
            float sub = Mathf.Sin(2f * Mathf.PI * (frequency * 0.5f) * t);
            samples[i] = (0.45f * sine + 0.4f * Mathf.Sign(sine) + 0.15f * sub) * envelope;
        }

        Normalize(samples, 0.95f);

        alertClip = AudioClip.Create("RobotAlarm", sampleCount, 1, sampleRate, false);
        alertClip.SetData(samples, 0);
        return alertClip;
    }

    static void Normalize(float[] samples, float peak)
    {
        float max = 0f;
        for (int i = 0; i < samples.Length; i++)
            max = Mathf.Max(max, Mathf.Abs(samples[i]));

        if (max <= 0.0001f)
            return;

        float scale = peak / max;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= scale;
    }
}
