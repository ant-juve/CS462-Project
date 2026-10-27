
using UnityEngine;

public class MinimapZone : MonoBehaviour
{
    public MinimapManager minimapManager;
    public int mapIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            minimapManager.ChangeMap(mapIndex);
        }
    }
}
