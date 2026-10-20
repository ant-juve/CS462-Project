using UnityEngine;

// A radioactive spill on the floor: dark, murky sludge with drifting scum, slow heavy ripples and a
// faint green glow underneath, with an irregular edge and a few stray droplets, plus an Area radiation
// zone sized to match. Put it on an empty object sitting on the floor; the shape is generated when the
// game starts, so it doesn't show in edit mode (the orange zone gizmo does). The look comes from the
// "Radiation/Sludge" shader; its material has more settings than the ones here.
[RequireComponent(typeof(RadiationSource))]
public class RadiationPuddle : MonoBehaviour
{
    private const string ShaderName = "Radiation/Sludge";
    private const int EdgePoints = 48;
    private const float FloorOffset = 0.01f; // just above the floor so it doesn't flicker into it

    [Tooltip("Average radius of the main puddle, in meters")]
    public float radius = 1.2f;
    [Tooltip("How wobbly the edge is. 0 is a perfect circle")]
    [Range(0f, 0.5f)] public float irregularity = 0.25f;
    [Tooltip("Width of the soft fade at the edge, as a fraction of the radius")]
    [Range(0f, 0.5f)] public float edgeSoftness = 0.06f; // sludge has a thicker, more defined edge than water
    [Tooltip("Small splashes scattered around the edge")]
    public int droplets = 4;
    [Tooltip("Same seed, same shape. 0 picks a new shape every time")]
    public int seed = 0;

    [Header("Look")]
    [Tooltip("Color of the open water")]
    public Color color = new Color(0.018f, 0.026f, 0.016f);
    [Tooltip("How glossy and wet the open water looks")]
    [Range(0f, 1f)] public float smoothness = 0.88f;
    [Tooltip("How much of the surface is covered in dull, floating scum. 0 is clean dark water")]
    [Range(0f, 1f)] public float scum = 0.6f;
    [Tooltip("Hint of oily rainbow film on the open water")]
    [Range(0f, 1f)] public float oilyFilm = 0.15f;
    [Tooltip("How much the surface ripples")]
    [Range(0f, 1f)] public float ripples = 0.08f;
    [Tooltip("Faint green glow under the surface. Set to black to turn it off")]
    public Color glow = new Color(0.03f, 0.12f, 0.02f);

    void Awake()
    {
        System.Random random = new System.Random(seed != 0 ? seed : Random.Range(1, int.MaxValue));
        Material material = CreateMaterial();

        CreateBlob("Puddle", Vector3.zero, radius, random, material);
        for (int i = 0; i < droplets; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = radius * Lerp(1.05f, 1.4f, random);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            CreateBlob("Droplet", offset, radius * Lerp(0.06f, 0.16f, random), random, material);
        }

        // Fit the radiation zone to the puddle. Tall enough to cover the player's chest, where dose is measured
        RadiationSource source = GetComponent<RadiationSource>();
        source.shape = RadiationSource.Shape.Area;
        source.areaSize = new Vector3(radius * 2f, 3f, radius * 2f);
    }

    Material CreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            // Plain glossy dark material, so a missing shader doesn't turn the puddle pink
            Debug.LogWarning($"RadiationPuddle: shader \"{ShaderName}\" not found, using URP Lit");
            Material fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallback.SetColor("_BaseColor", color);
            fallback.SetFloat("_Smoothness", smoothness);
            return fallback;
        }

        Material material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_ScumAmount", scum);
        material.SetFloat("_SheenStrength", oilyFilm);
        material.SetFloat("_RippleStrength", ripples);
        material.SetColor("_GlowColor", glow);
        return material;
    }

    void CreateBlob(string name, Vector3 center, float size, System.Random random, Material material)
    {
        GameObject blob = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        blob.transform.SetParent(transform, false);
        blob.transform.localPosition = center + Vector3.up * FloorOffset;
        blob.GetComponent<MeshFilter>().mesh = BlobMesh(size, random);

        MeshRenderer renderer = blob.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // A flat disc whose edge wobbles (a few sine waves of different frequencies added together),
    // with an outer ring that fades to transparent so the edge blends into the floor
    Mesh BlobMesh(float size, System.Random random)
    {
        float phase2 = (float)random.NextDouble() * Mathf.PI * 2f;
        float phase3 = (float)random.NextDouble() * Mathf.PI * 2f;
        float phase5 = (float)random.NextDouble() * Mathf.PI * 2f;
        float feather = size * edgeSoftness;

        // Vertex 0 is the center, then the solid edge ring, then the faded outer ring
        Vector3[] vertices = new Vector3[1 + EdgePoints * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[EdgePoints * 9];
        Color solid = Color.white;
        Color clear = new Color(1f, 1f, 1f, 0f);
        vertices[0] = Vector3.zero;
        colors[0] = solid;

        for (int i = 0; i < EdgePoints; i++)
        {
            float angle = i * Mathf.PI * 2f / EdgePoints;
            float wobble = 0.5f * Mathf.Sin(2f * angle + phase2)
                         + 0.35f * Mathf.Sin(3f * angle + phase3)
                         + 0.25f * Mathf.Sin(5f * angle + phase5);
            float r = size * Mathf.Max(0.4f, 1f + irregularity * wobble);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            int inner = 1 + i;
            int outer = 1 + EdgePoints + i;
            vertices[inner] = direction * r;
            vertices[outer] = direction * (r + feather);
            colors[inner] = solid;
            colors[outer] = clear;

            // Wound clockwise seen from above so it faces up
            int nextInner = 1 + (i + 1) % EdgePoints;
            int nextOuter = 1 + EdgePoints + (i + 1) % EdgePoints;
            int t = i * 9;
            triangles[t] = 0;                                           // center fan
            triangles[t + 1] = nextInner;
            triangles[t + 2] = inner;
            triangles[t + 3] = inner;                                   // fade ring, two triangles per segment
            triangles[t + 4] = nextInner;
            triangles[t + 5] = outer;
            triangles[t + 6] = nextInner;
            triangles[t + 7] = nextOuter;
            triangles[t + 8] = outer;
        }

        Mesh mesh = new Mesh { name = "Puddle" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float Lerp(float a, float b, System.Random random) => a + (b - a) * (float)random.NextDouble();
}
