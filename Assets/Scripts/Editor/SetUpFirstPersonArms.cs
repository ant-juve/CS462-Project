using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off tool: Tools > Set Up First Person Arms
// Puts the FPS Female Arms model on the main camera with a FirstPersonArms component,
// and removes the old robot body if it's still there.
public static class SetUpFirstPersonArms
{
    private const string ModelPath = "Assets/Models/FemaleArms/FPS_Female_Arms.fbx";
    private const string IdlePose = "Throw Pose";
    private const string HoldPose = "Fists Pose";

    [MenuItem("Tools/Set Up First Person Arms")]
    static void SetUp()
    {
        Camera cam = Camera.main;
        if (cam == null || Object.FindFirstObjectByType<FirstPersonPlayer>() == null)
        {
            EditorUtility.DisplayDialog("Set Up First Person Arms",
                "Couldn't find the main camera and first-person player in the open scene.", "OK");
            return;
        }

        if (cam.GetComponentInChildren<FirstPersonArms>(true) != null)
        {
            EditorUtility.DisplayDialog("Set Up First Person Arms", "The camera already has arms.", "OK");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        AnimationClip idle = FindClip(IdlePose);
        AnimationClip hold = FindClip(HoldPose);
        if (model == null || idle == null || hold == null)
        {
            EditorUtility.DisplayDialog("Set Up First Person Arms",
                "Couldn't find " + ModelPath + " or its \"" + IdlePose + "\" / \"" + HoldPose + "\" poses.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Set Up First Person Arms");

        // The robot body from the Starter Assets pack isn't needed with arms
        foreach (Transform t in Object.FindFirstObjectByType<FirstPersonPlayer>().GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Body" && t.GetComponent<Animator>() != null)
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                break;
            }
        }

        GameObject pivot = new GameObject("ArmsPivot");
        Undo.RegisterCreatedObjectUndo(pivot, "Create arms pivot");
        pivot.transform.SetParent(cam.transform, false);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot.transform);
        Undo.RegisterCreatedObjectUndo(instance, "Add arms model");

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = Undo.AddComponent<Animator>(instance);

        FirstPersonArms arms = Undo.AddComponent<FirstPersonArms>(pivot);
        arms.arms = animator;
        arms.idlePose = idle;
        arms.holdPose = hold;

        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Selection.activeGameObject = pivot;
        Debug.Log("Added first-person arms to the camera. Save the scene to keep them.");
    }

    static AnimationClip FindClip(string clipName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is AnimationClip clip && clip.name == clipName)
                return clip;
        }
        return null;
    }
}
