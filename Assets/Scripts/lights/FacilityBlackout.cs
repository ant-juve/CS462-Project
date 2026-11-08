
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacilityBlackout : MonoBehaviour
{
    [Header("Facility Blackouts")]
    public float minWaitTime = 30f;
    public float maxWaitTime = 60f;
    public float minBlackoutDuration = 3f;
    public float maxBlackoutDuration = 6f;

    [Header("Facility Blackout Effects")]
    public bool flickerBeforeBlackout = true;
    public bool flickerAfterBlackout = true;
    public int facilityFlickers = 3;
    public float facilityFlickerSpeed = 0.12f;

    [Header("Random Emission Flickering")]
    public bool enableRandomFlickering = true;
    public float minEmissionWait = 5f;
    public float maxEmissionWait = 15f;
    public int minEmissionFlickers = 2;
    public int maxEmissionFlickers = 5;
    public float minFlickerTime = 0.04f;
    public float maxFlickerTime = 0.15f;

    private UnstableLight[] unstableLights;

    private readonly List<Light> otherLights =
        new List<Light>();

    private readonly List<float> originalIntensities =
        new List<float>();

    private readonly List<EmissionEntry> emissiveMaterials =
        new List<EmissionEntry>();

    private bool facilityOverride;

    private class EmissionEntry
    {
        public Renderer renderer;
        public int materialIndex;
        public Color originalEmission;
        public float localBrightness = 1f;
    }

    void Start()
    {
        FindFacilityLights();
        FindEmissiveMaterials();

        StartCoroutine(BlackoutRoutine());

        if (enableRandomFlickering)
        {
            foreach (EmissionEntry entry in emissiveMaterials)
            {
                StartCoroutine(RandomEmissionFlicker(entry));
            }
        }
    }

    void FindFacilityLights()
    {
        unstableLights = FindObjectsByType<UnstableLight>(
            FindObjectsSortMode.None
        );

        Light[] allLights = FindObjectsByType<Light>(
            FindObjectsSortMode.None
        );

        foreach (Light light in allLights)
        {
            if (light.GetComponent<UnstableLight>() != null)
                continue;

            otherLights.Add(light);
            originalIntensities.Add(light.intensity);
        }
    }

    void FindEmissiveMaterials()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(
            FindObjectsSortMode.None
        );

        foreach (Renderer renderer in renderers)
        {
            // Ceiling fixtures are already controlled by
            // UnstableLight, so skip those renderers.
            bool controlledByUnstableLight = false;

            foreach (UnstableLight light in unstableLights)
            {
                if (light != null &&
                    light.fixtureRenderer == renderer)
                {
                    controlledByUnstableLight = true;
                    break;
                }
            }

            if (controlledByUnstableLight)
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

                EmissionEntry entry = new EmissionEntry
                {
                    renderer = renderer,
                    materialIndex = i,
                    originalEmission =
                        material.GetColor("_EmissionColor")
                };

                emissiveMaterials.Add(entry);
            }
        }
    }

    IEnumerator RandomEmissionFlicker(EmissionEntry entry)
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(minEmissionWait, maxEmissionWait)
            );

            int count = Random.Range(
                minEmissionFlickers,
                maxEmissionFlickers + 1
            );

            for (int i = 0; i < count; i++)
            {
                entry.localBrightness = 0f;
                ApplyEmission(entry);

                yield return new WaitForSeconds(
                    Random.Range(minFlickerTime, maxFlickerTime)
                );

                entry.localBrightness = 1f;
                ApplyEmission(entry);

                yield return new WaitForSeconds(
                    Random.Range(minFlickerTime, maxFlickerTime)
                );
            }

            entry.localBrightness = 1f;
            ApplyEmission(entry);
        }
    }

    IEnumerator BlackoutRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(minWaitTime, maxWaitTime)
            );

            if (flickerBeforeBlackout)
                yield return StartCoroutine(FacilityFlicker());

            SetFacilityPower(false);

            yield return new WaitForSeconds(
                Random.Range(
                    minBlackoutDuration,
                    maxBlackoutDuration
                )
            );

            if (flickerAfterBlackout)
                yield return StartCoroutine(FacilityFlicker());

            SetFacilityPower(true);
        }
    }

    IEnumerator FacilityFlicker()
    {
        for (int i = 0; i < facilityFlickers; i++)
        {
            SetFacilityPower(false);
            yield return new WaitForSeconds(facilityFlickerSpeed);

            SetFacilityPower(true);
            yield return new WaitForSeconds(facilityFlickerSpeed);
        }
    }

    void SetFacilityPower(bool powered)
    {
        facilityOverride = !powered;

        foreach (UnstableLight light in unstableLights)
        {
            if (light != null && light.isActiveAndEnabled)
                light.SetFacilityBlackout(!powered);
        }

        for (int i = 0; i < otherLights.Count; i++)
        {
            if (otherLights[i] != null)
            {
                otherLights[i].intensity = powered
                    ? originalIntensities[i]
                    : 0f;
            }
        }

        foreach (EmissionEntry entry in emissiveMaterials)
        {
            ApplyEmission(entry);
        }
    }

    void ApplyEmission(EmissionEntry entry)
    {
        if (entry.renderer == null)
            return;

        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        entry.renderer.GetPropertyBlock(
            block,
            entry.materialIndex
        );

        float brightness = facilityOverride
            ? 0f
            : entry.localBrightness;

        block.SetColor(
            "_EmissionColor",
            entry.originalEmission * brightness
        );

        entry.renderer.SetPropertyBlock(
            block,
            entry.materialIndex
        );
    }

    void OnDisable()
    {
        StopAllCoroutines();
        SetFacilityPower(true);
    }
}
