using UnityEngine;

// An anti-radiation autoinjector. Put it next to a PickupItem: pick it up, select its slot and
// click to inject. While the needle is in the arm, the player's total dose drops by a set amount,
// so the EPD visibly counts down. It doesn't give back health, but getting the dose under the
// Dosimeter's warning level stops the sickness and coughing. Used up after one injection.
//
// There's no model for it yet: CreatePlaceholder builds a simple one out of primitives.
public class RadiationShot : MonoBehaviour, IUsable
{
    [Tooltip("Total dose taken off, in mrem")]
    public float doseRemoved = 500f;
    [Tooltip("Seconds the dose takes to come off. Keep it within the arms' inject hold time so it's done " +
             "before the needle comes out")]
    public float duration = 0.5f;
    [Tooltip("Seconds after the needle goes in before the dose starts dropping")]
    public float delay = 0f;
    [Tooltip("Played when the needle goes in. Leave empty for a synthesized click and hiss")]
    public AudioClip injectSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    private static AudioClip synthesizedSound;

    public string UseVerb => "Inject";
    public bool ConsumeOnUse => true;

    public bool CanUse(GameObject player) => player.GetComponent<Dosimeter>() != null;

    public void Use(GameObject player)
    {
        player.GetComponent<Dosimeter>().RemoveDose(doseRemoved, duration, delay);

        AudioClip clip = injectSound != null ? injectSound : SynthesizedSound();
        AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : player.transform.position, volume);
    }

    // A sharp spring click as the injector fires, then a short hiss as it empties
    static AudioClip SynthesizedSound()
    {
        if (synthesizedSound != null)
            return synthesizedSound;

        const float Length = 0.6f;
        int sampleRate = AudioSettings.outputSampleRate;
        float[] samples = new float[Mathf.CeilToInt(Length * sampleRate)];
        System.Random random = new System.Random(462);
        float lowPassed = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / sampleRate;
            float noise = (float)random.NextDouble() * 2f - 1f;

            // Click: a few milliseconds of noise and a metallic ring
            float click = t < 0.012f ? (0.7f * noise + 0.5f * Mathf.Sin(2f * Mathf.PI * 2400f * t)) * Mathf.Exp(-t / 0.003f) : 0f;

            // Hiss: noise with the lows taken out, swelling in then fading away
            lowPassed += (noise - lowPassed) * 0.15f;
            float hissNoise = noise - lowPassed;
            float hissEnvelope = Mathf.Clamp01((t - 0.02f) / 0.05f) * Mathf.Exp(-(t - 0.02f) / 0.18f);
            float hiss = t > 0.02f ? hissNoise * hissEnvelope * 0.35f : 0f;

            samples[i] = click + hiss;
        }

        synthesizedSound = AudioClip.Create("Inject", samples.Length, 1, sampleRate, false);
        synthesizedSound.SetData(samples, 0);
        return synthesizedSound;
    }

    // A stand-in autoinjector: green body, yellow safety cap, steel tip. Lies on its side, ready
    // to be picked up. Swap in a real model later by building a prefab with a Collider,
    // PickupItem and RadiationShot on it
    public static RadiationShot CreatePlaceholder(Vector3 position)
    {
        GameObject root = new GameObject("Anti-Rad Shot");
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f); // capsules stand up; tip it over

        Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.065f, 0.035f), new Color(0.25f, 0.75f, 0.2f));
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.075f, 0f), new Vector3(0.03f, 0.012f, 0.03f), new Color(0.95f, 0.8f, 0.1f));
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.07f, 0f), new Vector3(0.015f, 0.008f, 0.015f), new Color(0.7f, 0.72f, 0.75f));

        // One collider for the whole thing, added before PickupItem so its Awake finds it
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.radius = 0.02f;
        collider.height = 0.17f;
        collider.direction = 1;

        root.AddComponent<Rigidbody>().mass = 0.1f;

        PickupItem item = root.AddComponent<PickupItem>();
        item.itemName = "Anti-Rad Shot";
        item.fixedGrip = true;
        item.gripAxis = Vector3.down;      // the steel tip is at the bottom
        item.gripRadius = collider.radius;
        return root.AddComponent<RadiationShot>();
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = type.ToString();
        DestroyImmediate(part.GetComponent<Collider>()); // the root's collider covers it
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().material.color = color;
    }
}
