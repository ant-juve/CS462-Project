
using UnityEngine;

public class MinimapPlayerDot : MonoBehaviour
{
    public Transform player;
    public float dotHeight = 5f;

    void LateUpdate()
    {
        if (player == null)
            return;

        transform.position = new Vector3(
            player.position.x,
            player.position.y + dotHeight,
            player.position.z
        );
    }
}
