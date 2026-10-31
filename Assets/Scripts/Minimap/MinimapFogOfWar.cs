
using UnityEngine;
using UnityEngine.UI;

public class MinimapFogOfWar : MonoBehaviour
{
    public RawImage fogImage;
    public Transform player;
    public Camera minimapCamera;
    public MinimapManager minimapManager;

    public int textureSize = 512;
    public float revealRadius = 5f;

    // One fog texture for each map
    private Texture2D[] fogTextures;

    private int currentMap = -1;

    void Start()
    {
        int mapCount = minimapManager.mapLocations.Length;

        fogTextures = new Texture2D[mapCount];

        // Create a separate black texture for every map
        for (int map = 0; map < mapCount; map++)
        {
            Texture2D texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false
            );

            Color[] pixels = new Color[textureSize * textureSize];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.black;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            fogTextures[map] = texture;
        }

        fogImage.color = Color.white;
    }

    void Update()
    {
        if (minimapManager == null ||
            minimapCamera == null ||
            player == null)
        {
            return;
        }

        int mapIndex = minimapManager.currentMapIndex;

        if (mapIndex < 0 || mapIndex >= fogTextures.Length)
            return;

        // Switch to the correct fog texture
        if (currentMap != mapIndex)
        {
            currentMap = mapIndex;
            fogImage.texture = fogTextures[currentMap];
        }

        RevealArea();
    }

    void RevealArea()
    {
        Texture2D fogTexture = fogTextures[currentMap];

        // Find the player's position on the minimap
        Vector3 screenPosition =
            minimapCamera.WorldToViewportPoint(player.position);

        if (screenPosition.z <= 0)
            return;

        int centerX = Mathf.RoundToInt(
            screenPosition.x * textureSize
        );

        int centerY = Mathf.RoundToInt(
            screenPosition.y * textureSize
        );

        // Convert world units to texture pixels
        float pixelsPerUnit =
            textureSize / (minimapCamera.orthographicSize * 2f);

        int radius = Mathf.RoundToInt(
            revealRadius * pixelsPerUnit
        );

        // Reveal a circular area around the player
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                int pixelX = centerX + x;
                int pixelY = centerY + y;

                if (pixelX >= 0 && pixelX < textureSize &&
                    pixelY >= 0 && pixelY < textureSize)
                {
                    fogTexture.SetPixel(
                        pixelX,
                        pixelY,
                        Color.clear
                    );
                }
            }
        }

        fogTexture.Apply();
    }

    void OnDestroy()
    {
        if (fogTextures == null)
            return;

        foreach (Texture2D texture in fogTextures)
        {
            if (texture != null)
                Destroy(texture);
        }
    }
}
