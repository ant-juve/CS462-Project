using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// TEMPORARY: spawns a few colored cubes in front of the player whenever a scene with a
// player loads, so the hotbar pickup logic can be tested without placing items by hand.
// Set Enabled to false (or delete this file) once real items are in the map.
public static class PickupTestSpawner
{
    public static bool Enabled = true;

    // One more than the hotbar holds, so "Hotbar full" can be tested too
    private static readonly (string name, Color color)[] TestItems =
    {
        ("Red Cube", Color.red),
        ("Green Cube", Color.green),
        ("Blue Cube", Color.blue),
        ("Yellow Cube", Color.yellow),
        ("White Cube", Color.white),
        ("Purple Cube", new Color(0.6f, 0.2f, 0.9f)),
    };

    private const float CubeSize = 0.12f; // small enough to hold in one hand
    private const float DistanceAhead = 0.9f;
    private const float Spacing = 0.35f;

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

        Transform origin = player.transform;
        // Player's pivot is at its feet, so start a little above the floor and let the cubes drop
        Vector3 center = origin.position + Vector3.up + origin.forward * DistanceAhead;
        float rowWidth = (TestItems.Length - 1) * Spacing;

        for (int i = 0; i < TestItems.Length; i++)
        {
            Vector3 position = center + origin.right * (i * Spacing - rowWidth / 2f);
            SpawnCube(TestItems[i].name, TestItems[i].color, position);
        }
    }

    static void SpawnCube(string itemName, Color color, Vector3 position)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = itemName;
        cube.transform.position = position;
        cube.transform.localScale = Vector3.one * CubeSize;
        cube.GetComponent<Renderer>().material.color = color;

        // Falls to the floor, and gets the same treatment when dropped from the hotbar
        cube.AddComponent<Rigidbody>();

        PickupItem item = cube.AddComponent<PickupItem>();
        item.itemName = itemName;
    }
}
