
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacilityBlackout : MonoBehaviour
{
    [Header("Stable Lighting")]
    public float minStableTime = 20f;
    public float maxStableTime = 30f;

    [Header("Power Failure")]
    public float instabilityDuration = 20f;
    public float minBlackoutDuration = 3f;
    public float maxBlackoutDuration = 6f;
    public float recoveryDuration = 15f;

    [Header("Flickering")]
    [Range(0f, 1f)]
    public float initialFlickerChance = 0.05f;

    [Range(0f, 1f)]
    public float maximumFlickerChance = 0.85f;

    public float minFlickerInterval = 0.08f;
    public float maxFlickerInterval = 0.25f;

    [Header("Recovery")]
    public float minRecoveryDelay = 0f;
    public float maxRecoveryDelay = 1f;

    private UnstableLight[] unstableLights;

    private readonly List<Light> otherLights =
        new List<Light>();

    private readonly List<float> originalIntensities =
        new List<float>();

    private readonly List<EmissionEntry> emissiveMaterials =
        new List<EmissionEntry>();

    private readonly List<LightState> otherLightStates =
        new List<LightState>();

    // MiniMap objects will not be affected.
    private int miniMapLayer;

    private enum PowerPhase
    {
        Stable,
        Unstable,
        Blackout,
        Recovery
    }

    private PowerPhase currentPhase = PowerPhase.Stable;

    private class EmissionEntry
    {
        public Renderer renderer;
        public int materialIndex;
        public Color originalEmission;
        public bool isOn = true;
        public float nextChange;
    }

    private class LightState
    {
        public bool isOn = true;
        public float nextChange;
    }

    void Start()
    {
        // Find the MiniMap layer.
        miniMapLayer = LayerMask.NameToLayer("MiniMap");

        FindFacilityLights();
        FindEmissiveMaterials();

        // Start with everything on.
        SetPhase(PowerPhase.Stable, 0f);

        StartCoroutine(PowerCycle());
    }

    // Check whether an object belongs to the MiniMap layer.
    bool IsMiniMapObject(GameObject obj)
    {
        return miniMapLayer >= 0 &&
               obj.layer == miniMapLayer;
    }

    void FindFacilityLights()
    {
        UnstableLight[] allUnstableLights =
            FindObjectsByType<UnstableLight>(
                FindObjectsSortMode.None
            );

        List<UnstableLight> validUnstableLights =
            new List<UnstableLight>();

        // Exclude UnstableLight components on the MiniMap layer.
        foreach (UnstableLight light in allUnstableLights)
        {
            if (light == null)
                continue;

            if (IsMiniMapObject(light.gameObject))
                continue;

            validUnstableLights.Add(light);
        }

        unstableLights = validUnstableLights.ToArray();

        Light[] allLights = FindObjectsByType<Light>(
            FindObjectsSortMode.None
        );

        foreach (Light light in allLights)
        {
            if (IsMiniMapObject(light.gameObject))
                continue;

            if (light.GetComponent<UnstableLight>() != null)
                continue;

            otherLights.Add(light);
            originalIntensities.Add(light.intensity);
            otherLightStates.Add(new LightState());
        }
    }

    void FindEmissiveMaterials()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(
            FindObjectsSortMode.None
        );

        HashSet<Renderer> controlledRenderers =
            new HashSet<Renderer>();

        // These renderers are already controlled by UnstableLight.
        foreach (UnstableLight light in unstableLights)
        {
            if (light != null && light.fixtureRenderer != null)
            {
                controlledRenderers.Add(light.fixtureRenderer);
            }
        }

        foreach (Renderer renderer in renderers)
        {
            // Never flicker or black out MiniMap objects.
            if (IsMiniMapObject(renderer.gameObject))
                continue;

            if (controlledRenderers.Contains(renderer))
                continue;

            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null ||
                    !material.HasProperty("_EmissionColor"))
                    continue;

                if (!material.IsKeywordEnabled("_EMISSION"))
                    continue;

                emissiveMaterials.Add(new EmissionEntry
                {
                    renderer = renderer,
                    materialIndex = i,
                    originalEmission =
                        material.GetColor("_EmissionColor")
                });
            }
        }
    }

    IEnumerator PowerCycle()
    {
        while (true)
        {
            // PHASE 1: All facility lights remain on.
            SetPhase(PowerPhase.Stable, 0f);

            float stableTime = Random.Range(
                minStableTime,
                maxStableTime
            );

            yield return new WaitForSeconds(
                Mathf.Max(0.1f, stableTime)
            );

            // PHASE 2: Random flickering grows more intense.
            float elapsed = 0f;

            while (elapsed < instabilityDuration)
            {
                float progress = Mathf.Clamp01(
                    elapsed / Mathf.Max(0.01f, instabilityDuration)
                );

                SetPhase(PowerPhase.Unstable, progress);

                yield return null;
                elapsed += Time.deltaTime;
            }

            // PHASE 3: Complete facility blackout.
            SetPhase(PowerPhase.Blackout, 1f);

            float blackoutTime = Random.Range(
                minBlackoutDuration,
                maxBlackoutDuration
            );

            yield return new WaitForSeconds(
                Mathf.Max(0.1f, blackoutTime)
            );

            // PHASE 4: Random power restoration.
            elapsed = 0f;

            while (elapsed < recoveryDuration)
            {
                float progress = Mathf.Clamp01(
                    elapsed / Mathf.Max(0.01f, recoveryDuration)
                );

                SetPhase(PowerPhase.Recovery, progress);

                yield return null;
                elapsed += Time.deltaTime;
            }

            // Return to stable lighting.
            SetPhase(PowerPhase.Stable, 0f);
        }
    }

    void SetPhase(PowerPhase phase, float progress)
    {
        currentPhase = phase;

        // Control individual ceiling lights.
        foreach (UnstableLight light in unstableLights)
        {
            if (light == null || !light.isActiveAndEnabled)
                continue;

            switch (phase)
            {
                case PowerPhase.Stable:
                    light.SetStable();
                    break;

                case PowerPhase.Unstable:
                    light.SetInstability(
                        progress,
                        initialFlickerChance,
                        maximumFlickerChance,
                        minFlickerInterval,
                        maxFlickerInterval
                    );
                    break;

                case PowerPhase.Blackout:
                    light.SetBlackout();
                    break;

                case PowerPhase.Recovery:
                    light.SetRecovery(
                        progress,
                        minFlickerInterval,
                        maxFlickerInterval
                    );
                    break;
            }
        }

        UpdateOtherLights(phase, progress);
        UpdateEmissiveMaterials(phase, progress);
    }

    void UpdateOtherLights(PowerPhase phase, float progress)
    {
        for (int i = 0; i < otherLights.Count; i++)
        {
            Light light = otherLights[i];

            if (light == null)
                continue;

            LightState state = otherLightStates[i];

            UpdateState(state, phase, progress);

            light.intensity = state.isOn
                ? originalIntensities[i]
                : 0f;
        }
    }

    void UpdateEmissiveMaterials(PowerPhase phase, float progress)
    {
        foreach (EmissionEntry entry in emissiveMaterials)
        {
            if (entry.renderer == null)
                continue;

            UpdateEmissionState(entry, phase, progress);

            MaterialPropertyBlock block =
                new MaterialPropertyBlock();

            entry.renderer.GetPropertyBlock(
                block,
                entry.materialIndex
            );

            block.SetColor(
                "_EmissionColor",
                entry.isOn
                    ? entry.originalEmission
                    : Color.black
            );

            entry.renderer.SetPropertyBlock(
                block,
                entry.materialIndex
            );
        }
    }

    void UpdateState(
        LightState state,
        PowerPhase phase,
        float progress)
    {
        if (phase == PowerPhase.Stable)
        {
            state.isOn = true;
            state.nextChange = 0f;
            return;
        }

        if (phase == PowerPhase.Blackout)
        {
            state.isOn = false;
            return;
        }

        if (Time.time < state.nextChange)
            return;

        if (phase == PowerPhase.Unstable)
        {
            float chance = Mathf.Lerp(
                initialFlickerChance,
                maximumFlickerChance,
                progress
            );

            state.isOn = Random.value >= chance;
        }
        else
        {
            state.isOn = Random.value < progress;
        }

        state.nextChange = Time.time + Random.Range(
            minFlickerInterval,
            maxFlickerInterval
        );
    }

    void UpdateEmissionState(
        EmissionEntry entry,
        PowerPhase phase,
        float progress)
    {
        if (phase == PowerPhase.Stable)
        {
            entry.isOn = true;
            entry.nextChange = 0f;
            return;
        }

        if (phase == PowerPhase.Blackout)
        {
            entry.isOn = false;
            return;
        }

        if (Time.time < entry.nextChange)
            return;

        if (phase == PowerPhase.Unstable)
        {
            float chance = Mathf.Lerp(
                initialFlickerChance,
                maximumFlickerChance,
                progress
            );

            entry.isOn = Random.value >= chance;
        }
        else
        {
            entry.isOn = Random.value < progress;
        }

        entry.nextChange = Time.time + Random.Range(
            minFlickerInterval,
            maxFlickerInterval
        );
    }

    void OnDisable()
    {
        StopAllCoroutines();

        if (unstableLights != null)
        {
            SetPhase(PowerPhase.Stable, 0f);
        }
    }
}
