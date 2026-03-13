using HarmonyLib;
using Il2CppExitGames.Client.Photon;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using Il2CppRootMotion.FinalIK;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.BootLoader;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using System.Collections;
using UnityEngine;
using Valve.OpenVR;

[assembly: MelonInfo(typeof(FullBodyBending.Core), "FullBodyBending", "1.0.0", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace FullBodyBending
{
    public class Core : Utilities.RumbleMod
    {
        internal static MelonLogger.Instance loggerInstance;

        internal static CVRSystem system;
        private static TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        internal static RaiseEventOptions REOptions = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };

        internal static float debugTrackerSize = 0.15f;
        public string thingy = "thingy";

        public override void OnInitializeMelon()
        {
            var error = EVRInitError.None;
            system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

            if (error != EVRInitError.None)
            {
                loggerInstance.Error($"OpenVR Init failed: {error}");
                return;
            }
            Actions.onMapInitialized += OnSceneWasLoaded;

            loggerInstance = LoggerInstance;

            LoggerInstance.Msg("Initialised.");
        }

        public override void OnEvent(List<Il2CppSystem.Object> data)
        {
            MelonCoroutines.Start(DelayedEvent(data));
        }

        private IEnumerator DelayedEvent(List<Il2CppSystem.Object> data)
        {
            int timeout = 1000;
            while (Calls.Players.GetAllPlayers().Count <= 1 && timeout > 0)
            {
                timeout--; // We don't really want this running in the background forever if it fails for some reason
                yield return null;
            }
            if (Calls.Players.GetAllPlayers().Count <= 1)
            {
                loggerInstance.Error("No players found");
                yield break;
            }
            TrackerManager.InitRemoteTrackers(data);
            yield break;
        }

        public void OnSceneWasLoaded(string sceneName)
        {
            TrackerManager.initCount = 0;
            RegisterEvents();

            if (TrackerManager.trackerCount > 0)
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

    public class TrackerManager
    {
        public static string[] supportedTrackers = ["waist", "chest", "right_foot", "left_foot", "right_knee", "left_knee", "right_elbow", "left_elbow"];

        public static Dictionary<string, OpenVRTracker> trackers = new();
        public static Dictionary<string, (Vector3, Quaternion)> storedOffsets = new();

        public static int trackerCount = 0;

        public static int initCount = 0; // Tracks how many trackers have yet to run their Start() method

        public static Dictionary<string, int[]> skeletonPaths = new() // Paths are child indexes after the pelvis bone
        {
            { "chest", [4, 0] },
            { "right_foot",  [ 3, 0, 0 ] },
            { "left_foot",   [ 2, 0, 0 ] },
            { "right_knee",  [ 3, 0 ] },
            { "left_knee",   [ 2, 0 ] },
            { "right_elbow", [ 4, 0, 2, 0, 0 ] },
            { "left_elbow",  [ 4, 0, 1, 0, 0 ] },
        };

        public static void InitLocalTrackers()
        { // TODO: Add a check for if the user actually has trackers lmao
            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            Core.system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
            bool inGym = Calls.Scene.GetSceneName() == "Gym";

            for (uint i = 0; i < poses.Length; i++)
            {
                if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    GameObject trackerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    OpenVRTracker tracker = trackerObject.AddComponent<OpenVRTracker>();
                    tracker.playerController = Calls.Players.GetLocalPlayer().Controller;
                    tracker.trackerIndex = i;

                    initCount++;
                }
            }

            if (inGym) return;
            MelonCoroutines.Start(NetworkTrackers());
        }

        public static void InitRemoteTrackers(List<Il2CppSystem.Object> data)
        {
            string strData = data[0].ToString();
            string[] trackers = strData.Split("|");

            foreach (string strTracker in trackers)
            {
                string[] trackerData = strTracker.Split("/");
                string trackerName = trackerData[0];
                int viewID = int.Parse(trackerData[1]);
                int ownerActorNo = int.Parse(trackerData[2]);

                PlayerController ownerController = Calls.Players.GetPlayerByActorNo(ownerActorNo).Controller;
                Transform skelington = ownerController.PlayerVisuals.transform.GetChild(1);

                GameObject trackerObject = new GameObject(trackerName);
                trackerObject.transform.SetParent(ownerController.PlayerVR.transform);

                Transform IKTarget = GameObject.Instantiate(GetBone(trackerName, skelington));

                AssignRemoteIK(ownerController, trackerName, IKTarget);
            }
        }

        private static IEnumerator NetworkTrackers()
        {
            int timeout = 5;
            while (initCount > 0 && timeout > 0)
            {
                timeout--;
                yield return null; // This should really only take one frame, but just in case ¯\_(ツ)_/¯
            }
            if (initCount > 0)
            {
                Core.loggerInstance.Error("Not all trackers initialised properly!");
                yield break;
            }
            else if (trackers.Count == 0)
            {
                yield break;
            }

            Core coreInstance = (Core)MelonMod.RegisteredMelons.Where(mod => mod.Info.Name == "FullBodyBending").First();
            List<Il2CppSystem.Object> data = new();

            string trackerData = "";
            foreach (OpenVRTracker tracker in trackers.Values.ToList())
            {
                trackerData += $"|{tracker.trackerName}";
                trackerData += $"/{tracker.viewID}";
                trackerData += $"/{tracker.IKTarget.GetComponent<PhotonView>().ownerActorNr}";
            }
            trackerData = trackerData[1..^0]; // Remove leading pipe
            data.Add(trackerData);

            coreInstance.RaiseEvent(data, Core.REOptions, SendOptions.SendReliable);

            yield break;
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

        private static void Elbow(string side, IKSolverVR solver, Transform IKTarget, bool unassign)
        {
            IKSolverVR.Arm arm = side == "left" ? solver.leftArm : solver.rightArm;
            arm.bendGoal = unassign ? null : IKTarget;
            arm.bendGoalWeight = unassign ? 0 : 1;
        }
        private static void Foot(string side, IKSolverVR solver, Transform IKTarget, bool unassign)
        {
            IKSolverVR.Leg leg = side == "left" ? solver.leftLeg : solver.rightLeg;
            leg.target = unassign ? null : IKTarget;
            leg.positionWeight = unassign ? 0 : 1;
            leg.rotationWeight = unassign ? 0 : 1;
        }
        private static void Knee(string side, IKSolverVR solver, Transform IKTarget, bool unassign)
        {
            IKSolverVR.Leg leg = side == "left" ? solver.leftLeg : solver.rightLeg;
            leg.bendGoal = unassign ? null : IKTarget;
            leg.bendGoalWeight = unassign ? 0 : 1;
        }

        private static void AssignRemoteIK(PlayerController playerController, string trackerName, Transform IKTarget, bool unassign = false)
        {
            GameObject VisualsGO = playerController.PlayerVisuals.gameObject;
            IKSolverVR solver = VisualsGO.GetComponent<VRIK>().solver;
            string side = trackerName.Split("_")[0];
            string trackerType = side == "left" || side == "right" ? trackerName.Split("_")[1] : trackerName;


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
                    Foot(side, solver, IKTarget, unassign);
                    break;
                case "knee":
                    Knee(side, solver, IKTarget, unassign);
                    break;
                case "elbow":
                    Elbow(side, solver, IKTarget, unassign);
                    break;
            }
        }
    }

    [RegisterTypeInIl2Cpp]
    public class OpenVRTracker : MonoBehaviour
    {
        // Need to be set when component is added
        public uint trackerIndex;
        public PlayerController playerController;
        public Transform originalBone;
        public bool isVisible = true;

        // Set in Start()
        public string controllerType;
        public string trackerType;
        public string trackerName;
        public Transform IKTarget;
        public int viewID;

        // Set elsewhere locally
        private bool offsetsSet = false;
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
            Transform activeSkeleton = playerController.PlayerVisuals.transform.GetChild(1);
            compareSkelington.position = activeSkeleton.position;
            Transform compareBone = TrackerManager.GetBone(trackerName, compareSkelington);

            Vector3 posOffset = compareBone.localPosition - transform.localPosition;
            Quaternion rotOffset = compareBone.localRotation * Quaternion.Inverse(transform.localRotation);

            offsets = (posOffset, rotOffset);
            TrackerManager.storedOffsets.Add(trackerName, offsets);
            offsetsSet = true;
        }

        //This is only for the loader because visuals are in a different spot
        public void BootLoaderCalibrate(Transform compareSkelington)
        {
            Transform activeSkeleton = playerController.transform.GetChild(0).GetChild(2);
            compareSkelington.position = activeSkeleton.position;
            compareSkelington.rotation = activeSkeleton.rotation;
            Transform compareBone = TrackerManager.GetBone(trackerName, compareSkelington);

            Vector3 posOffset = compareBone.localPosition - transform.localPosition;
            Quaternion rotOffset = Quaternion.Euler(compareBone.localRotation.eulerAngles - transform.localRotation.eulerAngles);

            if (trackerType == "foot")
                rotOffset = Quaternion.Euler(rotOffset.eulerAngles + new Vector3(180, 0, 0));

            offsets = (posOffset, rotOffset);
            TrackerManager.storedOffsets.Add(trackerName, offsets);
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
            transform.SetParent(playerController.PlayerVR.transform);
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
                TrackerManager.initCount--;
                Destroy(gameObject); // We <3 self-immolation
            }

            if (Calls.Scene.GetSceneName() != "Loader")
            {
                originalBone = TrackerManager.GetBone(trackerName, playerController.PlayerVisuals.transform.GetChild(1));

                IKTarget = Instantiate(originalBone.gameObject).transform;
                IKTarget.SetParent(transform);
                
                IKTarget.localPosition = Vector3.zero;
                IKTarget.localRotation = Quaternion.Euler(Vector3.zero);

                if (TrackerManager.storedOffsets.ContainsKey(trackerName))
                {
                    offsets = TrackerManager.storedOffsets[trackerName];
                    offsetsSet = true;
                }

                if (offsetsSet)
                {
                    AssignIK();
                }

                if (trackerType == "knee" || trackerType == "elbow")
                {
                    transform.GetChild(0).localPosition += transform.GetChild(0).forward; // Helps prevent knees bending backwards
                }
            }

            int ownerActorNo = playerController.assignedPlayer.Data.GeneralData.actorNo;
            PhotonView photonView = gameObject.transform.GetChild(0).gameObject.AddComponent<PhotonView>();

            viewID = PhotonNetwork.AllocateViewID(ownerActorNo);
            photonView.ViewID = viewID;

            PhotonTransformView photonTransformView = gameObject.transform.GetChild(0).gameObject.AddComponent<PhotonTransformView>();
            photonView.ObservedComponents = new();
            photonView.ObservedComponents.Add(photonTransformView);

            TrackerManager.trackers.Add(trackerName, this);
            TrackerManager.initCount--;
        }

        private void OnDestroy()
        {
            TrackerManager.trackers.Remove(trackerName);
        }
    }

    [HarmonyPatch(typeof(BootLoaderPlayer), nameof(BootLoaderPlayer.Start))]
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
                    GameObject trackerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    OpenVRTracker tracker = trackerObject.AddComponent<OpenVRTracker>();
                    tracker.playerController = __instance;
                    tracker.trackerIndex = i;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetMeasurement))]
    public static class TPosePatch
    {
        private static void Postfix(ref PlayerData __instance)
        {
            MelonLogger.Msg("T-Pose detected!");

            TrackerManager.trackerCount = TrackerManager.trackers.Count; // We're assuming the player doesn't connect or disconnect any trackers while the game is running
            if (TrackerManager.trackerCount > 0)
                Core.loggerInstance.Msg("Calibrating FBT...");
            else return; // We don't need to calibrate what's not there

            Transform compareSkelington = GameObject.Instantiate(Resources.FindObjectsOfTypeAll<PlayerController>().First().PlayerVisuals.transform.GetChild(1).gameObject).transform;

            foreach (OpenVRTracker tracker in TrackerManager.trackers.Values)
            {
                if (Calls.Scene.GetSceneName() == "Loader")
                    tracker.BootLoaderCalibrate(compareSkelington);
                else
                    tracker.Calibrate(compareSkelington);
            }

            GameObject.Destroy(compareSkelington.gameObject);
        }
    }
}