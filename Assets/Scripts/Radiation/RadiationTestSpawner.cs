using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// TEMPORARY: puts a radioactive puddle on the floor a few meters in front of the player whenever
// a scene with a player loads, so the EPD can be tested without placing spills by hand.
// Walk into it and watch the dose climb.
// Set Enabled to false (or delete this file) once real spills are in the map.
public static class RadiationTestSpawner
{
    public static bool Enabled = true;

    private const float DistanceAhead = 7f;   // starts you at the faint far end of its reach
    private const float PuddleRadius = 1.2f;
    private const float DoseRate = 5000f;     // mrem/hr anywhere on the puddle
    private const float EdgeFade = 6f;        // meters past the edge before it drops to nothing

    // Guards against spawning twice in the same scene
    private static readonly HashSet<int> spawnedScenes = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!Enabled)
            return;

        spawnedScenes.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SpawnNearPlayer(); // the scene that was open when Play was pressed
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnNearPlayer();
    }

    static void SpawnNearPlayer()
    {
        FirstPersonPlayer player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        if (player == null || !spawnedScenes.Add(player.gameObject.scene.handle))
            return;

        // Find the floor below the spot ahead; fall back to the player's feet height
        Transform origin = player.transform;
        Vector3 ahead = origin.position + origin.forward * DistanceAhead;
        Vector3 position = ahead;
        if (Physics.Raycast(ahead + Vector3.up, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;

        // Built inactive so the puddle's Awake runs after its settings are filled in
        GameObject spill = new GameObject("Test Radiation Spill");
        spill.SetActive(false);
        spill.transform.position = position;

        RadiationSource source = spill.AddComponent<RadiationSource>();
        source.doseRate = DoseRate;
        source.edgeFade = EdgeFade;

        RadiationPuddle puddle = spill.AddComponent<RadiationPuddle>();
        puddle.radius = PuddleRadius;

        spill.SetActive(true);
    }
}
