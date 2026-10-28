
using UnityEngine;
using UnityEditor;

public class MinimapWallGenerator
{
    [MenuItem("Tools/Generate Minimap Walls")]
    public static void GenerateWalls()
    {
        GameObject minimapParent =
            GameObject.Find("MinimapGeometry");

        if (minimapParent == null)
        {
            Debug.LogError("MinimapGeometry was not found!");
            return;
        }

        GameObject[] selectedObjects =
            Selection.gameObjects;

        int count = 0;

        foreach (GameObject selected in selectedObjects)
        {
            MeshFilter[] meshes =
                selected.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter mesh in meshes)
            {
                // Only include objects with Wall in their name.
                if (!mesh.gameObject.name.ToLower().Contains("wall"))
                    continue;

                if (mesh.sharedMesh == null)
                    continue;

                GameObject copy = new GameObject(
                    "Minimap_" + mesh.gameObject.name
                );

                Undo.RegisterCreatedObjectUndo(
                    copy, "Generate Minimap Walls"
                );

                copy.transform.SetParent(
                    minimapParent.transform, false
                );

                copy.transform.position =
                    mesh.transform.position;

                copy.transform.rotation =
                    mesh.transform.rotation;

                copy.transform.localScale =
                    mesh.transform.lossyScale;

                MeshFilter newFilter =
                    copy.AddComponent<MeshFilter>();

                newFilter.sharedMesh = mesh.sharedMesh;

                MeshRenderer newRenderer =
                    copy.AddComponent<MeshRenderer>();

                newRenderer.sharedMaterials =
                    mesh.GetComponent<MeshRenderer>()?.sharedMaterials;

                count++;
            }
        }

        Debug.Log("Generated " + count + " minimap wall meshes.");
    }
}
