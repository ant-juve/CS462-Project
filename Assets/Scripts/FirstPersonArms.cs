using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// First-person arms that hang off the camera. The model only has static poses, so all the
// movement here is procedural: idle breathing, walk/sprint bob, arm swing and a running pose,
// look sway, jump/land,
// a dip when switching hotbar slots, and a reach-out-and-grab when picking something up.
//
// Grabbing: the palm reaches to the nearest point on the item's surface with the wrist turned
// toward it, then each finger closes joint by joint until it touches the item. Items keep
// their real size and stay exactly where the hand closed on them.
//
// Only the model's left arm is shown: its right arm reads as a backwards left hand from
// the first-person view. The right arm on screen is a mirrored copy of the left arm,
// made at runtime.
//
// Hierarchy:
//   MainCamera
//     ArmsPivot      <- this component; procedural motion moves this
//       FPS_Female_Arms (model, with Animator; shows the left arm)
//       FPS_Female_Arms (Right)  <- mirrored copy made at runtime; shows the right arm
[DefaultExecutionOrder(100)] // after the camera has moved for this frame, so the arms are placed against it
public class FirstPersonArms : MonoBehaviour
{
    [Header("Model")]
    public Animator arms;
    [Tooltip("Pose when the hands are empty")]
    public AnimationClip idlePose;
    [Tooltip("Pose whose fingers are used as the fully closed hand (the arm itself stays in the idle pose)")]
    public AnimationClip holdPose;
    [Tooltip("Where the model sits relative to the camera")]
    public Vector3 modelOffset = new Vector3(0f, -1.85f, -0.1f);
    public Vector3 modelRotation = new Vector3(0f, 180f, 0f);

    [Header("Fingers")]
    [Tooltip("Rough thickness of a finger from its bone to its skin, in meters")]
    public float fingerRadius = 0.009f;
    [Tooltip("Rough distance from the palm's center line to its skin, in meters")]
    public float palmThickness = 0.015f;
    [Tooltip("How quickly fingers close around (or open from) an item")]
    public float fingerCloseSpeed = 6f;

    [Header("Carrying")]
    [Tooltip("Extra offset for the right arm while holding something (camera space: right, up, forward)")]
    public Vector3 carryOffset = new Vector3(0f, 0.1f, 0.03f);
    [Tooltip("How quickly the arm moves into and out of the carry offset")]
    public float carryBlendSpeed = 4f;

    [Header("Look Sway")]
    public float swayAmount = 0.02f;
    public float swayMax = 0.06f;
    public float swaySmooth = 8f;

    [Header("Resting")]
    [Tooltip("When empty, arms hang down at your sides from the body's shoulders. How far the shoulders are below eye level and out from the center, in meters")]
    public float shoulderDrop = 0.25f;
    public float shoulderWidth = 0.2f;
    [Tooltip("How far out to the side and forward the hanging arms angle (per meter of arm)")]
    public float hangOutward = 0.35f;
    public float hangForward = -0.05f;
    [Tooltip("How much resting fingers curl (0 = straight, 1 = fist)")]
    [Range(0f, 1f)] public float relaxedCurl = 0.3f;
    [Range(0f, 1f)] public float relaxedThumbCurl = 0.15f;
    [Tooltip("How much a resting wrist straightens to follow the forearm (0 = keep the pose's bent-back wrist)")]
    [Range(0f, 1f)] public float wristRelax = 0.85f;

    [Header("Walk Bob")]
    [Tooltip("How far the arms sway side to side (x) and bounce up and down (y), in meters")]
    public Vector2 bobSize = new Vector2(0.006f, 0.005f);
    [Tooltip("How quickly the bob fades in when you start moving and out when you stop")]
    public float bobFade = 3f;
    public float sprintBobMultiplier = 1.6f;
    public float sprintLower = 0.03f;
    [Tooltip("How quickly the arms ease down when you start sprinting and back up when you stop")]
    public float sprintBlendSpeed = 3f;

    [Header("Arm Swing")]
    [Tooltip("Full forward-and-back arm swings per meter moved (lower = slower swing). Each swing is two steps, and the head bob and arm bounce follow the same rhythm")]
    public float swingCyclesPerMeter = 0.22f;
    [Tooltip("How far empty arms swing forward and back from the shoulder while walking, in degrees")]
    public float walkSwing = 8f;
    [Tooltip("How far empty arms pump forward and back while sprinting, in degrees")]
    public float runSwing = 40f;
    [Tooltip("Elbow bend while sprinting, in degrees: with the arm swung back, and with it swung forward")]
    public float runElbowBack = 65f;
    public float runElbowForward = 105f;
    [Tooltip("How much the forearms angle in toward the body while sprinting (per meter forward)")]
    public float runInward = 0.3f;
    [Tooltip("How much the fingers close into a loose fist while sprinting (0 = relaxed, 1 = fist)")]
    [Range(0f, 1f)] public float runFistCurl = 1f;
    [Tooltip("How quickly the arms move into the running pose when you start sprinting and out when you stop")]
    public float runBlendSpeed = 4f;

    [Header("Head Bob")]
    [Tooltip("How far the view sways side to side (x), dips (y) and surges forward and back (z) with each step while walking, in meters")]
    public Vector3 headBobWalk = new Vector3(0.004f, 0.01f, 0.005f);
    [Tooltip("The same while sprinting")]
    public Vector3 headBobRun = new Vector3(0.006f, 0.015f, 0.008f);
    [Tooltip("How far the head nods down as each foot lands (x) and tilts toward the planted foot (y), in degrees, walking and sprinting. Keep the tilt small: your eyes keep the horizon level, so a tilting view reads as a crooked head")]
    public Vector2 headTiltWalk = new Vector2(0.3f, 0.1f);
    public Vector2 headTiltRun = new Vector2(0.5f, 0.15f);
    [Tooltip("Extra quick dip right as the heel hits, as a fraction of the step dip")]
    public float heelStrike = 0.25f;

