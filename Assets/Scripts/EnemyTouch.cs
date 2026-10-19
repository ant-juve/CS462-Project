using UnityEngine;

public class EnemyTouch : MonoBehaviour
{
    [Tooltip("Tag used to identify the player object")]
    [SerializeField] private string playerTag = "Player";


    // Use this if your collider has 'Is Trigger' CHECKED
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            GameManager.Instance.TriggerGameOver();
        }
    }
}