
using UnityEngine;
using UnityEditor;

public class MinimapWallGenerator
{
    [MenuItem("Tools/Generate Minimap Walls")]
    public static void GenerateWalls()
    {
        GameObject minimapParent = GameObject.Find("MinimapGeometry");

        if (minimapParent == null)
        {
            Debug.LogError("MinimapGeometry was not found!");
            return;
        }

        Material wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Minimap/MinimapWall.mat"
        );

        if (wallMaterial == null)
        {
            Debug.LogError("MinimapWall material was not found!");
            return;
        }

        int minimapLayer = LayerMask.NameToLayer("MiniMap");

        if (minimapLayer == -1)
        {
            Debug.LogError("MiniMap layer was not found!");
            return;
        }

        GameObject[] selectedObjects = Selection.gameObjects;
        int count = 0;

        foreach (GameObject selected in selectedObjects)
        {
            BoxCollider[] colliders =
                selected.GetComponentsInChildren<BoxCollider>(true);

            foreach (BoxCollider wall in colliders)
            {
                string objectName = wall.gameObject.name;

                if (!objectName.StartsWith("Wall_") &&
                    !objectName.StartsWith("Collider_"))
                {
                    continue;
                }

                GameObject cube =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);

                Undo.RegisterCreatedObjectUndo(
                    cube, "Generate Minimap Walls"
                );

                cube.name = "Minimap_" + objectName;

                cube.transform.SetParent(minimapParent.transform);

                cube.transform.position =
                    wall.transform.TransformPoint(wall.center);

                cube.transform.rotation = wall.transform.rotation;

                Vector3 scale = wall.transform.lossyScale;

                // Set every minimap wall's X scale to 1.
                // Keep the original Y and Z dimensions.
                cube.transform.localScale = new Vector3(
                    1f,
                    wall.size.y * Mathf.Abs(scale.y),
                    wall.size.z * Mathf.Abs(scale.z)
                );

                cube.GetComponent<MeshRenderer>().sharedMaterial =
                    wallMaterial;

                Object.DestroyImmediate(cube.GetComponent<Collider>());

                cube.layer = minimapLayer;

                count++;
            }
        }

        Debug.Log("Generated " + count + " minimap walls.");
    }
}
