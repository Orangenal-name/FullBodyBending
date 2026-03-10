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
using Player = Il2CppRUMBLE.Players.Player;

[assembly: MelonInfo(typeof(FullBodyBending.Core), "FullBodyBending", "1.0.0", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace FullBodyBending
{
    public class Core : Utilities.RumbleMod
    {
        internal static List<GameObject> spheres;
        public static List<uint> trackerIndices = new List<uint>();
        internal static CVRSystem system;

        static GameObject chest;
        static GameObject pelvis;
        static GameObject RKnee;
        static GameObject RFoot;
        static GameObject LKnee;
        static GameObject LFoot;
        static GameObject RElbow;
        static GameObject LElbow;

        internal static Dictionary<string, (Vector3, Quaternion)> offsets = new();

        private static RaiseEventOptions REOptions = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };

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

            LoggerInstance.Msg("Initialised.");
        }

        internal static int SpawnTracker(PlayerController playerController, string trackerID, bool isOwn, int ownerActorNo = -1, int viewID = -1, bool debug = true)
        {
            string sceneName = Calls.Scene.GetSceneName();
            Transform Visuals = playerController.transform.Find("Visuals");
            VRIK vrik = Visuals.GetComponent<VRIK>();

            Transform originalPelvis;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            if (sceneName == "Loader")
            {
                MelonLogger.Msg("no problem");
                originalPelvis = Visuals.GetChild(2).GetChild(0);
                MelonLogger.Msg("perhaps problem?");
                sphere.transform.SetParent(playerController.transform);
            }
            else
            {
                MelonLogger.Msg("problem");
                originalPelvis = Visuals.GetChild(1).GetChild(0);
                sphere.transform.SetParent(playerController.transform.GetChild(2));
            }

            float ballSize = 0.15f;

            sphere.transform.localPosition = Vector3.zero;
            sphere.GetComponent<MeshRenderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            sphere.GetComponent<SphereCollider>().enabled = false;
            sphere.transform.localScale = new Vector3(ballSize, ballSize, ballSize);
            sphere.name = trackerID;

            if (!debug)
            {
                sphere.GetComponent<MeshRenderer>().enabled = false;
            }

            string shortID;
            GameObject bone;

            if (trackerID.EndsWith("chest"))
            {
                if (sceneName != "Loader")
                    chest = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).gameObject);
                else chest = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).gameObject);
                chest.transform.SetParent(sphere.transform);
                vrik.solver.spine.chestGoal = chest.transform;
                vrik.solver.spine.chestGoalWeight = 1f;

                shortID = "chest";
                bone = chest;

                //GameObject.Destroy(sphere);
                //continue;
            }
            else if (trackerID.EndsWith("waist"))
            {
                pelvis = GameObject.Instantiate(originalPelvis.gameObject);
                pelvis.transform.SetParent(sphere.transform);
                vrik.solver.spine.pelvisTarget = pelvis.transform;
                vrik.solver.spine.pelvisPositionWeight = 1f;
                vrik.solver.spine.pelvisRotationWeight = 1f;

                shortID = "waist";
                bone = pelvis;
            }
            else if (trackerID.EndsWith("right_foot"))
            {
                RFoot = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).GetChild(0).gameObject);
                RFoot.transform.SetParent(sphere.transform);
                vrik.solver.rightLeg.target = RFoot.transform;
                vrik.solver.rightLeg.positionWeight = 1f;
                vrik.solver.rightLeg.rotationWeight = 1f;

                shortID = "rfoot";
                bone = RFoot;
            }
            else if (trackerID.EndsWith("left_foot"))
            {
                LFoot = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).GetChild(0).gameObject);
                LFoot.transform.SetParent(sphere.transform);
                vrik.solver.leftLeg.target = LFoot.transform;
                vrik.solver.leftLeg.positionWeight = 1f;
                vrik.solver.leftLeg.rotationWeight = 1f;

                shortID = "lfoot";
                bone = LFoot;
            }
            else if (trackerID.EndsWith("right_knee"))
            {
                RKnee = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).gameObject);
                RKnee.transform.SetParent(sphere.transform);
                vrik.solver.rightLeg.bendGoal = RKnee.transform;
                vrik.solver.rightLeg.bendGoalWeight = 1f;

                shortID = "rknee";
                bone = RKnee;
            }
            else if (trackerID.EndsWith("left_knee"))
            {
                LKnee = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).gameObject);
                LKnee.transform.SetParent(sphere.transform);
                vrik.solver.leftLeg.bendGoal = LKnee.transform;
                vrik.solver.leftLeg.bendGoalWeight = 1f;

                shortID = "lknee";
                bone = LKnee;
            }
            else if (trackerID.EndsWith("left_elbow"))
            {
                LElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0).gameObject);
                LElbow.transform.SetParent(sphere.transform);
                vrik.solver.leftArm.bendGoal = LElbow.transform;
                vrik.solver.leftArm.bendGoalWeight = 1f;

                shortID = "lelbow";
                bone = LElbow;
            }
            else if (trackerID.EndsWith("right_elbow"))
            {
                RElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0).gameObject);
                RElbow.transform.SetParent(sphere.transform);
                vrik.solver.rightArm.bendGoal = RElbow.transform;
                vrik.solver.rightArm.bendGoalWeight = 1f;

                shortID = "relbow";
                bone = RElbow;
            }

            else if (trackerID == "liv_virtualcamera")
            {
                GameObject.Destroy(sphere);
                return -1;
            }
            else
            {
                MelonLogger.Msg($"Unknown tracker id: {trackerID}");
                GameObject.Destroy(sphere);
                return -1;
            }

            if (shortID != null && bone != null)
            {
                if (offsets.ContainsKey(shortID))
                {
                    bone.transform.localRotation = offsets[shortID].Item2;
                    bone.transform.localPosition = offsets[shortID].Item1;
                }
            }


            //sphere.transform.GetChild(0).localPosition = Vector3.zero;
            sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(0, -30, 0));

            if (trackerID.EndsWith("knee") || trackerID.EndsWith("elbow"))
            {
                sphere.transform.GetChild(0).localPosition += sphere.transform.GetChild(0).transform.forward;
            }

            if (trackerID.EndsWith("foot"))
            {
                sphere.transform.GetChild(0).Rotate(new Vector3(90, 0, 0));
            }
            MelonLogger.Msg(spheres == null);
            if (isOwn)
            {
                spheres.Add(sphere);
            }

            //sphere.transform.GetChild(0).localRotation = Quaternion.Euler(Vector3.zero);
            //sphere.transform.GetChild(0).localPosition = Vector3.zero;

            if (ownerActorNo != -1)
            {
                PhotonView pv = sphere.AddComponent<PhotonView>();
                if (viewID == -1)
                    pv.ViewID = PhotonNetwork.AllocateViewID(ownerActorNo);
                else
                    pv.ViewID = viewID;

                PhotonTransformView transformView = sphere.AddComponent<PhotonTransformView>();
                pv.ObservedComponents = new();
                pv.ObservedComponents.Add(transformView);
                MelonLogger.Msg($"Correct actor no: {playerController.assignedPlayer.Data.GeneralData.ActorNo}/{ownerActorNo} Photon View owner: {pv.ownerActorNr}");
                return pv.ViewID;
            }

            return -1;
        }

        public override void OnEvent(List<Il2CppSystem.Object> data)
        {
            MelonCoroutines.Start(DelayedEvent(data));
        }

        IEnumerator DelayedEvent(List<Il2CppSystem.Object> data)
        {
            while (Calls.Players.GetAllPlayers().Count <= 1) yield return null;

            Int16 actorNo = data[0].Unbox<Int16>();
            int viewID = data[2].Unbox<int>();

            Player player = Calls.Players.GetPlayerByActorNo(actorNo);
            string id = data[1].ToString();

            MelonLogger.Msg($"Received view id {viewID} for tracker {id} from actor {actorNo}");

            SpawnTracker(player.Controller, id, false, actorNo, viewID);
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            spheres = [];
            trackerIndices = [];
        }

        public void OnSceneWasLoaded(string sceneName)
        {
            spheres = [];
            trackerIndices = [];
            Player localPlayer = Calls.Players.GetLocalPlayer();

            
            RegisterEvents();

            if (sceneName == "Loader") return;

            if (offsets.Count == 0)
            {
                MelonLogger.Warning("No offsets set! Please T-Pose to enable full body tracking!");
                return;
            }

            int numPlayers = Calls.Players.GetAllPlayers().Count;
            if (numPlayers > 1)
            {
                MelonLogger.Msg($"{numPlayers} players in lobby");
            }

            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
            for (uint i = 0; i < poses.Length; i++)
            {
                if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    var id = new System.Text.StringBuilder(64);
                    ETrackedPropertyError error = new();

                    OpenVR.System.GetStringTrackedDeviceProperty(
                        i,
                        ETrackedDeviceProperty.Prop_ControllerType_String,
                        id, 64, ref error
                    );

                    int actorNo = Calls.Players.GetLocalPlayer().Data.GeneralData.ActorNo;

                    int viewID = SpawnTracker(localPlayer.Controller, id.ToString(), true, actorNo);
                    trackerIndices.Add(i);

                    if (viewID == -1 || sceneName == "Gym") continue;

                    List<Il2CppSystem.Object> data = new();
                    data.Add(actorNo);
                    data.Add(id.ToString());
                    data.Add(viewID);
                    RaiseEvent(data, REOptions, SendOptions.SendReliable);
                }
            }
        }

        public override void OnLateUpdate()
        {
            if (spheres.Count > 0 && trackerIndices.Count > 0 && system != null)
            {
                var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
                system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);

                for (int i = 0; i < trackerIndices.Count; i++)
                {
                    var id = new System.Text.StringBuilder(64);
                    ETrackedPropertyError error = new();

                    OpenVR.System.GetStringTrackedDeviceProperty(
                        (uint)i,
                        ETrackedDeviceProperty.Prop_ControllerType_String,
                        id, 64, ref error
                    );

                    uint index = trackerIndices[i];
                    if (poses[index].bDeviceIsConnected && poses[index].bPoseIsValid)
                    {
                        var matrix = poses[index].mDeviceToAbsoluteTracking;
                        Vector3 position = new Vector3(matrix.m3, matrix.m7, -matrix.m11);

                        spheres[i].transform.localPosition = position;

                        Vector3 forward = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
                        Vector3 up = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

                        spheres[i].transform.localRotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0, 225, 0);
                    }
                }
            }
        }

        internal static void Calibrate(Transform activeSkeleton)
        {
            Transform compareSkelington = GameObject.Instantiate(Resources.FindObjectsOfTypeAll<PlayerController>().First().transform.GetChild(1).GetChild(1));

            compareSkelington.transform.position = activeSkeleton.transform.position;

            foreach (GameObject sphere in Core.spheres)
            {
                if (sphere.name.EndsWith("chest"))
                {
                    Transform compareChest = compareSkelington.transform.GetChild(0).GetChild(4).GetChild(0);
                    Vector3 chestOffsetPos = compareChest.localPosition - sphere.transform.localPosition;
                    Quaternion chestOffsetRot = compareChest.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("chest", (chestOffsetPos, chestOffsetRot));
                }
                else if (sphere.name.EndsWith("waist"))
                {
                    Transform compareWaist = compareSkelington.transform.GetChild(0);
                    Vector3 waistOffsetPos = compareWaist.localPosition - sphere.transform.localPosition;
                    Quaternion waistOffsetRot = compareWaist.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("waist", (waistOffsetPos, waistOffsetRot));
                }
                else if (sphere.name.EndsWith("right_foot"))
                {
                    Transform compareRFoot = compareSkelington.transform.GetChild(0).GetChild(2).GetChild(0).GetChild(0);
                    Vector3 RFootOffsetPos = compareRFoot.localPosition - sphere.transform.localPosition;
                    Quaternion RFootOffsetRot = compareRFoot.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("rfoot", (RFootOffsetPos, RFootOffsetRot));
                }
                else if (sphere.name.EndsWith("left_foot"))
                {
                    Transform compareLFoot = compareSkelington.transform.GetChild(0).GetChild(2).GetChild(0).GetChild(0);
                    Vector3 LFootOffsetPos = compareLFoot.localPosition - sphere.transform.localPosition;
                    Quaternion LFootOffsetRot = compareLFoot.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("lfoot", (LFootOffsetPos, LFootOffsetRot));
                }
                else if (sphere.name.EndsWith("right_knee"))
                {
                    Transform compareRKnee = compareSkelington.transform.GetChild(0).GetChild(3).GetChild(0);
                    Vector3 RKneeOffsetPos = compareRKnee.localPosition - sphere.transform.localPosition;
                    Quaternion RKneeOffsetRot = compareRKnee.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("rknee", (RKneeOffsetPos, RKneeOffsetRot));
                }
                else if (sphere.name.EndsWith("left_knee"))
                {
                    Transform compareLKnee = compareSkelington.transform.GetChild(0).GetChild(2).GetChild(0);
                    Vector3 LKneeOffsetPos = compareLKnee.localPosition - sphere.transform.localPosition;
                    Quaternion LKneeOffsetRot = compareLKnee.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("lknee", (LKneeOffsetPos, LKneeOffsetRot));
                }
                else if (sphere.name.EndsWith("right_elbow"))
                {
                    Transform compareRElbow = compareSkelington.transform.GetChild(0).GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0);
                    Vector3 RElbowOffsetPos = compareRElbow.localPosition - sphere.transform.localPosition;
                    Quaternion RElbowOffsetRot = compareRElbow.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("relbow", (RElbowOffsetPos, RElbowOffsetRot));
                }
                else if (sphere.name.EndsWith("left_elbow"))
                {
                    Transform compareLElbow = compareSkelington.transform.GetChild(0).GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0);
                    Vector3 LElbowOffsetPos = compareLElbow.localPosition - sphere.transform.localPosition;
                    Quaternion LelbowOffsetRot = compareLElbow.localRotation * Quaternion.Inverse(sphere.transform.localRotation);

                    Core.offsets.Add("lelbow", (LElbowOffsetPos, LelbowOffsetRot));
                }
            }
        }
    }

    [HarmonyPatch(typeof(BootLoaderMeasurementSystem), nameof(BootLoaderMeasurementSystem.DoMeasurement))]
    public static class MeasurePatch
    {
        private static void Postfix(ref BootLoaderMeasurementSystem __instance)
        {
            MelonLogger.Msg("Calibrating FBT");
            Transform activeSkeleton = __instance.transform.GetChild(0).GetChild(2);

            Core.Calibrate(activeSkeleton);
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
                    var id = new System.Text.StringBuilder(64);
                    ETrackedPropertyError propError = new();

                    OpenVR.System.GetStringTrackedDeviceProperty(
                        i,
                        ETrackedDeviceProperty.Prop_ControllerType_String,
                        id, 64, ref propError
                    );

                    int viewID = Core.SpawnTracker(__instance, id.ToString(), true, debug:true);

                    Core.trackerIndices.Add(i);
                }
            }
        }
    }
}