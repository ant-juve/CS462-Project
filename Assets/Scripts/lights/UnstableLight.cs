
using UnityEngine;

[RequireComponent(typeof(Light))]
public class UnstableLight : MonoBehaviour
{
    [Header("Light Fixture")]
    public Renderer fixtureRenderer;
    public int emissionMaterialIndex = 0;

    private Light lightSource;
    private float originalIntensity;

    private Material fixtureMaterial;
    private Color originalEmission;

    private bool isOn = true;
    private float nextFlickerTime = 0f;

    private enum LightPhase
    {
        Stable,
        Unstable,
        Blackout,
        Recovery
    }

    private LightPhase currentPhase = LightPhase.Stable;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        originalIntensity = lightSource.intensity;

        if (fixtureRenderer == null && transform.parent != null)
        {
            Renderer[] renderers =
                transform.parent.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer.transform == transform)
                    continue;

                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];

                    if (material != null &&
                        material.name.StartsWith("Neon_01"))
                    {
                        fixtureRenderer = renderer;
                        emissionMaterialIndex = i;
                        break;
                    }
                }

                if (fixtureRenderer != null)
                    break;
            }
        }

        if (fixtureRenderer != null)
        {
            Material[] materials = fixtureRenderer.materials;

            if (emissionMaterialIndex >= 0 &&
                emissionMaterialIndex < materials.Length)
            {
                fixtureMaterial =
                    materials[emissionMaterialIndex];

                if (fixtureMaterial != null &&
                    fixtureMaterial.HasProperty("_EmissionColor"))
                {
                    originalEmission =
                        fixtureMaterial.GetColor("_EmissionColor");
                }
                else
                {
                    fixtureMaterial = null;
                }
            }
        }
    }

    void OnEnable()
    {
        SetStable();
    }

    public void SetStable()
    {
        if (currentPhase != LightPhase.Stable)
        {
            currentPhase = LightPhase.Stable;
            nextFlickerTime = 0f;
        }

        SetBrightness(1f);
    }

    public void SetInstability(
        float progress,
        float minChance,
        float maxChance,
        float minInterval,
        float maxInterval)
    {
        if (currentPhase != LightPhase.Unstable)
        {
            currentPhase = LightPhase.Unstable;
            nextFlickerTime = 0f;
        }

        if (Time.time < nextFlickerTime)
            return;

        float chance = Mathf.Lerp(
            minChance,
            maxChance,
            progress
        );

        // Each light independently decides whether to blink.
        isOn = Random.value >= chance;

        SetBrightness(isOn ? 1f : 0f);

        nextFlickerTime = Time.time + Random.Range(
            minInterval,
            maxInterval
        );
    }

    public void SetBlackout()
    {
        currentPhase = LightPhase.Blackout;
        SetBrightness(0f);
    }

    public void SetRecovery(
        float progress,
        float minInterval,
        float maxInterval)
    {
        if (currentPhase != LightPhase.Recovery)
        {
            currentPhase = LightPhase.Recovery;
            nextFlickerTime = 0f;
        }

        if (Time.time < nextFlickerTime)
            return;

        // Lights gradually return at different times.
        isOn = Random.value < progress;

        SetBrightness(isOn ? 1f : 0f);

        nextFlickerTime = Time.time + Random.Range(
            minInterval,
            maxInterval
        );
    }

    void SetBrightness(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (lightSource != null)
        {
            lightSource.intensity =
                originalIntensity * ratio;
        }

        if (fixtureMaterial != null)
        {
            fixtureMaterial.SetColor(
                "_EmissionColor",
                originalEmission * ratio
            );
        }
    }

    void OnDisable()
    {
        SetBrightness(1f);
    }

    void OnDestroy()
    {
        if (fixtureMaterial != null)
            Destroy(fixtureMaterial);
    }
}
