
using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour
{
    public Image minimapImage;

    public Sprite[] blueprints;

    public void ChangeMap(int mapIndex)
    {
        if (mapIndex >= 0 && mapIndex < blueprints.Length)
        {
            minimapImage.sprite = blueprints[mapIndex];
        }
    }
}
