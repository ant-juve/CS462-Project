
using UnityEngine;
using UnityEditor;

public class MinimapWallGenerator
{
    [MenuItem("Tools/Generate Minimap Walls")]
    public static void GenerateWalls()
    {
        GameObject level = GameObject.Find("_Level");
        GameObject minimapGeometry = GameObject.Find("MinimapGeometry");

        if (level == null || minimapGeometry == null)
        {
            Debug.LogError("_Level or MinimapGeometry was not found!");
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

        string[] sourceNames = { "Hallways", "Map2", "Map3", "Map4" };

        for (int i = 0; i < sourceNames.Length; i++)
        {
            Transform source = level.transform.Find(sourceNames[i]);

            if (source == null)
            {
                Debug.LogWarning(sourceNames[i] + " was not found.");
                continue;
            }

            string groupName = "Map" + (i + 1) + "Walls";

            Transform oldGroup = minimapGeometry.transform.Find(groupName);

            if (oldGroup != null)
            {
                Undo.DestroyObjectImmediate(oldGroup.gameObject);
            }

            GameObject group = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(group, "Generate Minimap Walls");
            group.transform.SetParent(minimapGeometry.transform, false);
            group.layer = minimapLayer;

            int count = 0;

            BoxCollider[] colliders =
                source.GetComponentsInChildren<BoxCollider>(true);

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

                Undo.RegisterCreatedObjectUndo(cube, "Generate Minimap Wall");

                cube.name = "Minimap_" + objectName;
                cube.transform.SetParent(group.transform);

                cube.transform.position =
                    wall.transform.TransformPoint(wall.center);

                cube.transform.rotation = wall.transform.rotation;

                Vector3 scale = wall.transform.lossyScale;

                cube.transform.localScale = new Vector3(
                    Mathf.Max(1f, wall.size.x * Mathf.Abs(scale.x)),
                    wall.size.y * Mathf.Abs(scale.y),
                    wall.size.z * Mathf.Abs(scale.z)
                );

                cube.GetComponent<MeshRenderer>().sharedMaterial =
                    wallMaterial;

                Object.DestroyImmediate(cube.GetComponent<Collider>());

                cube.layer = minimapLayer;

                count++;
            }

            Debug.Log(groupName + ": Generated " + count + " walls.");
        }
    }
}
