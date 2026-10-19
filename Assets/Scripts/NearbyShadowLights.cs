using System.Collections.Generic;
using UnityEngine;

// Gives the player a real shadow. The level is lit by hundreds of small lights with shadows off
// (shadows from all of them would be far too expensive), so this turns shadows on for just the few
// lights that light the player most, fading them in and out as you walk so nothing pops.
// Lights that already cast shadows are left alone, and every light is put back the way it was when
// this is disabled. Nothing in the scene file is changed.
//
// Goes on the PlayerCapsule.
public class NearbyShadowLights : MonoBehaviour
{
    [Tooltip("How many nearby lights cast shadows at once. Each point light renders the area around it 6 times, so keep this small")]
    [Range(1, 4)] public int maxShadowLights = 2;
    [Tooltip("How dark the shadows get (0-1)")]
    [Range(0f, 1f)] public float shadowStrength = 1f;
    [Tooltip("How quickly a light's shadow fades in or out, per second")]
    public float fadeSpeed = 2.5f;
    [Tooltip("How often to look for the nearest lights, in seconds")]
    public float checkInterval = 0.2f;
    [Tooltip("Where on the body to measure light from (meters above the feet)")]
    public float chestHeight = 1.2f;

    private struct Candidate
    {
        public Light light;
        public LightShadows originalShadows;
        public float originalStrength;
    }

    private readonly List<Candidate> candidates = new List<Candidate>();
    private readonly Dictionary<Light, int> indexOf = new Dictionary<Light, int>();
    private readonly HashSet<int> wanted = new HashSet<int>();
    private readonly List<int> fading = new List<int>(); // lights currently with a shadow we control
    private float[] strength;
    private float[] influence;
    private int[] best;
    private float nextCheck;

    void OnEnable()
    {
        candidates.Clear();
        indexOf.Clear();
        fading.Clear();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            // Only realtime point and spot lights that don't already cast shadows
            if (light.type != LightType.Point && light.type != LightType.Spot)
                continue;
            if (light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked ||
                light.shadows != LightShadows.None)
                continue;
            indexOf[light] = candidates.Count;
            candidates.Add(new Candidate { light = light, originalShadows = light.shadows, originalStrength = light.shadowStrength });
        }
        strength = new float[candidates.Count];
        influence = new float[candidates.Count];
        best = new int[Mathf.Max(1, maxShadowLights)];
        nextCheck = 0f;
    }

    void OnDisable()
    {
        // Put every light back the way it was
        foreach (int i in fading)
            Restore(i);
        fading.Clear();
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            PickNearest();
        }

        float step = fadeSpeed * Time.deltaTime;
        for (int f = fading.Count - 1; f >= 0; f--)
        {
            int i = fading[f];
            Light light = candidates[i].light;
            if (light == null)
            {
                fading.RemoveAt(f);
                continue;
            }

            float target = wanted.Contains(i) && light.isActiveAndEnabled ? shadowStrength : 0f;
            strength[i] = Mathf.MoveTowards(strength[i], target, step);
            if (strength[i] <= 0f && target <= 0f)
            {
                Restore(i);
                fading.RemoveAt(f);
                continue;
            }
            light.shadows = LightShadows.Soft;
            light.shadowStrength = strength[i];
        }
    }

    // Chooses the lights that light the player most: in range, brightest for their distance
    void PickNearest()
    {
        Vector3 chest = transform.position + Vector3.up * chestHeight;
        int count = Mathf.Min(maxShadowLights, best.Length);
        for (int b = 0; b < count; b++)
            best[b] = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            Light light = candidates[i].light;
            influence[i] = 0f;
            if (light == null || !light.isActiveAndEnabled)
                continue;

            float distanceSq = (light.transform.position - chest).sqrMagnitude;
            float range = light.range;
            if (distanceSq >= range * range)
                continue; // doesn't reach the player
            if (light.type == LightType.Spot && Vector3.Angle(light.transform.forward, chest - light.transform.position) > light.spotAngle * 0.5f)
                continue;

            // Brightness here, fading to nothing at the light's range
            float falloff = 1f - distanceSq / (range * range);
            influence[i] = light.intensity * falloff / Mathf.Max(distanceSq, 0.25f);
            Insert(i, count);
        }

        wanted.Clear();
        for (int b = 0; b < count; b++)
        {
            int i = best[b];
            if (i < 0)
                continue;
            wanted.Add(i);
            if (!fading.Contains(i))
            {
                fading.Add(i);
                strength[i] = 0f;
            }
        }
    }

    // Keeps best[] sorted by influence, highest first
    void Insert(int i, int count)
    {
        for (int b = 0; b < count; b++)
        {
            if (best[b] < 0 || influence[i] > influence[best[b]])
            {
                for (int k = count - 1; k > b; k--)
                    best[k] = best[k - 1];
                best[b] = i;
                return;
            }
        }
    }

    void Restore(int i)
    {
        Candidate c = candidates[i];
        if (c.light == null)
            return;
        c.light.shadows = c.originalShadows;
        c.light.shadowStrength = c.originalStrength;
        strength[i] = 0f;
    }
}
