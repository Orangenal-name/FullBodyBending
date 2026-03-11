using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppPhoton.Realtime;
using Il2CppRootMotion.FinalIK;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.BootLoader;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using UnityEngine;
using Valve.OpenVR;
using static RumbleModdingAPI.RMAPI.GameObjects.Gym.INTERACTABLES.DressingRoom.PreviewPlayerController.Visuals;
using Player = Il2CppRUMBLE.Players.Player;

[assembly: MelonInfo(typeof(FullBodyBending.Core), "FullBodyBending", "1.0.0", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace FullBodyBending
{
    public class Core : Utilities.RumbleMod
    {
        internal static MelonLogger.Instance loggerInstance;

        internal static List<GameObject> spheres;
        public static List<uint> trackerIndices = new List<uint>();
        internal static CVRSystem system;
        private static TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        internal static float debugTrackerSize = 0.15f;

        public override void OnInitializeMelon()
        {
            var error = EVRInitError.None;
            system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

            if (error != EVRInitError.None)
            {
                MelonLogger.Error($"OpenVR Init failed: {error}");
                return;
            }
            Actions.onMapInitialized += OnSceneWasLoaded;

            loggerInstance = LoggerInstance;

            LoggerInstance.Msg("Initialised.");
        }

        public void OnSceneWasLoaded(string sceneName)
        {
            TrackerManager.InitLocalTrackers();
        }

        public override void OnUpdate()
        {
            if (system == null || TrackerManager.trackers.Count == 0) return;
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);

            foreach (OpenVRTracker tracker in TrackerManager.trackers.Values)
            {
                uint trackerIndex = tracker.trackerIndex;
                if (poses[trackerIndex].bDeviceIsConnected && poses[trackerIndex].bPoseIsValid)
                {
                    TrackedDevicePose_t pose = poses[tracker.trackerIndex];
                    tracker.UpdateTransform(pose.mDeviceToAbsoluteTracking);
                }
            }
        }
    }

    public static class TrackerManager
    {
        public static readonly string[] supportedTrackers = ["waist", "chest", "right_foot", "left_foot", "right_knee", "left_knee", "right_elbow", "left_elbow"];

        public static Dictionary<string, OpenVRTracker> trackers = new();

        public static Dictionary<string, int[]> skeletonPaths = new() // Paths are child indexes after the pelvis bone
        {
            { "chest", [4, 0] },
            { "right_foot",  [ 2, 0, 0 ] },
            { "left_foot",   [ 2, 0, 0 ] },
            { "right_knee",  [ 3, 0 ] },
            { "left_knee",   [ 2, 0 ] },
            { "right_elbow", [ 4, 0, 2, 0, 0 ] },
            { "left_elbow",  [ 4, 0, 1, 0, 0 ] },
        }; // TODO: Re-implement bootloader trackers

        public static void InitLocalTrackers()
        {
            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            Core.system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
            for (uint i = 0; i < poses.Length; i++)
            {
                if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    GameObject trackerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    OpenVRTracker tracker = trackerObject.AddComponent<OpenVRTracker>();
                    tracker.player = Calls.Players.GetLocalPlayer();
                    tracker.trackerIndex = i;
                }
            }
        }

        public static Transform GetBone(string trackerName, Transform skeleton)
        {
            Transform bone = skeleton.GetChild(0);

            if (TrackerManager.skeletonPaths.ContainsKey(trackerName))
            {
                int[] path = TrackerManager.skeletonPaths[trackerName];

                foreach (int child in path)
                {
                    bone = bone.GetChild(child);
                }
            }

            return bone;
        }

        /* Every update:
         * TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
         * OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
         * foreach (var tracker in trackerObjects)
         * {
         *      tracker.UpdatePose(poses);
         * }
         */
    }

    [RegisterTypeInIl2Cpp]
    public class OpenVRTracker : MonoBehaviour
    {
        // Need to be set when component is added
        public uint trackerIndex;
        public Player player;
        public Transform originalBone;
        public bool isVisible = true;

        // Set in Start()
        public string controllerType;
        public string trackerType;
        public string trackerName;
        public Transform IKTarget;

        public bool offsetsSet = false;
        public (Vector3, Quaternion) offsets;

        public OpenVRTracker(IntPtr ptr) : base(ptr) { }

        public void UpdateTransform(HmdMatrix34_t matrix)
        {
            Vector3 position = new Vector3(matrix.m3, matrix.m7, -matrix.m11);
            transform.localPosition = position;

            Vector3 forward = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
            Vector3 up = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

            transform.localRotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0, 225, 0);
        }

        // Allows for individual trackers to be calibrated
        public void Calibrate()
        {
            Transform compareSkelington = GameObject.Instantiate(Resources.FindObjectsOfTypeAll<PlayerController>().First().PlayerVisuals.transform.GetChild(1).gameObject).transform;

            Calibrate(compareSkelington);

            Destroy(compareSkelington.gameObject);
        }

        // Allows for the same skeleton to be compared against multiple times when calibrating multiple trackers
        public void Calibrate(Transform compareSkelington)
        {
            Transform activeSkeleton = player.Controller.PlayerVisuals.transform.GetChild(1);
            compareSkelington.position = activeSkeleton.position;
            Transform compareBone = TrackerManager.GetBone(trackerName, compareSkelington);

            Vector3 posOffset = compareBone.position - transform.position;
            Quaternion rotOffset = compareBone.rotation * Quaternion.Inverse(transform.rotation);

            offsets = (posOffset, rotOffset);
            offsetsSet = true;
        }

        private void Elbow(string side, IKSolverVR solver, bool unassign)
        {
            IKSolverVR.Arm arm = side == "left" ? solver.leftArm : solver.rightArm;
            arm.bendGoal = unassign ? null : IKTarget;
            arm.bendGoalWeight = unassign ? 0 : 1;
        }
        private void Foot(string side, IKSolverVR solver, bool unassign)
        {
            IKSolverVR.Leg leg = side == "left" ? solver.leftLeg : solver.rightLeg;
            leg.target = unassign ? null : IKTarget;
            leg.positionWeight = unassign ? 0 : 1;
            leg.rotationWeight = unassign ? 0 : 1;
        }
        private void Knee(string side, IKSolverVR solver, bool unassign)
        {
            IKSolverVR.Leg leg = side == "left" ? solver.leftLeg : solver.rightLeg;
            leg.bendGoal = unassign ? null : IKTarget;
            leg.bendGoalWeight = unassign ? 0 : 1;
        }

        public void AssignIK(bool unassign = false)
        {
            PlayerController playerController = player.Controller;
            GameObject VisualsGO = playerController.PlayerVisuals.gameObject;
            IKSolverVR solver = VisualsGO.GetComponent<VRIK>().solver;
            string side = trackerName.Split("_")[0];

            

            switch (trackerType)
            {
                case "chest":
                    solver.spine.chestGoal = unassign ? null : IKTarget;
                    solver.spine.chestGoalWeight = unassign ? 0 : 1;
                    break;
                case "waist":
                    solver.spine.pelvisTarget = unassign ? null : IKTarget;
                    solver.spine.pelvisPositionWeight = unassign ? 0 : 1;
                    solver.spine.pelvisRotationWeight = unassign ? 0 : 1;
                    break;
                case "foot":
                    Foot(side, solver, unassign);
                    break;
                case "knee":
                    Knee(side, solver, unassign);
                    break;
                case "elbow":
                    Elbow(side, solver, unassign);
                    break;
            }

            IKTarget.localPosition = Vector3.zero;
            IKTarget.localRotation = Quaternion.Euler(Vector3.zero);

            if (!offsetsSet) return;

            IKTarget.localPosition = offsets.Item1;
            IKTarget.localRotation = offsets.Item2;
        }

        private void Start()
        {
            transform.SetParent(player.Controller.PlayerVR.transform);
            transform.localPosition = Vector3.zero;
            Destroy(GetComponent<SphereCollider>());
            if (isVisible)
            {
                GetComponent<MeshRenderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                transform.localScale = new Vector3(Core.debugTrackerSize, Core.debugTrackerSize, Core.debugTrackerSize);
            }
            else Destroy(GetComponent<MeshRenderer>());

            System.Text.StringBuilder sb = new(64);
            ETrackedPropertyError error = new();
            OpenVR.System.GetStringTrackedDeviceProperty(
                trackerIndex,
                ETrackedDeviceProperty.Prop_ControllerType_String,
                sb, 64, ref error
            );

            controllerType = sb.ToString();
            gameObject.name = controllerType;

            if (controllerType.EndsWith("chest") || controllerType.EndsWith("waist"))
            {
                trackerName = controllerType.Split("_").Last();
                trackerType = trackerName;
            }
            else
            {
                string[] splitType = controllerType.Split("_");
                trackerName = splitType[^2] + "_" + splitType[^1]; // CARATS!? IN MY INDEX!?!?!?
                trackerType = splitType[^1];
            }

            if (!TrackerManager.supportedTrackers.Contains(trackerName))
            {
                if (trackerName != "liv_virtualcamera")
                {
                    Core.loggerInstance.Warning($"Unrecognised tracker: {controllerType}");
                }
                Destroy(gameObject); // We <3 self-immolation
            }

            originalBone = TrackerManager.GetBone(trackerName, player.Controller.PlayerVisuals.transform.GetChild(1));

            IKTarget = Instantiate(originalBone.gameObject).transform;
            IKTarget.SetParent(transform);

            // TODO: remove this once calibration is in
            offsets.Item1 = Vector3.zero;
            offsets.Item2 = Quaternion.Euler(Vector3.zero);
            offsetsSet = true;

            if (offsetsSet)
            {
                AssignIK();
            }

            TrackerManager.trackers.Add(trackerName, this);
        }

        private void OnDestroy()
        {
            TrackerManager.trackers.Remove(trackerName);
        }
    }

    /*[HarmonyPatch(typeof(BootLoaderPlayer), nameof(BootLoaderPlayer.Start))]
    public static class LoaderStartPatch
    {
        private static void Postfix(ref BootLoaderPlayer __instance)
        {
            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            Core.system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
            for (uint i = 0; i < poses.Length; i++)
            {
                if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    var id = new System.Text.StringBuilder(64);
                    ETrackedPropertyError propError = new();

                    OpenVR.System.GetStringTrackedDeviceProperty(
                        i,
                        ETrackedDeviceProperty.Prop_ControllerType_String,
                        id, 64, ref propError
                    );

                    int viewID = Core.SpawnTracker(__instance, id.ToString(), true, debug: true);

                    Core.trackerIndices.Add(i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetMeasurement))]
    public static class TPosePatch
    {
        private static void Postfix(ref PlayerData __instance)
        {
            MelonLogger.Msg("Calibrating FBT");

            Transform compareSkelington = GameObject.Instantiate(Resources.FindObjectsOfTypeAll<PlayerController>().First().PlayerVisuals.transform.GetChild(1).gameObject).transform;

            foreach (OpenVRTracker tracker in TrackerManager.trackers.Values)
            {
                tracker.Calibrate(compareSkelington);
            }

            GameObject.Destroy(compareSkelington.gameObject);
        }
    }*/
}