    [Header("Jump / Land (head)")]
    [Tooltip("How hard the head drops when landing, per m/s of falling speed (the knees soak up the landing)")]
    public float landImpact = 0.35f;
    [Tooltip("Strongest landing drop, as a downward speed given to the head, in m/s")]
    public float maxLandImpact = 2.5f;
    [Tooltip("The head lags a little behind as the legs push off, in m/s")]
    public float takeoffKick = 0.35f;
    [Tooltip("How far the head nods forward per meter it drops")]
    public float landNod = 25f;
    [Tooltip("Ignore landings after less time than this in the air, e.g. stepping off a stair, in seconds")]
    public float minAirTime = 0.15f;
    [Tooltip("Springiness of the knees: higher bounces back faster")]
    public float kneeStiffness = 180f;
    public float kneeDamping = 16f;

    [Header("Start / Stop Lean")]
    [Tooltip("How far the head leans per m/s² of speeding up (forward) or slowing down (back), in degrees")]
    public float leanPerAcceleration = 0.3f;
    public float maxHeadLean = 3f;
    [Tooltip("How springy the lean is: higher settles faster")]
    public float leanStiffness = 60f;
    [Tooltip("How quickly the lean's wobble dies out")]
    public float leanDamping = 9f;

    [Header("Idle Breathing")]
    public float breatheAmount = 0.004f;
    public float breatheSpeed = 1.3f;

    [Header("Jump / Land")]
    public float airOffsetPerSpeed = 0.006f;
    public float landDip = 0.05f;

    [Header("Switch Slots")]
    public float switchDip = 0.25f;
    public float switchDuration = 0.35f;

    [Header("Grab")]
    [Tooltip("Seconds for the hand to reach out to an item")]
    public float reachDuration = 0.4f;
    [Tooltip("Seconds to pull the arm back once the item is in hand")]
    public float returnDuration = 0.45f;
    [Tooltip("Furthest the arm can stretch toward an item, in meters")]
    public float maxReach = 0.4f;
    [Tooltip("Seconds to draw in an item the hand couldn't quite reach")]
    public float pullDuration = 0.2f;
    [Tooltip("How much the wrist turns so the palm faces the item (0-1)")]
    [Range(0f, 1f)] public float wristAlign = 0.8f;
    [Tooltip("How far the view can dip down to reach low items, like bending over, in meters")]
    public float maxBend = 1.1f;
    [Tooltip("How far the view can lean forward to reach items, in meters")]
    public float maxLean = 0.5f;
    [Tooltip("Comfortable reach from the eyes before bending or leaning, in meters")]
    public float comfortableReach = 0.7f;

    private Hotbar hotbar;
    private CharacterController body;
    private FirstPersonController controller;
    private FirstPersonBody fullBody; // optional: the visible body, whose shoulders and stride the arms follow
    private Transform cam;
    private Transform cameraRoot;
    private Vector3 cameraRootRest;
    private Vector3 bend; // how far the view dips/leans for the current grab (local to the player)

    private Animator rightArms;
    private Transform leftModel;  // cached transforms of the two arm models
    private Transform rightModel;
    private float holdWeight;

    // Right hand (the mirrored copy's .L bones)
    private Transform rightShoulder;
    private Transform leftShoulder;
    private Transform leftHand;
    private Transform leftForearm;
    private Transform rightForearm;
    private Transform leftUpperArm;
    private Transform rightUpperArm;
    private Transform leftKnuckle;   // middle finger's first knuckle: which way the hand points
    private Transform rightKnuckle;

    // The arms hold a still pose, sampled once. Each frame only the bones the code moves are put
    // back to it, instead of re-running an animation over the whole skeleton.
    private Transform[] posedBones;
    private Quaternion[] posedRotations;
    private readonly List<Transform> leftFingerJoints = new List<Transform>();
    private Quaternion[] leftClosedRotations;
    private bool[] leftIsThumb;
    private bool[] isThumb;
    private float currentReach; // how far the right arm is reaching for an item this frame (0-1)
    private Transform rightHand;
    private Transform palm;              // a point on the palm's surface; held items attach here
    private Vector3 palmNormalLocal;     // in rightHand's local space
    private Vector3 fingerDirectionLocal; // wrist-to-fingertips, in rightHand's local space
    private float knuckleDistance;       // palm point to the knuckle line, in meters
    private readonly List<Transform> fingerJoints = new List<Transform>();
    private readonly List<int> fingerStart = new List<int>();  // index of each finger's first joint
    private readonly List<Transform> fingerTips = new List<Transform>();
    private Quaternion[] closedRotations;
    private float[] curlTarget;
    private float[] curl;
    private PickupItem fingersSolvedFor;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 swayPosition;
    private Quaternion lastCameraRotation;
    private float bendAmount; // how far into the current grab's bend the view is (0-1)
    private Vector3 lastVelocity;     // player-local, for the start/stop lean
    private Vector3 acceleration;
    private Vector2 lean;             // pitch (x) and roll (y), degrees
    private Vector2 leanVelocity;
    private float knee;          // how far the head has dropped from bent knees, in meters (negative = down)
    private float kneeVelocity;
    private bool headGrounded = true;
    private float airTime;
    private float fallSpeed;     // downward speed just before landing
    private float swingPhase;

    // Worked out once per frame and shared by everything that needs them
    private Vector3 velocity;     // the player's, world space
    private bool hasBody;         // fullBody is present and running
    private float runBlend;       // eased runWeight
    private float leftSwing;      // degrees each arm is swung forward
    private float rightSwing;
    private float restingCurl;    // how far resting fingers (and thumbs) curl
    private float restingThumbCurl;
    private float bobStrength;
    private float sprintWeight;
    private float runWeight; // 0 = walking pose, 1 = running pose
    private float smoothedLift;
    private float landTimer = 1f;
    private bool wasGrounded = true;
    private PickupItem lastSelected;
    private float switchTimer = 1f;

    private enum GrabPhase { None, Reach, Pull, Return }
    private GrabPhase grabPhase;
    private float grabTimer;
    private PickupItem grabItem;
    private Vector3 reachTarget;
    private Vector3 pullStart;
    private System.Action onGrabbed;

