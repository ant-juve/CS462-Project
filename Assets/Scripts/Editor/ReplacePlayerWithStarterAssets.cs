using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off tool: Tools > Replace Player With Starter Assets FPS
// Swaps the old "Player" (CharacterController + child camera) in the open scene for the
// Starter Assets first-person PlayerCapsule, its MainCamera and its Cinemachine follow camera.
public static class ReplacePlayerWithStarterAssets
{
    private const string PrefabFolder = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/";
    private const string CameraTargetTag = "CinemachineTarget";
    private const float DefaultSensitivity = 2f; // same default the settings menu uses

    [MenuItem("Tools/Replace Player With Starter Assets FPS")]
    static void Replace()
    {
        GameObject oldPlayer = FindOldPlayer();
        if (oldPlayer == null)
        {
            EditorUtility.DisplayDialog("Replace Player",
                "Couldn't find an object named \"Player\" with a CharacterController in the open scene.", "OK");
            return;
        }

        GameObject capsulePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "PlayerCapsule.prefab");
        GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "MainCamera.prefab");
        GameObject followPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "PlayerFollowCamera.prefab");
        if (capsulePrefab == null || cameraPrefab == null || followPrefab == null)
        {
            EditorUtility.DisplayDialog("Replace Player",
                "Couldn't find the Starter Assets first-person prefabs in " + PrefabFolder, "OK");
            return;
        }

        AddTagIfMissing(CameraTargetTag);
        Undo.SetCurrentGroupName("Replace Player With Starter Assets FPS");

        // The old player's pivot is the middle of its capsule; the new one's is at its feet
        CharacterController oldController = oldPlayer.GetComponent<CharacterController>();
        Vector3 feet = oldPlayer.transform.TransformPoint(oldController.center)
                     - Vector3.up * (oldController.height * 0.5f * oldPlayer.transform.lossyScale.y);
        Quaternion facing = Quaternion.Euler(0f, oldPlayer.transform.eulerAngles.y, 0f);

        GameObject mainCamera = Spawn(cameraPrefab);
        GameObject followCamera = Spawn(followPrefab);
        GameObject capsule = Spawn(capsulePrefab);
        capsule.transform.SetPositionAndRotation(feet + Vector3.up * 0.05f, facing);

        // The follow camera has to be pointed at the capsule's camera root
        Transform cameraRoot = capsule.transform.Find("PlayerCameraRoot");
        CinemachineCamera cmCamera = followCamera.GetComponent<CinemachineCamera>();
        Undo.RecordObject(cmCamera, "Set follow target");
        cmCamera.Follow = cameraRoot;
        mainCamera.transform.SetPositionAndRotation(cameraRoot.position, cameraRoot.rotation);

        FirstPersonController controller = capsule.GetComponent<FirstPersonController>();
        Undo.RecordObject(controller, "Set rotation speed");
        controller.RotationSpeed = DefaultSensitivity * 2f; // matches FirstPersonPlayer's conversion

        Undo.AddComponent<FirstPersonPlayer>(capsule);
        Undo.DestroyObjectImmediate(oldPlayer); // also removes its old camera

        EditorSceneManager.MarkSceneDirty(capsule.scene);
        Selection.activeGameObject = capsule;
        Debug.Log("Replaced Player with the Starter Assets first-person controller. Save the scene to keep it.");
    }

    static GameObject FindOldPlayer()
    {
        foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cc.name == "Player" && cc.GetComponent<FirstPersonController>() == null)
                return cc.gameObject;
        }
        return null;
    }

    static GameObject Spawn(GameObject prefab)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Spawn " + prefab.name);
        return instance;
    }

    static void AddTagIfMissing(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                return;
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
