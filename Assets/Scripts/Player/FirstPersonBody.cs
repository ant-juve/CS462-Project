using StarterAssets;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

// The player's full body (a Mixamo humanoid) under the first-person camera, so looking down shows
// your torso and legs, and the body casts a proper shadow.
//
// Two copies of the model are made at runtime:
//   - a visible one with the head and arms hidden (the camera is in the head, and the arms are
//     drawn by FirstPersonArms), which casts no shadow
//   - a shadow-only one with everything, so the shadow on the floor has a head and arms. Only the
//     visible copy is animated; the shadow copies its body bones (fingers don't show in a shadow).
//     FirstPersonArms then bends the shadow's arms up to match the on-screen hands (ShadowBone)
//
// Legs are animated in code, like the arms: idle, walk, run and strafe clips are blended from the
// player's real speed and direction, and advance by distance travelled (one cycle per stride), so
// feet don't slide at any speed. Walking backward plays the walk in reverse. Jumping follows the
// jump clip by height: pushing off while rising, tucked at the top, legs reaching down while falling,
// then the clip's knee-bending recovery on landing. FirstPersonArms reads StridePhase so the arm swing and
// head bob stay in step with the legs.
//
// Standing still, the feet stay planted: turning your view turns only your head until it's more than
// turnThreshold away from your body, then the body turns quickly to catch up. Looking down tips
// the eyes forward (LookDownShift, applied to the camera by FirstPersonArms) rather than moving
// the body.
//
// Goes on the PlayerCapsule.
[DefaultExecutionOrder(-50)] // pose the body before FirstPersonArms hangs the arms from its shoulders
public class FirstPersonBody : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("A humanoid model (e.g. a Mixamo character imported with Animation Type: Humanoid)")]
    public GameObject model;
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip run;
    public AnimationClip strafeLeft;
    public AnimationClip strafeRight;
    public AnimationClip jump;

    [Header("Clip speeds")]
    [Tooltip("How fast each clip moves at its own size and speed, in meters per second (from the clip's root motion)")]
    public float walkClipSpeed = 1.53f;
    public float runClipSpeed = 3.57f;
    public float strafeClipSpeed = 1.6f;

    [Header("Placement")]
    [Tooltip("How far the body sits behind the eyes, so looking down shows the chest and legs instead of the inside of the body, in meters")]
    public float bodyBack = 0.12f;
    [Tooltip("When you look down your head tips forward from the neck, carrying your eyes out past your chest. How far forward the eyes move when looking straight down, in meters")]
    [FormerlySerializedAs("lookDownBack")]
    public float lookDownForward = 0.2f;
    [Tooltip("Height of the model's eyes above its head bone, as a fraction of the model's height")]
    public float eyeAboveHead = 0.05f;

    [Header("Turning in place")]
    [Tooltip("How far you can turn your head before your body turns to follow, when standing still, in degrees")]
    public float turnThreshold = 50f;
    [Tooltip("How fast the body turns to catch up, in degrees per second")]
    public float turnSpeed = 280f;

    [Header("Blending")]
    [Tooltip("How quickly the leg blend follows changes in speed and direction")]
    public float blendSmoothing = 8f;
    [Tooltip("How quickly the body moves into and out of the jump pose")]
    public float airBlendSpeed = 6f;

    // Where in the stride the legs are: 0-1 is one full cycle (two steps), and the left foot lands at 0
    public float StridePhase { get; private set; }

    // Horizontal velocity relative to the way the player faces. Measured before the controller turns
    // the player for this frame, so looking around doesn't make it look like you sped up or swerved.
    public Vector3 LocalVelocity { get; private set; }

    // How far forward the eyes should move for the current look pitch (tipping the head down), in meters
    public float LookDownShift { get; private set; }

    // Which way the body faces. It can lag behind the view while standing and turning your head
    public Transform Torso => visible.animator.transform;

    // Set up and running. False if scripts were recompiled during Play mode, which drops the
    // runtime setup; restarting Play mode fixes it
    public bool IsReady => visible != null && isActiveAndEnabled;

    // A foot hit the ground while walking or running (the speed, in m/s, is passed along)
    public event System.Action<float> Stepped;
    // Landed after being in the air (the downward speed at impact, in m/s, is passed along)
    public event System.Action<float> Landed;

    private CharacterController body;
    private FirstPersonController controller;
    private Rig visible;
    private Transform shadowRoot;
    private Animator shadowAnimator;
    private Transform[] poseFrom; // visible copy's body bones, and the shadow copy's matching ones
    private Transform[] poseTo;
    private Transform leftShoulder;
    private Transform rightShoulder;

    private float scale = 1f;
    private float restZ;
    private Transform headBone;
    private Vector3 restHead; // where the head bone sits standing, relative to the player, facing the torso's way
    private float torsoYaw;     // world yaw the body faces
    private bool catchingUp;
    private bool movingBack; // playing the leg clips in reverse
    private Transform cameraRoot;
    private float idleTime;
    private float air;            // how far into the jump pose the body is (0-1)
    private float airTime;        // seconds since leaving the ground
    private float takeoffSpeed;   // upward speed at the start of this jump, m/s
    private float landTimer = 10f;
    private float jumpTakeoff, jumpApex, jumpTouchdown; // moments in the jump clip, in seconds
    private const float MinJumpAirTime = 0.12f; // shorter than this is just stepping off a stair
    private Vector3 smoothedVelocity;
    private bool wasGrounded = true;
    private float fallSpeed;
    private float[] strike; // normalized time in each looping clip when the left foot lands

    private const int Idle = 0, Walk = 1, Run = 2, StrafeL = 3, StrafeR = 4, Jump = 5;
    private AnimationClip[] clips;
    private readonly float[] weights = new float[6];
    private readonly float[] times = new float[6];

    void Start()
    {
        body = GetComponent<CharacterController>();
        controller = GetComponent<FirstPersonController>();
        if (model == null || walk == null)
        {
            enabled = false;
            return;
        }

        clips = new[] { idle, walk, run, strafeLeft, strafeRight, jump };

        visible = new Rig(Instantiate(model, transform), clips, "Body");
        GameObject shadowCopy = Instantiate(model, transform);
        shadowCopy.name = "Body (Shadow)";
        shadowAnimator = shadowCopy.GetComponent<Animator>();
        shadowRoot = shadowCopy.transform;

        // Scale the model so its eyes are at the camera's height
        cameraRoot = controller != null && controller.CinemachineCameraTarget != null
            ? controller.CinemachineCameraTarget.transform
            : Camera.main.transform;
        Transform head = visible.Bone(HumanBodyBones.Head);
        float headHeight = transform.InverseTransformPoint(head.position).y;
        float modelHeight = headHeight / 0.9f;
        float eyeHeight = transform.InverseTransformPoint(cameraRoot.position).y;
        scale = eyeHeight / (headHeight + eyeAboveHead * modelHeight);
        float headForward = transform.InverseTransformPoint(head.position).z * scale;

        restZ = -headForward - bodyBack;
        headBone = head;
        restHead = new Vector3(0f, headHeight * scale, -bodyBack);
        foreach (Transform t in new[] { visible.animator.transform, shadowRoot })
        {
            t.localScale = Vector3.one * scale;
            t.localRotation = Quaternion.identity;
            t.localPosition = new Vector3(0f, 0f, restZ);
        }

        foreach (Renderer r in visible.animator.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Hair and eyelashes belong to the hidden head
            if (r.name.Contains("Hair") || r.name.Contains("Eyelash"))
                r.enabled = false;
        }
        foreach (Renderer r in shadowCopy.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            if (r.name.Contains("Eyelash"))
                r.enabled = false; // too small to show in a shadow
        }

        // The shadow copy just mirrors the visible one's body bones
        shadowAnimator.enabled = false;
        var from = new System.Collections.Generic.List<Transform>();
        var to = new System.Collections.Generic.List<Transform>();
        for (HumanBodyBones b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
        {
            if (b >= HumanBodyBones.LeftThumbProximal && b <= HumanBodyBones.RightLittleDistal)
                continue; // fingers
            Transform a = visible.Bone(b), c = shadowAnimator.GetBoneTransform(b);
            if (a != null && c != null)
            {
                from.Add(a);
                to.Add(c);
            }
        }
        poseFrom = from.ToArray();
        poseTo = to.ToArray();

        // Both copies stay around the player, so a fixed box is enough for their bounds
        // (cheaper than recomputing them from the bones every frame)
        foreach (Transform root in new[] { visible.animator.transform, shadowRoot })
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Transform space = r.rootBone != null ? r.rootBone : r.transform;
                float toLocal = 1f / Mathf.Max(0.0001f, space.lossyScale.x);
                r.localBounds = new Bounds(space.InverseTransformPoint(transform.position + transform.up * 1f), Vector3.one * 2.6f * toLocal);
                r.updateWhenOffscreen = false;
            }

        leftShoulder = visible.Bone(HumanBodyBones.LeftUpperArm);
        rightShoulder = visible.Bone(HumanBodyBones.RightUpperArm);

        MeasureClips();
        HideHeadAndArms(); // animation doesn't touch bone scale, so once is enough

        torsoYaw = transform.eulerAngles.y;
        gameObject.AddComponent<FirstPersonBodyPose>().body = this;

        // The capsule placeholder isn't needed once there's a real body
        Transform capsule = transform.Find("Capsule");
        if (capsule != null && capsule.TryGetComponent(out Renderer capsuleRenderer))
            capsuleRenderer.enabled = false;
    }

    void OnDestroy()
    {
        visible?.Destroy();
    }

    // World position of the body's shoulder joint. side: +1 right, -1 left
    public Vector3 ShoulderPosition(float side)
    {
        return (side > 0f ? rightShoulder : leftShoulder).position;
    }

    // A bone of the shadow-only copy, for scripts that run after the body is posed each frame to
    // adjust what the shadow shows
    public Transform ShadowBone(HumanBodyBones bone)
    {
        return shadowAnimator != null ? shadowAnimator.GetBoneTransform(bone) : null;
    }

    // Runs before the controller turns the player for this frame, so the velocity is measured
    // against the facing it was moved with
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || visible == null)
            return;

        UpdateTorso(dt);
        UpdateBlend(dt);
        if (controller != null)
            LookDownShift = lookDownForward * Mathf.Clamp01(Mathf.Sin(controller.CameraPitch * Mathf.Deg2Rad));
    }

    // Runs after the controller has turned the player (FirstPersonBodyPose), so the body is placed
    // against this frame's facing, and before FirstPersonArms hangs the arms from its shoulders
    internal void PoseAndPlace()
    {
        if (Time.deltaTime <= 0f || visible == null)
            return;

        // Keep the body facing its own way, and keep it where it stands while the player turns
        Quaternion offset = Quaternion.Euler(0f, Mathf.DeltaAngle(transform.eulerAngles.y, torsoYaw), 0f);
        Vector3 position = offset * new Vector3(0f, 0f, restZ);
        visible.animator.transform.SetLocalPositionAndRotation(position, offset);
        shadowRoot.SetLocalPositionAndRotation(position, offset);

        visible.Pose(weights, times);
        Vector3 placed = position + offset * HeadCorrection(offset);
        visible.animator.transform.localPosition = placed;
        shadowRoot.localPosition = placed;
        for (int i = 0; i < poseFrom.Length; i++)
            poseTo[i].SetLocalPositionAndRotation(poseFrom[i].localPosition, poseFrom[i].localRotation);
    }

    // The jump clip lifts the whole body off the ground and throws the chest forward, but the player
    // capsule already does the real jumping, so the clip's body would rise a second time and bring
    // the neck and shoulders up into the first-person view. This works out how far to move the body
    // so the head never rises above, or (while jumping) moves ahead of, where it sits standing.
    // Facing the torso's way; `offset` is the torso's turn from the player's facing.
    Vector3 HeadCorrection(Quaternion offset)
    {
        if (headBone == null)
            return Vector3.zero;

        Vector3 head = Quaternion.Inverse(offset) * transform.InverseTransformPoint(headBone.position);
        float down = Mathf.Min(0f, restHead.y - head.y);
        float back = Mathf.Min(0f, restHead.z - head.z) * weights[Jump]; // walking and running keep their lean
        return new Vector3(0f, down, back);
    }

    // Moving: the body faces where you're looking. Standing: it stays put until your head has turned
    // too far from it, then turns to catch up
    void UpdateTorso(float dt)
    {
        float viewYaw = transform.eulerAngles.y;
        float behind = Mathf.Abs(Mathf.DeltaAngle(torsoYaw, viewYaw));
        bool moving = LocalVelocity.sqrMagnitude > 0.2f * 0.2f;

        if (moving)
        {
            catchingUp = false;
            torsoYaw = Mathf.MoveTowardsAngle(torsoYaw, viewYaw, 720f * dt);
        }
        else
        {
            if (behind > turnThreshold)
                catchingUp = true;
            if (catchingUp)
            {
                torsoYaw = Mathf.MoveTowardsAngle(torsoYaw, viewYaw, turnSpeed * dt);
                if (Mathf.Abs(Mathf.DeltaAngle(torsoYaw, viewYaw)) < 2f)
                    catchingUp = false;
            }
        }
    }

    void UpdateBlend(float dt)
    {
        Vector3 worldVelocity = body.velocity;
        Vector3 velocity = transform.InverseTransformDirection(worldVelocity);
        velocity.y = 0f;
        LocalVelocity = velocity;

        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-blendSmoothing * dt));
        Vector3 v = smoothedVelocity;
        float speed = v.magnitude;
        bool grounded = controller == null || controller.Grounded;

        float walkSpeed = walkClipSpeed * scale;
        float runSpeed = runClipSpeed * scale;
        float forward = Mathf.Abs(v.z), side = Mathf.Abs(v.x);
        float forwardShare = forward + side > 0.001f ? forward / (forward + side) : 1f;
        float sideShare = 1f - forwardShare;

        float moving = Mathf.SmoothStep(0f, 1f, speed / 0.6f);
        float running = Mathf.Clamp01((speed - walkSpeed) / (runSpeed - walkSpeed));

        // One stride (full cycle, two steps) covers this much ground
        float walkStride = walkClipSpeed * walk.length * scale;
        float runStride = runClipSpeed * run.length * scale;
        float strafeStride = strafeClipSpeed * strafeLeft.length * scale;
        float stride = forwardShare * Mathf.Lerp(walkStride, runStride, running) + sideShare * strafeStride;
        float before = StridePhase;
        // Blending a forward step with a side-step shortens the combined step (to ~71% at 45 degrees),
        // so pace the cycle by forward + sideways speed, which makes up exactly that difference and
        // keeps the planted foot still on diagonals. Straight lines are unaffected.
        if (grounded && stride > 0.01f)
        {
            float rate = (forward + side) / stride; // strides per second
            // There's no sprinting side-step, only a walking one, so sprinting sideways would play it
            // several times faster than it was made for and the legs (and arms, which follow them)
            // turn into a blur. Sprinting in any direction steps at the forward sprint's pace instead:
            // longer side-steps with a little slide
            float sprinting = controller != null && controller.SprintSpeed > controller.MoveSpeed
                ? Mathf.Clamp01((speed - controller.MoveSpeed) / (controller.SprintSpeed - controller.MoveSpeed))
                : 0f;
            float forwardSprintRate = speed / Mathf.Max(runStride, 0.01f);
            rate = Mathf.Lerp(rate, forwardSprintRate, sprinting);
            StridePhase = Mathf.Repeat(StridePhase + rate * dt, 1f);
        }

        // A foot lands at stride 0 (left) and 0.5 (right)
        if (grounded && moving > 0.5f && (CrossedStep(before, StridePhase, 0f) || CrossedStep(before, StridePhase, 0.5f)))
            Stepped?.Invoke(speed);

        if (!grounded)
            fallSpeed = Mathf.Max(0f, -worldVelocity.y);
        if (grounded && !wasGrounded)
            Landed?.Invoke(fallSpeed);
        wasGrounded = grounded;
        idleTime += dt;

        // Jump: follow the clip by vertical speed while in the air, then play its landing
        float jumpWeight, jumpTime;
        if (!grounded)
        {
            if (airTime == 0f)
                takeoffSpeed = 0f;
            airTime += dt;
            takeoffSpeed = Mathf.Max(takeoffSpeed, worldVelocity.y);
            bool jumped = takeoffSpeed > 1f;
            air = Mathf.MoveTowards(air, jumped || airTime > MinJumpAirTime ? 1f : 0f, airBlendSpeed * dt);
            jumpWeight = air;
            jumpTime = worldVelocity.y > 0f && jumped
                ? Mathf.Lerp(jumpTakeoff, jumpApex, 1f - worldVelocity.y / takeoffSpeed)
                : Mathf.Lerp(jumpApex, jumpTouchdown, Mathf.Clamp01(-worldVelocity.y / Mathf.Max(takeoffSpeed, 3f)));
        }
        else
        {
            if (airTime > MinJumpAirTime)
                landTimer = 0f; // just landed from a real jump or fall
            airTime = 0f;
            air = 0f;
            landTimer += dt;
            float landDuration = Mathf.Min(jump.length - jumpTouchdown, 0.4f);
            float recovered = Mathf.SmoothStep(0f, 1f, landTimer / landDuration);
            jumpWeight = (1f - recovered) * (1f - 0.5f * moving); // moving on shortens the crouch
            jumpTime = Mathf.Min(jumpTouchdown + landTimer, jump.length);
        }
        float ground = 1f - jumpWeight;

        weights[Idle] = (1f - moving) * ground;
        weights[Walk] = moving * forwardShare * (1f - running) * ground;
        weights[Run] = moving * forwardShare * running * ground;
        // Any backward movement plays every leg clip in reverse. A side-step played in reverse moves
        // the other way, so going back-left uses the right side-step reversed (and back-right the
        // left one); that keeps both clips stepping in the same order, so they blend cleanly.
        // A small dead zone stops it flipping back and forth when moving straight sideways.
        if (v.z < -0.15f) movingBack = true;
        else if (v.z > -0.05f) movingBack = false; // straight sideways plays the side-steps forward
        bool left = v.x < 0f;
        int strafe = movingBack == left ? StrafeR : StrafeL;
        weights[StrafeL] = strafe == StrafeL ? moving * sideShare * ground : 0f;
        weights[StrafeR] = strafe == StrafeR ? moving * sideShare * ground : 0f;
        weights[Jump] = jumpWeight;

        float phase = movingBack ? -StridePhase : StridePhase;
        times[Idle] = Mathf.Repeat(idleTime, idle.length);
        times[Walk] = ClipTime(Walk, phase);
        times[Run] = ClipTime(Run, phase);
        times[StrafeL] = ClipTime(StrafeL, phase);
        times[StrafeR] = ClipTime(StrafeR, phase);
        times[Jump] = jumpTime;
    }

    static bool CrossedStep(float before, float after, float at)
    {
        if (after < before)
            after += 1f; // wrapped around
        return (before < at && after >= at) || (before < at + 1f && after >= at + 1f);
    }

    float ClipTime(int index, float phase)
    {
        return Mathf.Repeat(strike[index] + phase, 1f) * clips[index].length;
    }

    void HideHeadAndArms()
    {
        Vector3 hidden = Vector3.one * 0.0001f;
        visible.Bone(HumanBodyBones.Head).localScale = hidden;
        visible.Bone(HumanBodyBones.LeftUpperArm).localScale = hidden;
        visible.Bone(HumanBodyBones.RightUpperArm).localScale = hidden;
    }

    // Samples the clips once to line them up. Walk and run: where in the stride the left foot lands
    // (most forward). Side-steps don't move the feet forward, so they're lined up with the walk by
    // when the left foot is planted instead. Also finds the moment in the jump when the feet are highest.
    void MeasureClips()
    {
        strike = new float[clips.Length];
        Transform root = visible.animator.transform;
        Transform leftFoot = visible.Bone(HumanBodyBones.LeftFoot);
        Transform hips = visible.Bone(HumanBodyBones.Hips);
        const int samples = 60;

        for (int c = Walk; c <= Run; c++)
        {
            float bestZ = float.MinValue;
            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)samples;
                visible.Sample(c, t * clips[c].length);
                float z = root.InverseTransformPoint(leftFoot.position).z - root.InverseTransformPoint(hips.position).z;
                if (z > bestZ)
                {
                    bestZ = z;
                    strike[c] = t;
                }
            }
        }

        // How long after the walk's left foot lands it's halfway through being planted
        float plantedAfterStrike = Mathf.Repeat(MidStance(Walk, samples) - strike[Walk], 1f);
        for (int c = StrafeL; c <= StrafeR; c++)
            strike[c] = Mathf.Repeat(MidStance(c, samples) - plantedAfterStrike, 1f);

        if (jump != null)
            MeasureJump(root, leftFoot, visible.Bone(HumanBodyBones.RightFoot));
    }

    // Finds when the feet leave the ground, when they're highest, and when they touch down again
    void MeasureJump(Transform root, Transform leftFoot, Transform rightFoot)
    {
        const int samples = 120;
        float[] height = new float[samples];
        float lowest = float.MaxValue;
        int apex = 0;
        for (int s = 0; s < samples; s++)
        {
            visible.Sample(Jump, s / (float)samples * jump.length);
            height[s] = Mathf.Min(root.InverseTransformPoint(leftFoot.position).y, root.InverseTransformPoint(rightFoot.position).y);
            lowest = Mathf.Min(lowest, height[s]);
            if (height[s] > height[apex])
                apex = s;
        }

        float offGround = lowest + 0.03f;
        int takeoff = apex, touchdown = apex;
        while (takeoff > 0 && height[takeoff - 1] > offGround) takeoff--;
        while (touchdown < samples - 1 && height[touchdown + 1] > offGround) touchdown++;

        float step = jump.length / samples;
        jumpTakeoff = takeoff * step;
        jumpApex = apex * step;
        jumpTouchdown = (touchdown + 1) * step;
    }

    // Middle of the time the left foot is down (within 1cm of its lowest), as a fraction of the clip
    float MidStance(int clip, int samples)
    {
        Transform root = visible.animator.transform;
        Transform leftFoot = visible.Bone(HumanBodyBones.LeftFoot);
        float[] height = new float[samples];
        float lowest = float.MaxValue;
        for (int s = 0; s < samples; s++)
        {
            visible.Sample(clip, s / (float)samples * clips[clip].length);
            height[s] = root.InverseTransformPoint(leftFoot.position).y;
            lowest = Mathf.Min(lowest, height[s]);
        }

        // Average the planted samples around the cycle (it can wrap past the end of the clip)
        Vector2 sum = Vector2.zero;
        for (int s = 0; s < samples; s++)
            if (height[s] < lowest + 0.01f)
            {
                float angle = s / (float)samples * 2f * Mathf.PI;
                sum += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        return Mathf.Repeat(Mathf.Atan2(sum.y, sum.x) / (2f * Mathf.PI), 1f);
    }

    // One copy of the model and the playable graph that poses it
    class Rig
    {
        public readonly Animator animator;
        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] playables;

        public Rig(GameObject instance, AnimationClip[] clips, string name)
        {
            instance.name = name;
            animator = instance.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("FirstPersonBody " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, name, animator);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            playables = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;
                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                playables[i].SetApplyFootIK(true);
                graph.Connect(playables[i], 0, mixer, i);
            }
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        public void Pose(float[] weights, float[] times)
        {
            for (int i = 0; i < playables.Length; i++)
            {
                if (!playables[i].IsValid())
                    continue;
                mixer.SetInputWeight(i, weights[i]);
                if (weights[i] > 0f)
                    playables[i].SetTime(times[i]);
            }
            graph.Evaluate(0f);
        }

        // Poses the model with a single clip at a given time
        public void Sample(int index, float time)
        {
            float[] w = new float[playables.Length];
            float[] t = new float[playables.Length];
            w[index] = 1f;
            t[index] = time;
            Pose(w, t);
        }

        public void Destroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }
    }
}

// Second half of FirstPersonBody's frame, at a later point in the frame (see PoseAndPlace)
[DefaultExecutionOrder(50)]
public class FirstPersonBodyPose : MonoBehaviour
{
    internal FirstPersonBody body;

    void LateUpdate()
    {
        if (body != null && body.isActiveAndEnabled)
            body.PoseAndPlace();
    }
}
