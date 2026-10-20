using UnityEngine;

// The radiation crackle. Clicks are synthesized on the audio thread, no sound files needed.
// Like a real Geiger counter, each click happens at random and the average click rate is
// proportional to the dose rate: a few ticks near the edge of a hotspot, a frantic crackle
// in the middle of a spill. Added to the player by FirstPersonPlayer, on its own child object
// so it doesn't share an AudioSource with the footsteps.
[RequireComponent(typeof(AudioSource))]
public class GeigerCounter : MonoBehaviour
{
    private const float Loudness = 0.5f; // output level at 100% volume

    [Tooltip("Set by the Radiation slider in the settings menu")]
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Average clicks per second for each mrem/hr. At 0.02, a 1,000 mrem/hr hotspot gives 20 clicks a second")]
    public float clicksPerMremPerHour = 0.02f;
    [Tooltip("Cap so the reactor turns into a dense roar instead of solid noise")]
    public float maxClicksPerSecond = 400f;

    [Header("Click sound")]
    [Tooltip("Pitch of the ring inside each click, in Hz")]
    public float ringFrequency = 3200f;
    [Tooltip("How long each click rings, in milliseconds")]
    public float clickLength = 1.5f;

    private Dosimeter dosimeter;

    // Written on the main thread, read on the audio thread
    private volatile float clicksPerSecond;
    private volatile float outputVolume;
    private float ringFrequencyAudio;
    private float clickLengthAudio;

    // Audio thread state
    private int sampleRate;
    private System.Random random;
    private float clickTime = float.MaxValue; // seconds since the current click started
    private float clickAmplitude;

    void Awake()
    {
        dosimeter = GetComponentInParent<Dosimeter>();
        volume = PlayerPrefs.GetFloat(SettingsMenuController.RadiationVolumeKey, volume);
        sampleRate = AudioSettings.outputSampleRate;
        random = new System.Random();

        // OnAudioFilterRead only runs while the source is playing, so give it a silent loop to play
        AudioSource source = GetComponent<AudioSource>();
        source.clip = AudioClip.Create("Silence", sampleRate, 1, sampleRate, false);
        source.loop = true;
        source.spatialBlend = 0f; // it's on the player, so no 3D positioning
        source.Play();
    }

    void Update()
    {
        // Audio keeps running behind the pause menu, so silence the counter there
        float rate = dosimeter == null || PauseMenuController.IsPaused ? 0f : dosimeter.CurrentRate;
        clicksPerSecond = Mathf.Min(rate * clicksPerMremPerHour, maxClicksPerSecond);
        outputVolume = volume * Loudness;
        ringFrequencyAudio = ringFrequency;
        clickLengthAudio = Mathf.Max(clickLength, 0.1f) / 1000f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float dt = 1f / sampleRate;
        double clickChance = clicksPerSecond * dt; // chance of a click starting on any one sample
        float gain = outputVolume;
        float ringPhase = 2f * Mathf.PI * ringFrequencyAudio;
        float decay = clickLengthAudio / 4f;      // time constant, so the click is near silent by clickLength

        for (int i = 0; i < data.Length; i += channels)
        {
            if (random.NextDouble() < clickChance)
            {
                clickTime = 0f;
                clickAmplitude = 0.6f + 0.4f * (float)random.NextDouble(); // real clicks vary a little
            }

            float sample = 0f;
            if (clickTime < clickLengthAudio)
            {
                // A sharp burst of noise plus a short metallic ring, both dying away fast
                float envelope = Mathf.Exp(-clickTime / decay);
                float noise = (float)random.NextDouble() * 2f - 1f;
                float ring = Mathf.Sin(ringPhase * clickTime);
                sample = (0.6f * noise + 0.4f * ring) * envelope * clickAmplitude * gain;
                clickTime += dt;
            }

            for (int c = 0; c < channels; c++)
                data[i + c] += sample;
        }
    }
}
