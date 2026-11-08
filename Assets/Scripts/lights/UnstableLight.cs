
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class UnstableLight : MonoBehaviour
{
    [Header("Normal Flickering")]
    public float minWaitTime = 5f;
    public float maxWaitTime = 15f;

    public int minFlickers = 2;
    public int maxFlickers = 6;

    public float minFlickerTime = 0.04f;
    public float maxFlickerTime = 0.15f;

    [Header("Individual Blackouts")]
    public bool enableBlackouts = true;

    [Range(0f, 1f)]
    public float blackoutChance = 0.3f;

    public float minOffTime = 3f;
    public float maxOffTime = 6f;

    [Header("Blackout Effects")]
    public bool flickerBeforeBlackout = true;
    public bool flickerAfterBlackout = true;

    [Header("Light Fixture")]
    public Renderer fixtureRenderer;
    public int emissionMaterialIndex = 0;

    private Light lightSource;
    private float originalIntensity;

    private Material fixtureMaterial;
    private Color originalEmission;

    private Coroutine behaviorCoroutine;

    // Facility-wide blackout override.
    private bool facilityBlackout = false;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        originalIntensity = lightSource.intensity;

        // Automatically find the glowing fixture.
        if (fixtureRenderer == null && transform.parent != null)
        {
            Renderer[] renderers =
                transform.parent.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer.transform == transform)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material material = sharedMaterials[i];

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

        // Create an individual material instance.
        if (fixtureRenderer != null)
        {
            Material[] materials = fixtureRenderer.materials;

            if (emissionMaterialIndex >= 0 &&
                emissionMaterialIndex < materials.Length)
            {
                fixtureMaterial = materials[emissionMaterialIndex];

                if (fixtureMaterial != null &&
                    fixtureMaterial.HasProperty("_EmissionColor"))
                {
                    originalEmission =
                        fixtureMaterial.GetColor("_EmissionColor");
                }
                else
                {
                    fixtureMaterial = null;
                    Debug.LogWarning(
                        "Fixture material has no emission color.",
                        this
                    );
                }
            }
        }
    }

    void OnEnable()
    {
        behaviorCoroutine = StartCoroutine(LightBehavior());
    }

    void OnDisable()
    {
        if (behaviorCoroutine != null)
        {
            StopCoroutine(behaviorCoroutine);
            behaviorCoroutine = null;
        }

        facilityBlackout = false;
        RestoreLight();
    }

    void OnDestroy()
    {
        if (fixtureMaterial != null)
        {
            Destroy(fixtureMaterial);
        }
    }

    // Called by FacilityBlackout.cs.
    public void SetFacilityBlackout(bool isBlackout)
    {
        facilityBlackout = isBlackout;

        if (isBlackout)
        {
            SetBrightness(0f);
        }
        else
        {
            RestoreLight();
        }
    }

    void SetBrightness(float ratio)
    {
        // Global blackouts override individual flickering.
        if (facilityBlackout)
        {
            ratio = 0f;
        }

        ratio = Mathf.Clamp01(ratio);

        // Control the actual light.
        if (lightSource != null)
        {
            lightSource.intensity =
                originalIntensity * ratio;
        }

        // Control the fixture's emission.
        if (fixtureMaterial != null)
        {
            fixtureMaterial.SetColor(
                "_EmissionColor",
                originalEmission * ratio
            );
        }
    }

    void RestoreLight()
    {
        SetBrightness(1f);
    }

    IEnumerator LightBehavior()
    {
        while (true)
        {
            float waitTime = Random.Range(
                minWaitTime,
                maxWaitTime
            );

            yield return new WaitForSeconds(waitTime);

            // Pause individual behavior during global blackouts.
            while (facilityBlackout)
            {
                yield return null;
            }

            if (enableBlackouts &&
                Random.value < blackoutChance)
            {
                yield return StartCoroutine(Blackout());
            }
            else
            {
                yield return StartCoroutine(Flicker());
            }
        }
    }

    IEnumerator Flicker()
    {
        int flickers = Random.Range(
            minFlickers,
            maxFlickers + 1
        );

        for (int i = 0; i < flickers; i++)
        {
            SetBrightness(0f);

            yield return new WaitForSeconds(
                Random.Range(
                    minFlickerTime,
                    maxFlickerTime
                )
            );

            SetBrightness(1f);

            yield return new WaitForSeconds(
                Random.Range(
                    minFlickerTime,
                    maxFlickerTime
                )
            );
        }

        RestoreLight();
    }

    IEnumerator Blackout()
    {
        // Flicker before losing power.
        if (flickerBeforeBlackout)
        {
            yield return StartCoroutine(Flicker());
        }

        // Individual power failure.
        SetBrightness(0f);

        yield return new WaitForSeconds(
            Random.Range(
                minOffTime,
                maxOffTime
            )
        );

        // Flicker as power returns.
        if (flickerAfterBlackout)
        {
            yield return StartCoroutine(Flicker());
        }

        RestoreLight();
    }
}
