using UnityEngine;

// Footstep sounds, played the moment each foot lands (FirstPersonBody decides when that is, from
// the legs' animation), plus a heavier sound when landing from a jump or fall.
// Goes on the PlayerCapsule alongside FirstPersonBody.
[RequireComponent(typeof(FirstPersonBody))]
public class FirstPersonFootsteps : MonoBehaviour
{
    public AudioClip[] steps;
    public AudioClip[] landings;

    [Tooltip("Step volume at walking speed and at sprinting speed")]
    [Range(0f, 1f)] public float walkVolume = 0.3f;
    [Range(0f, 1f)] public float sprintVolume = 0.55f;
    [Tooltip("Random change in pitch and volume from step to step, so no two sound identical")]
    public float pitchVariation = 0.07f;
    public float volumeVariation = 0.15f;
    [Tooltip("Falls slower than this (m/s) land softly, like a step")]
    public float softLandingSpeed = 4f;

    private AudioSource source;
    private FirstPersonBody body;
    private float walkSpeed = 2.5f, sprintSpeed = 6f;
    private int lastStep = -1, lastLanding = -1;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // your own feet: the same in both ears
    }

    void OnEnable()
    {
        body = GetComponent<FirstPersonBody>();
        body.Stepped += OnStep;
        body.Landed += OnLand;

        var controller = GetComponent<StarterAssets.FirstPersonController>();
        if (controller != null)
        {
            walkSpeed = controller.MoveSpeed;
            sprintSpeed = controller.SprintSpeed;
        }
    }

    void OnDisable()
    {
        body.Stepped -= OnStep;
        body.Landed -= OnLand;
    }

    void OnStep(float speed)
    {
        float t = Mathf.InverseLerp(walkSpeed, sprintSpeed, speed);
        float volume = Mathf.Lerp(walkVolume, sprintVolume, t) * Mathf.Clamp01(speed / walkSpeed);
        Play(steps, ref lastStep, volume, 1f + t * 0.05f);
    }

    void OnLand(float fallSpeed)
    {
        if (fallSpeed < softLandingSpeed)
        {
            Play(steps, ref lastStep, walkVolume * 1.2f, 1f);
            return;
        }
        float volume = Mathf.Lerp(sprintVolume, 1f, Mathf.InverseLerp(softLandingSpeed, 12f, fallSpeed));
        Play(landings != null && landings.Length > 0 ? landings : steps, ref lastLanding, volume, 1f);
    }

    // Picks a random clip, never the same one twice in a row
    void Play(AudioClip[] clips, ref int last, float volume, float pitch)
    {
        if (clips == null || clips.Length == 0)
            return;

        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == last)
            index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        last = index;

        source.pitch = pitch * (1f + Random.Range(-pitchVariation, pitchVariation));
        source.PlayOneShot(clips[index], volume * (1f + Random.Range(-volumeVariation, volumeVariation)));
    }
}