    void Start()
    {
        cam = Camera.main.transform;
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        lastCameraRotation = cam.rotation;

        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player != null)
        {
            hotbar = player.GetComponent<Hotbar>();
            body = player.GetComponent<CharacterController>();
            controller = player.GetComponent<FirstPersonController>();
            fullBody = player.GetComponent<FirstPersonBody>();
            if (fullBody != null && !fullBody.isActiveAndEnabled)
                fullBody = null;
            if (controller != null && controller.CinemachineCameraTarget != null)
            {
                cameraRoot = controller.CinemachineCameraTarget.transform;
                cameraRootRest = cameraRoot.localPosition;
            }
        }

        rightArms = Instantiate(arms.gameObject, arms.transform.parent).GetComponent<Animator>();
        rightArms.name = arms.name + " (Right)";
        SetUpModel(arms, 1f);
        SetUpModel(rightArms, -1f); // mirrored, so its left arm appears on the right

        rightShoulder = FindBone(rightArms, "Shoulder.L");
        leftShoulder = FindBone(arms, "Shoulder.L");
        leftHand = FindBone(arms, "Hand.L");
        rightHand = FindBone(rightArms, "Hand.L");
        leftForearm = FindBone(arms, "Forearm.L");
        rightForearm = FindBone(rightArms, "Forearm.L");
        leftUpperArm = FindBone(arms, "Shoulder_2.L");
        rightUpperArm = FindBone(rightArms, "Shoulder_2.L");
        FindFingers();
        MeasureHand();

        CollectFingers(leftHand, leftFingerJoints, new List<int>(), new List<Transform>());
        leftClosedRotations = ClosedRotations(arms, leftFingerJoints);
        leftIsThumb = ThumbMask(leftFingerJoints);

        // Both arms stay in the relaxed idle pose, which carries items at a natural distance;
        // fingers are closed procedurally around whatever is held
        SamplePose(arms, idlePose);
        SamplePose(rightArms, idlePose);
        leftModel = arms.transform;
        rightModel = rightArms.transform;
        leftKnuckle = Knuckle(leftHand);
        rightKnuckle = Knuckle(rightHand);
        RecordPosedBones();

