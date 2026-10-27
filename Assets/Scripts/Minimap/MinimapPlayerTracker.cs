
using UnityEngine;

public class MinimapPlayerTracker : MonoBehaviour
{
    public Transform player;
    public RectTransform playerDot;
    public RectTransform minimap;

    // World positions (Unity X and Z)
    public Vector2 worldPoint1 = new Vector2(-41.82f, 27.32f);
    public Vector2 worldPoint2 = new Vector2(-21.81f, 0.85f);
    public Vector2 worldPoint3 = new Vector2(20.12f, 19.46f);

    // Matching blueprint positions (normalized 0 to 1)
    public Vector2 mapPoint1 = new Vector2(0.21f, 0.86f);
    public Vector2 mapPoint2 = new Vector2(0.45f, 0.40f);
    public Vector2 mapPoint3 = new Vector2(0.90f, 0.73f);

    void Update()
    {
        if (player == null || playerDot == null || minimap == null)
            return;

        Vector2 worldPosition = new Vector2(
            player.position.x,
            player.position.z
        );

        Vector2 worldA = worldPoint2 - worldPoint1;
        Vector2 worldB = worldPoint3 - worldPoint1;

        Vector2 mapA = mapPoint2 - mapPoint1;
        Vector2 mapB = mapPoint3 - mapPoint1;

        float determinant =
            worldA.x * worldB.y -
            worldA.y * worldB.x;

        if (Mathf.Abs(determinant) < 0.0001f)
            return;

        Vector2 offset = worldPosition - worldPoint1;

        float u = (
            offset.x * worldB.y -
            offset.y * worldB.x
        ) / determinant;

        float v = (
            worldA.x * offset.y -
            worldA.y * offset.x
        ) / determinant;

        Vector2 mapPosition =
            mapPoint1 + u * mapA + v * mapB;

        float x = (mapPosition.x - 0.5f) * minimap.rect.width;
        float y = (mapPosition.y - 0.5f) * minimap.rect.height;

        playerDot.anchoredPosition = new Vector2(x, y);
    }
}
