
using UnityEngine;

public class MinimapManager : MonoBehaviour
{
    public Camera minimapCamera;

    // Camera positions for each area
    public Transform[] mapLocations;

    // Camera zoom for each area
    public float[] mapSizes;

    // Walls belonging to each map
    public GameObject[] mapWalls;

    // Keep track of the currently active map
    public int currentMapIndex = 0;

    public void ChangeMap(int mapIndex)
    {
        if (minimapCamera == null ||
            mapLocations == null ||
            mapIndex < 0 ||
            mapIndex >= mapLocations.Length ||
            mapLocations[mapIndex] == null)
        {
            return;
        }

        // Update the current map
        currentMapIndex = mapIndex;

        // Move the minimap camera to the selected area
        minimapCamera.transform.position =
            mapLocations[mapIndex].position;

        // Change the camera zoom
        if (mapSizes != null && mapIndex < mapSizes.Length)
        {
            minimapCamera.orthographicSize = mapSizes[mapIndex];
        }

        // Only show the walls belonging to the active map
        if (mapWalls != null)
        {
            for (int i = 0; i < mapWalls.Length; i++)
            {
                if (mapWalls[i] != null)
                {
                    mapWalls[i].SetActive(i == mapIndex);
                }
            }
        }

        Debug.Log("Minimap changed to area " + (mapIndex + 1));
    }
}