        if (hotbar != null)
        {
            hotbar.AttachHandTo(palm);
            hotbar.GrabAnimation = PlayGrab;
            lastSelected = hotbar.SelectedItem;
        }
    }

    void OnDestroy()
    {
        if (hotbar != null && hotbar.GrabAnimation == (System.Action<PickupItem, System.Action>)PlayGrab)
            hotbar.GrabAnimation = null;
    }

    // ---------- Setup ----------

    // Places a model copy and hides the model's own right arm, keeping only its left arm
    void SetUpModel(Animator model, float mirrorX)
    {
        model.transform.localPosition = modelOffset;
        model.transform.localRotation = Quaternion.Euler(modelRotation);
        model.transform.localScale = new Vector3(mirrorX, 1f, 1f);

        foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.enabled = !IsSkinnedToRightArm(r);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // floating arms shouldn't cast shadows
            // The mesh's own bounds don't follow the camera-parented pose, so give it a fixed box
            // around the arms (cheaper than recomputing the bounds from the bones every frame)
            Transform space = r.rootBone != null ? r.rootBone : r.transform;
            float toLocal = 1f / Mathf.Max(0.0001f, Mathf.Abs(space.lossyScale.x));
            r.localBounds = new Bounds(space.InverseTransformPoint(model.transform.parent.position), Vector3.one * 3f * toLocal);
            r.updateWhenOffscreen = false;
        }
    }

    // Both arm meshes list the whole skeleton as bones, so look at which bones actually move their vertices
    static bool IsSkinnedToRightArm(SkinnedMeshRenderer r)
    {
        Transform[] bones = r.bones;
        float right = 0f, left = 0f;
        if (r.sharedMesh != null && r.sharedMesh.isReadable)
        {
            foreach (BoneWeight w in r.sharedMesh.boneWeights)
            {
                AddWeight(bones, w.boneIndex0, w.weight0, ref right, ref left);
                AddWeight(bones, w.boneIndex1, w.weight1, ref right, ref left);
                AddWeight(bones, w.boneIndex2, w.weight2, ref right, ref left);
                AddWeight(bones, w.boneIndex3, w.weight3, ref right, ref left);
            }
        }

        if (right + left > 0f)
            return right > left;
        return r.name.Contains("Right"); // weights unavailable: fall back to the mesh name
    }

    static void AddWeight(Transform[] bones, int index, float weight, ref float right, ref float left)
    {
        if (weight <= 0f || index < 0 || index >= bones.Length || bones[index] == null)
            return;
        if (bones[index].name.EndsWith(".R")) right += weight;
        else if (bones[index].name.EndsWith(".L")) left += weight;
    }

    // Each finger is a chain of joints under the hand, ending in an "_end" bone at the fingertip
    void FindFingers()
    {
        CollectFingers(rightHand, fingerJoints, fingerStart, fingerTips);
        isThumb = ThumbMask(fingerJoints);
        curl = new float[fingerJoints.Count];
        curlTarget = new float[fingerJoints.Count];
    }

    static void CollectFingers(Transform hand, List<Transform> joints, List<int> starts, List<Transform> tips)
    {
        foreach (Transform child in hand)
        {
            if (!child.name.StartsWith("Finger_"))
                continue;

            starts.Add(joints.Count);
            Transform joint = child;
            while (joint != null && !joint.name.EndsWith("_end"))
            {
                joints.Add(joint);
                joint = joint.childCount > 0 ? joint.GetChild(0) : null;
            }
            tips.Add(joint != null ? joint : joints[joints.Count - 1]);
        }
        starts.Add(joints.Count); // sentinel: end of the last finger
    }

    // The thumb is the finger with three joints (Finger_5)
    static bool[] ThumbMask(List<Transform> joints)
    {
        bool[] mask = new bool[joints.Count];
        for (int i = 0; i < joints.Count; i++)
            mask[i] = joints[i].name.StartsWith("Finger_5");
        return mask;
    }

    // Finger rotations in the hold pose (a closed fist), sampled once on the given model
    Quaternion[] ClosedRotations(Animator model, List<Transform> joints)
    {
        PlayableGraph graph = PlayableGraph.Create("FirstPersonArms measure");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Measure", model);
        output.SetSourcePlayable(AnimationClipPlayable.Create(graph, holdPose));
        graph.Evaluate(0f);

        Quaternion[] closed = new Quaternion[joints.Count];
        for (int i = 0; i < joints.Count; i++)
            closed[i] = joints[i].localRotation;
        graph.Destroy();
        return closed;
    }

    // Poses the hand open (idle) and closed (hold pose) once, to learn the closed finger rotations,
    // which way the palm faces, and where its surface is
    void MeasureHand()
    {
        Vector3 openTips = AverageTipsAfterPose(idlePose);
        Vector3 knuckles = Vector3.zero;
        foreach (Transform joint in fingerJoints)
            if (joint.name.Contains(".2.")) knuckles += rightHand.InverseTransformPoint(joint.position);
        knuckles /= Mathf.Max(1, CountKnuckles());

        Vector3 closedTips = AverageTipsAfterPose(holdPose);
        closedRotations = new Quaternion[fingerJoints.Count];
        for (int i = 0; i < fingerJoints.Count; i++)
            closedRotations[i] = fingerJoints[i].localRotation;

        // Fingers curl toward the palm, so the palm faces the way the fingertips move when closing
        Vector3 fingerDirection = (openTips - Vector3.zero).normalized;
        Vector3 curlDirection = Vector3.ProjectOnPlane(closedTips - openTips, fingerDirection);
        palmNormalLocal = curlDirection.normalized;

        // Palm center sits between the wrist and the knuckles; the surface is a palm-thickness out from it
        Vector3 palmCenter = knuckles * 0.55f;
        float scale = rightHand.TransformVector(palmNormalLocal).magnitude; // bone units -> meters
        Vector3 palmSurface = palmCenter + palmNormalLocal * (palmThickness / Mathf.Max(scale, 0.0001f));
        fingerDirectionLocal = Vector3.ProjectOnPlane(fingerDirection, palmNormalLocal).normalized;
        knuckleDistance = Vector3.Dot(knuckles - palmSurface, fingerDirectionLocal) * scale;

        // Rig bones are scaled 100x (and mirrored); cancel that so held items keep their real size
        palm = new GameObject("Palm").transform;
        palm.SetParent(rightHand, false);
        Vector3 boneScale = rightHand.lossyScale;
        palm.localScale = new Vector3(1f / boneScale.x, 1f / boneScale.y, 1f / boneScale.z);
        palm.localPosition = palmSurface;
    }

    int CountKnuckles()
    {
        int n = 0;
        foreach (Transform joint in fingerJoints)
            if (joint.name.Contains(".2.")) n++;
        return n;
    }

    // Evaluates a pose on the right arm right away and returns the average fingertip (hand-local)
    Vector3 AverageTipsAfterPose(AnimationClip pose)
    {
        PlayableGraph graph = PlayableGraph.Create("FirstPersonArms measure");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Measure", rightArms);
        output.SetSourcePlayable(AnimationClipPlayable.Create(graph, pose));
        graph.Evaluate(0f);

        Vector3 sum = Vector3.zero;
        foreach (Transform tip in fingerTips)
            sum += rightHand.InverseTransformPoint(tip.position);
        graph.Destroy();
        return sum / Mathf.Max(1, fingerTips.Count);
    }

    // Poses the model once; nothing animates it after this
    static void SamplePose(Animator model, AnimationClip pose)
    {
        PlayableGraph graph = PlayableGraph.Create("FirstPersonArms pose");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Arms", model);
        output.SetSourcePlayable(AnimationClipPlayable.Create(graph, pose));
        graph.Evaluate(0f);
        graph.Destroy();
    }

    // Remembers the posed rotation of every bone the code moves each frame
    void RecordPosedBones()
    {
        var bones = new List<Transform> { leftForearm, leftHand, rightForearm, rightHand };
        bones.AddRange(leftFingerJoints);
        bones.AddRange(fingerJoints);
        bones.RemoveAll(b => b == null);
        posedBones = bones.ToArray();
        posedRotations = new Quaternion[posedBones.Length];
        for (int i = 0; i < posedBones.Length; i++)
            posedRotations[i] = posedBones[i].localRotation;
    }

    void ResetPosedBones()
    {
        for (int i = 0; i < posedBones.Length; i++)
            posedBones[i].localRotation = posedRotations[i];
    }

    static Transform Knuckle(Transform hand)
    {
        if (hand == null)
            return null;
        foreach (Transform child in hand)
            if (child.name.StartsWith("Finger_3"))
                return child.childCount > 0 ? child.GetChild(0) : child;
        return null;
    }

    // ---------- Per frame ----------

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return; // paused
        if (posedBones == null)
            return; // scripts were recompiled during Play mode and the setup was lost; restart Play mode

        velocity = body != null ? body.velocity : Vector3.zero;
        hasBody = fullBody != null && fullBody.IsReady;
        UpdateCarryBlend(dt);
        ResetPosedBones();

        Vector3 offset = Vector3.zero;
        offset += LookSway(dt);
        offset += WalkBob(dt);
        offset += Breathing();
        offset += JumpAndLand(dt);
        offset += SwitchDip(dt);

        // WalkBob just updated the gait; work out what the arms share from it
        runBlend = Mathf.SmoothStep(0f, 1f, runWeight);
        float swing = Mathf.Sin(swingPhase) * Mathf.Lerp(walkSwing * Mathf.Clamp01(bobStrength), runSwing, runBlend);
        leftSwing = -swing;
        rightSwing = swing;
        restingCurl = Mathf.Lerp(relaxedCurl, Mathf.Max(relaxedCurl, runFistCurl), runBlend);
        restingThumbCurl = Mathf.Lerp(relaxedThumbCurl, Mathf.Max(relaxedThumbCurl, runFistCurl), runBlend);

        transform.localPosition = restPosition + offset;
        transform.localRotation = restRotation * Quaternion.Euler(-swayPosition.y * 100f, swayPosition.x * 100f, 0f);

        leftModel.localPosition = modelOffset;
        leftModel.localRotation = Quaternion.Euler(modelRotation);
        HangArm(leftModel, leftShoulder, leftHand, -1f, 1f, leftSwing);
        BendElbow(leftUpperArm, leftForearm, leftHand, -1f, 1f, leftSwing);
        StraightenWrist(leftHand, leftForearm, leftKnuckle, 1f);
        for (int i = 0; i < leftFingerJoints.Count; i++)
            leftFingerJoints[i].localRotation = Quaternion.Slerp(leftFingerJoints[i].localRotation, leftClosedRotations[i],
                leftIsThumb[i] ? restingThumbCurl : restingCurl);

        UpdateGrab(dt);
        UpdateFingers(dt);

        UpdateHead(dt);
    }

    PickupItem HeldItem => grabPhase != GrabPhase.Reach && grabPhase != GrabPhase.Pull && hotbar != null ? hotbar.SelectedItem : null;

    void UpdateCarryBlend(float dt)
    {
        holdWeight = Mathf.MoveTowards(holdWeight, HeldItem != null ? 1f : 0f, carryBlendSpeed * dt);
    }

    // ---------- Grab ----------

    // Called by the hotbar when the player picks something up
    void PlayGrab(PickupItem item, System.Action grabbed)
    {
        grabItem = item;
        onGrabbed = grabbed;
        grabPhase = GrabPhase.Reach;
        grabTimer = 0f;
        reachTarget = GrabPoint(item);
        bend = BendFor(reachTarget);
    }

    // Like a person bending over and leaning in: dip the view for items below arm's reach,
    // and lean forward for items further out than is comfortable
    Vector3 BendFor(Vector3 target)
    {
        if (cameraRoot == null)
            return Vector3.zero;

        Vector3 local = cameraRoot.parent.InverseTransformPoint(target) - cameraRootRest;
        float below = -local.y;
        float ahead = new Vector2(local.x, local.z).magnitude;
        float dip = Mathf.Clamp(below - comfortableReach, 0f, maxBend);
        float lean = Mathf.Clamp(ahead - comfortableReach * 0.6f, 0f, maxLean);
        Vector3 flat = new Vector3(local.x, 0f, local.z).normalized;
        return Vector3.down * dip + flat * lean;
    }

    void ApplyBend(float amount)
    {
        bendAmount = amount;
    }

    void UpdateGrab(float dt)
    {
        // Start from the arm's normal pose each frame, then bend it toward the item
        rightModel.localPosition = modelOffset + Quaternion.Inverse(transform.localRotation) * carryOffset * holdWeight;
        rightModel.localRotation = Quaternion.Euler(modelRotation);

        // Empty hand hangs at your side; it swings up as it reaches and stays up while carrying
        float resting = (1f - holdWeight) * (1f - currentReach);
        HangArm(rightModel, rightShoulder, palm, 1f, resting, rightSwing);
        BendElbow(rightUpperArm, rightForearm, rightHand, 1f, resting, rightSwing);
        StraightenWrist(rightHand, rightForearm, rightKnuckle, resting);

        if (grabPhase == GrabPhase.None)
        {
            currentReach = 0f;
            return;
        }

        grabTimer += dt;
        switch (grabPhase)
        {
            case GrabPhase.Reach:
            {
                float t = Mathf.Clamp01(grabTimer / reachDuration);
                ApplyBend(Ease(t));
                reachTarget = GrabPoint(grabItem);
                currentReach = Ease(t);
                ReachToward(reachTarget, grabItem.WorldCenter, currentReach);

                if (t >= 1f)
                {
                    if (grabItem.SurfaceDistance(palm.position) <= 0.03f)
                    {
                        CloseHandOnItem();
                    }
                    else
                    {
                        // Out of reach: draw the item the last bit into the hand
                        grabPhase = GrabPhase.Pull;
                        grabTimer = 0f;
                        pullStart = grabItem.transform.position;
                    }
                }
                break;
            }
            case GrabPhase.Pull:
            {
                float t = Mathf.Clamp01(grabTimer / pullDuration);
                ApplyBend(1f);
                currentReach = 1f;
                ReachToward(reachTarget, grabItem.WorldCenter, 1f);

                // Measure from where the item started, against where the palm is now (it keeps moving
                // a little), so the item always ends exactly against the palm
                grabItem.transform.position = pullStart;
                Vector3 toPalm = palm.position - grabItem.ClosestSurfacePoint(palm.position);
                grabItem.transform.position = pullStart + toPalm * (t * t);
                if (t >= 1f)
                    CloseHandOnItem();
                break;
            }
            case GrabPhase.Return:
            {
                float t = Mathf.Clamp01(grabTimer / returnDuration);
                ApplyBend(1f - Ease(t));
                currentReach = 1f - Ease(t);
                ReachToward(reachTarget, reachTarget, currentReach);
                if (t >= 1f)
                {
                    grabPhase = GrabPhase.None;
                    ApplyBend(0f);
                }
                break;
            }
        }
    }

    void CloseHandOnItem()
    {
        grabItem.CaptureHeldPose(palm); // the item stays exactly where the hand met it
        grabPhase = GrabPhase.Return;
        grabTimer = 0f;
        grabItem = null;
        fingersSolvedFor = null;

        onGrabbed?.Invoke(); // the hotbar puts the item in the hand now
        onGrabbed = null;
        lastSelected = hotbar.SelectedItem;
    }

    // Where the palm should land: on the side of the item facing the hand, slid along the fingers'
    // direction so the knuckles line up with the far edge. The fingers can then hook over that edge,
    // the way you'd pick up a box from above.
    Vector3 GrabPoint(PickupItem item)
    {
        Vector3 face = item.SurfacePointToward(palm.position);
        Vector3 normal = (face - item.WorldCenter).normalized;
        Vector3 along = Vector3.ProjectOnPlane(rightHand.TransformVector(fingerDirectionLocal), normal).normalized;
        if (along == Vector3.zero)
            return face;

        // How far the face extends from `face` in the fingers' direction
        Vector3 justInside = face - normal * 0.003f;
        float inside = 0f, outside = 0.5f;
        for (int i = 0; i < 16; i++)
        {
            float mid = (inside + outside) * 0.5f;
            if (item.SurfaceDistance(justInside + along * mid) > 0f) outside = mid;
            else inside = mid;
        }

        float slide = Mathf.Clamp(outside - knuckleDistance, -outside, outside);
        return face + along * slide;
    }

    // Turns the wrist so the palm faces `faceToward`, swings the arm around its shoulder so the palm
    // points at `target`, and stretches it toward the target (up to maxReach), blended in by `amount`
    void ReachToward(Vector3 target, Vector3 faceToward, float amount)
    {
        if (amount <= 0f)
            return;

        Vector3 palmNormal = rightHand.TransformVector(palmNormalLocal).normalized;
        Vector3 toFace = faceToward - palm.position;
        if (toFace.sqrMagnitude > 0.0001f)
        {
            Quaternion turn = Quaternion.FromToRotation(palmNormal, toFace.normalized);
            rightHand.rotation = Quaternion.Slerp(Quaternion.identity, turn, amount * wristAlign) * rightHand.rotation;
        }

        Transform model = rightModel;
        Vector3 shoulder = rightShoulder.position;
        Vector3 toPalm = palm.position - shoulder;
        Vector3 toTarget = target - shoulder;

        Quaternion aim = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(toPalm, toTarget), amount);
        model.rotation = aim * model.rotation;
        model.position = shoulder + aim * (model.position - shoulder);

        float stretch = Mathf.Clamp(toTarget.magnitude - toPalm.magnitude, 0f, maxReach);
        model.position += toTarget.normalized * stretch * amount;
    }

    // Moves an arm so it hangs down at the player's side from the body's shoulder, blended in by
    // `amount`. The arm models ride on the camera, so this uses the body (not the view) to decide
    // where the shoulder is: looking down then shows the hands at your sides instead of in front.
    // side: +1 for the right arm, -1 for the left. swingAngle: degrees the arm is swung forward (negative = back)
    void HangArm(Transform model, Transform shoulder, Transform hand, float side, float amount, float swingAngle)
    {
        if (amount <= 0f)
            return;

        Transform facing = hasBody ? fullBody.Torso : body != null ? body.transform : cam;
        Vector3 eye = cameraRoot != null ? cameraRoot.position : cam.position;
        Vector3 bodyShoulder = hasBody
            ? fullBody.ShoulderPosition(side)
            : eye - facing.up * shoulderDrop + facing.right * (side * shoulderWidth);

        Vector3 shoulderPos = shoulder.position;
        Vector3 toHand = hand.position - shoulderPos;
        Vector3 hang = (-facing.up + facing.right * (side * hangOutward) + facing.forward * hangForward).normalized;
        hang = Quaternion.AngleAxis(-swingAngle, facing.right) * hang;
        Quaternion swing = Quaternion.FromToRotation(toHand, hang);

        Quaternion hungRotation = swing * model.rotation;
        Vector3 hungPosition = bodyShoulder + swing * (model.position - shoulderPos);
        model.rotation = Quaternion.Slerp(model.rotation, hungRotation, amount);
        model.position = Vector3.Lerp(model.position, hungPosition, amount);
    }

    // Bends the elbow up into a running arm: the forearm points forward and a little in toward the
    // body, bending more as the arm swings forward. Blended in by `amount` and the running blend
    // swingAngle: how far this arm is swung forward right now, in degrees
    void BendElbow(Transform upperArm, Transform forearm, Transform hand, float side, float amount, float swingAngle)
    {
        float weight = amount * runBlend;
        if (weight <= 0f || upperArm == null || forearm == null)
            return;

        Transform facing = hasBody ? fullBody.Torso : body != null ? body.transform : cam;
        float forwardness = Mathf.InverseLerp(-runSwing, runSwing, swingAngle);
        float angle = Mathf.Lerp(runElbowBack, runElbowForward, forwardness);

        Vector3 upper = forearm.position - upperArm.position;
        Vector3 ahead = (facing.forward - facing.right * (side * runInward)).normalized;
        float toAhead = Vector3.Angle(upper, ahead);
        if (toAhead < 1f)
            return;
        Vector3 target = Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.FromToRotation(upper, ahead), angle / toAhead) * upper;

        Quaternion bend = Quaternion.FromToRotation(hand.position - forearm.position, target);
        forearm.rotation = Quaternion.Slerp(Quaternion.identity, bend, weight) * forearm.rotation;
    }

    // Bends the wrist so the hand carries on in the forearm's direction, like a relaxed hanging hand
    // (the idle pose holds the hand bent back), blended in by `amount`
    void StraightenWrist(Transform hand, Transform forearm, Transform knuckle, float amount)
    {
        if (amount <= 0f || forearm == null || knuckle == null)
            return;

        Vector3 handDirection = knuckle.position - hand.position;
        Vector3 armDirection = hand.position - forearm.position;
        Quaternion straighten = Quaternion.FromToRotation(handDirection, armDirection);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, straighten, amount * wristRelax) * hand.rotation;
    }

    static float Ease(float t)
    {
        return Mathf.SmoothStep(0f, 1f, t);
    }

    // ---------- Fingers ----------

    void UpdateFingers(float dt)
    {
        PickupItem held = HeldItem;
        if (held != fingersSolvedFor)
        {
            fingersSolvedFor = held;
            if (held != null)
                SolveFingers(held);
        }

        // Not holding anything: open wide while reaching for an item, otherwise relaxed
        if (held == null)
        {
            bool reaching = grabPhase == GrabPhase.Reach || grabPhase == GrabPhase.Pull;
            for (int i = 0; i < curlTarget.Length; i++)
                curlTarget[i] = reaching ? 0f : isThumb[i] ? restingThumbCurl : restingCurl;
        }

        for (int i = 0; i < fingerJoints.Count; i++)
        {
            curl[i] = Mathf.MoveTowards(curl[i], curlTarget[i], fingerCloseSpeed * dt);
            if (curl[i] > 0f)
                fingerJoints[i].localRotation = Quaternion.Slerp(fingerJoints[i].localRotation, closedRotations[i], curl[i]);
        }
    }

    // Closes each finger one joint at a time, from the knuckle out, stopping each joint just before
    // it would push the finger into the item. Later joints keep curling, so the finger wraps around
    // its shape. A finger already resting on a surface may slide along it but not press into it.
    // Fingers that never touch close all the way.
    void SolveFingers(PickupItem item)
    {
        const int steps = 20;
        for (int f = 0; f + 1 < fingerStart.Count; f++)
        {
            int first = fingerStart[f], end = fingerStart[f + 1];
            Quaternion[] open = new Quaternion[end - first];
            for (int i = first; i < end; i++)
                open[i - first] = fingerJoints[i].localRotation;

            for (int j = first; j < end; j++)
            {
                float best = 0f;
                float limit = Mathf.Min(fingerRadius, FingerClearance(item, j, end, f) - 0.001f);
                for (int s = 1; s <= steps; s++)
                {
                    float c = s / (float)steps;
                    fingerJoints[j].localRotation = Quaternion.Slerp(open[j - first], closedRotations[j], c);
                    if (FingerClearance(item, j, end, f) < limit)
                        break;
                    best = c;
                }
                curlTarget[j] = best;
                fingerJoints[j].localRotation = Quaternion.Slerp(open[j - first], closedRotations[j], best);
            }

            // Put the animated pose back; UpdateFingers blends toward the targets over time
            for (int i = first; i < end; i++)
                fingerJoints[i].localRotation = open[i - first];
        }
    }

    // How close the part of the finger beyond joint `j` comes to the item's surface
    float FingerClearance(PickupItem item, int j, int end, int finger)
    {
        float closest = float.MaxValue;
        for (int i = j + 1; i <= end; i++)
        {
            Vector3 a = fingerJoints[i - 1].position;
            Vector3 b = i < end ? fingerJoints[i].position : fingerTips[finger].position;
            closest = Mathf.Min(closest, item.SurfaceDistance(b), item.SurfaceDistance((a + b) * 0.5f));
        }
        return closest;
    }

    // ---------- Procedural motion ----------

    // The arms lag a little behind the camera when turning, so they feel like they have weight
    Vector3 LookSway(float dt)
    {
        Quaternion delta = Quaternion.Inverse(lastCameraRotation) * cam.rotation;
        lastCameraRotation = cam.rotation;
        Vector3 euler = delta.eulerAngles;
        float yaw = Mathf.DeltaAngle(0f, euler.y);
        float pitch = Mathf.DeltaAngle(0f, euler.x);

        Vector3 target = new Vector3(
            Mathf.Clamp(-yaw * swayAmount, -swayMax, swayMax),
            Mathf.Clamp(pitch * swayAmount, -swayMax, swayMax),
            0f);
        swayPosition = Vector3.Lerp(swayPosition, target, swaySmooth * dt);
        return swayPosition;
    }

    Vector3 WalkBob(float dt)
    {
        if (body == null)
            return Vector3.zero;

        float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        bool grounded = controller == null || controller.Grounded;
        bool sprinting = controller != null && speed > controller.MoveSpeed + 0.5f;

        if (hasBody)
        {
            // Follow the legs: the left foot lands at stride 0, when the right arm is furthest forward
            swingPhase = fullBody.StridePhase * 2f * Mathf.PI + Mathf.PI * 0.5f;
        }
        else if (grounded)
        {
            swingPhase += speed * swingCyclesPerMeter * 2f * Mathf.PI * dt;
        }

        float walkSpeed = controller != null ? controller.MoveSpeed : 4f;
        float target = grounded ? Mathf.Clamp01(speed / walkSpeed) : 0f;
        if (sprinting)
            target *= sprintBobMultiplier;
        bobStrength = Mathf.MoveTowards(bobStrength, target, bobFade * dt);
        sprintWeight = Mathf.MoveTowards(sprintWeight, sprinting && grounded ? 1f : 0f, sprintBlendSpeed * dt);
        runWeight = Mathf.MoveTowards(runWeight, sprinting ? 1f : 0f, runBlendSpeed * dt);
        float lower = sprintLower * Mathf.SmoothStep(0f, 1f, sprintWeight);

        // One side-to-side sway per two steps, one bounce per step, lowest as each foot lands
        // (when the arms are at the ends of their swing)
        return new Vector3(
            Mathf.Sin(swingPhase) * bobSize.x * bobStrength,
            -Mathf.Abs(Mathf.Sin(swingPhase)) * bobSize.y * bobStrength - lower,
            0f);
    }

    // Moves and turns the camera's target with each step, plus a lean when speeding up or slowing down.
    // Steps follow the arm swing (and the legs): a foot lands whenever an arm is at the end of its
    // swing, and when the right arm is forward the left foot is down.
    void UpdateHead(float dt)
    {
        if (cameraRoot == null)
            return;

        float strength = Mathf.Clamp01(bobStrength);
        Vector3 size = Vector3.Lerp(headBobWalk, headBobRun, runBlend) * strength;
        Vector2 tilt = Vector2.Lerp(headTiltWalk, headTiltRun, runBlend) * strength;

        // 0 as a foot lands, 1 as the next one does
        float step = Mathf.Repeat((swingPhase - Mathf.PI * 0.5f) / Mathf.PI, 1f);
        float height = StepHeight(step);
        float side = Mathf.Sin(swingPhase); // +1 when the left foot is planted

        Vector3 bob = new Vector3(
            -side * size.x,                          // over the planted foot
            height * 0.5f * size.y,
            -Mathf.Sin(step * 2f * Mathf.PI) * size.z); // slowest as the foot lands, fastest pushing off
        float nod = -height * 0.5f * tilt.x;          // head drops forward a little with each landing
        float roll = side * tilt.y;                   // tilts toward the planted foot

        UpdateLean(dt);
        UpdateKnees(dt);
        Vector3 leanShift = new Vector3(0f, 0f, lean.x * 0.006f); // leaning forward carries the head forward
        bob.y += knee;
        nod -= knee * landNod;

        Vector3 lookDown = hasBody ? new Vector3(0f, 0f, fullBody.LookDownShift) : Vector3.zero;
        cameraRoot.localPosition = cameraRootRest + bend * bendAmount + bob + leanShift + lookDown;
        if (controller != null)
            controller.CameraTilt = new Vector2(nod + lean.x, roll + lean.y); // the controller applies it
    }

    // Head height through one step, from -1 (lowest) to 1 (highest), with 0 the moment a foot lands.
    // Real steps aren't a smooth wave: the head falls faster and faster into the landing, jolts down a
    // little more as the heel hits, then rises smoothly over the planted leg.
    float StepHeight(float step)
    {
        const float lowest = 0.06f, highest = 0.55f;
        float h;
        if (step >= lowest && step < highest)
            h = Mathf.Lerp(-1f, 1f, Mathf.SmoothStep(0f, 1f, (step - lowest) / (highest - lowest)));
        else
        {
            // falling: from the top, through the landing at 0, to the bottom just after
            float t = Mathf.Repeat(step - highest, 1f) / (1f - highest + lowest);
            h = 1f - 2f * t * t;
        }

        float jolt = step / 0.04f;
        h -= heelStrike * 2f * jolt * Mathf.Exp(1f - jolt) * (step < 0.3f ? 1f : 0f);
        return Mathf.Max(h, -1.5f);
    }

    // Landing bends the knees: the head drops (more for harder landings) and springs back up, with a
    // small forward nod. Pushing off for a jump leaves the head a little behind as the legs extend.
    void UpdateKnees(float dt)
    {
        bool grounded = controller == null || controller.Grounded;
        if (!grounded)
        {
            airTime += dt;
            fallSpeed = Mathf.Max(0f, -velocity.y);
            if (headGrounded && velocity.y > 1f)
                kneeVelocity -= takeoffKick; // jumped
        }
        else if (!headGrounded && airTime >= minAirTime)
        {
            kneeVelocity -= Mathf.Min(fallSpeed * landImpact, maxLandImpact);
        }
        if (grounded)
            airTime = 0f;
        headGrounded = grounded;

        Spring(ref knee, ref kneeVelocity, 0f, kneeStiffness, kneeDamping, dt);
    }

    // Moves value toward target on a damped spring. Solved implicitly, so it stays stable even on
    // long frames (a hitch, or the editor running in the background)
    static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
    {
        velocity = (velocity + (target - value) * stiffness * dt) / (1f + damping * dt + stiffness * dt * dt);
        value += velocity * dt;
    }

    // Speeding up leans the head forward and slowing down rocks it back, on a spring so it
    // overshoots a little and settles. Turning sharply while strafing tilts it sideways.
    void UpdateLean(float dt)
    {
        if (body == null)
            return;

        // The controller turns the player after moving it, so measuring against the player's facing
        // here would read every fast turn as a sudden swerve. The body measures before the turn;
        // without it, use only the change in speed.
        Vector3 local = hasBody
            ? fullBody.LocalVelocity
            : new Vector3(0f, 0f, new Vector3(velocity.x, 0f, velocity.z).magnitude);
        Vector3 rawAcceleration = (local - lastVelocity) / dt;
        lastVelocity = local;
        acceleration = Vector3.Lerp(acceleration, rawAcceleration, 1f - Mathf.Exp(-12f * dt));

        bool grounded = controller == null || controller.Grounded;
        Vector2 target = grounded
            ? new Vector2(
                Mathf.Clamp(acceleration.z * leanPerAcceleration, -maxHeadLean, maxHeadLean),
                Mathf.Clamp(acceleration.x * leanPerAcceleration * 0.5f, -maxHeadLean, maxHeadLean))
            : Vector2.zero;

        Spring(ref lean.x, ref leanVelocity.x, target.x, leanStiffness, leanDamping, dt);
        Spring(ref lean.y, ref leanVelocity.y, target.y, leanStiffness, leanDamping, dt);
    }

    Vector3 Breathing()
    {
        return new Vector3(0f, Mathf.Sin(Time.time * breatheSpeed) * breatheAmount, 0f);
    }

    Vector3 JumpAndLand(float dt)
    {
        if (body == null || controller == null)
            return Vector3.zero;

        bool grounded = controller.Grounded;
        if (grounded && !wasGrounded)
            landTimer = 0f;
        wasGrounded = grounded;

        // Arms trail behind vertical motion: up while rising, down while falling
        float lift = grounded ? 0f : Mathf.Clamp(-velocity.y * airOffsetPerSpeed, -0.06f, 0.06f);
        smoothedLift = Mathf.Lerp(smoothedLift, lift, 10f * dt);

        landTimer += dt;
        float land = landTimer < 0.3f ? -Mathf.Sin(landTimer / 0.3f * Mathf.PI) * landDip : 0f;

        return new Vector3(0f, smoothedLift + land, 0f);
    }

    Vector3 SwitchDip(float dt)
    {
        if (hotbar != null && hotbar.SelectedItem != lastSelected)
        {
            lastSelected = hotbar.SelectedItem;
            if (grabPhase == GrabPhase.None) // a grab plays its own motion
                switchTimer = 0f;
        }

        switchTimer += dt;
        if (switchTimer < switchDuration)
            return new Vector3(0f, -Mathf.Sin(switchTimer / switchDuration * Mathf.PI) * switchDip, 0f);
        return Vector3.zero;
    }

    // ---------- Helpers ----------

    static Transform FindBone(Animator model, string boneName)
    {
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName)
                return t;
        }
        return null;
    }
}
