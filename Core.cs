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
        List<GameObject> spheres;
        public static List<uint> trackerIndices = new List<uint>();
        private CVRSystem system;

        GameObject chest;
        GameObject pelvis;
        GameObject RKnee;
        GameObject RFoot;
        GameObject LKnee;
        GameObject LFoot;
        GameObject RElbow;
        GameObject LElbow;

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

        internal int SpawnTracker(PlayerController playerController, string trackerID, bool isOwn, int ownerActorNo, int viewID = -1)
        {
            Transform Visuals = playerController.transform.Find("Visuals");
            VRIK vrik = Visuals.GetComponent<VRIK>();
            Transform originalPelvis = Visuals.GetChild(1).GetChild(0);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(playerController.transform.GetChild(2));
            sphere.transform.localPosition = Vector3.zero;
            sphere.GetComponent<MeshRenderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            sphere.GetComponent<SphereCollider>().enabled = false;
            sphere.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            sphere.name = trackerID;

            PhotonView pv = sphere.AddComponent<PhotonView>();
            if (viewID == -1)
                pv.ViewID = PhotonNetwork.AllocateViewID(ownerActorNo);
            else
                pv.ViewID = viewID;

            PhotonTransformView transformView = sphere.AddComponent<PhotonTransformView>();
            pv.ObservedComponents = new();
            pv.ObservedComponents.Add(transformView);

            MelonLogger.Msg($"Correct actor no: {playerController.assignedPlayer.Data.GeneralData.ActorNo} Photon View owner: {pv.ownerActorNr}");

            if (trackerID.EndsWith("chest"))
            {
                chest = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).gameObject);
                chest.transform.SetParent(sphere.transform);
                vrik.solver.spine.chestGoal = chest.transform;
                vrik.solver.spine.chestGoalWeight = 1f;
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
            }
            else if (trackerID.EndsWith("right_foot"))
            {
                RFoot = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).GetChild(0).gameObject);
                RFoot.transform.SetParent(sphere.transform);
                vrik.solver.rightLeg.target = RFoot.transform;
                vrik.solver.rightLeg.positionWeight = 1f;
                //vrik.solver.rightLeg.rotationWeight = 1f;
            }
            else if (trackerID.EndsWith("left_foot"))
            {
                LFoot = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).GetChild(0).gameObject);
                LFoot.transform.SetParent(sphere.transform);
                vrik.solver.leftLeg.target = LFoot.transform;
                vrik.solver.leftLeg.positionWeight = 1f;
                //vrik.solver.rightLeg.rotationWeight = 1f;
            }
            else if (trackerID.EndsWith("right_knee"))
            {
                RKnee = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).gameObject);
                RKnee.transform.SetParent(sphere.transform);
                vrik.solver.rightLeg.bendGoal = RKnee.transform;
                vrik.solver.rightLeg.bendGoalWeight = 1f;
            }
            else if (trackerID.EndsWith("left_knee"))
            {
                LKnee = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).gameObject);
                LKnee.transform.SetParent(sphere.transform);
                vrik.solver.leftLeg.bendGoal = LKnee.transform;
                vrik.solver.leftLeg.bendGoalWeight = 1f;
            }
            else if (trackerID.EndsWith("left_elbow"))
            {
                LElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0).gameObject);
                LElbow.transform.SetParent(sphere.transform);
                vrik.solver.leftArm.bendGoal = LElbow.transform;
                vrik.solver.leftArm.bendGoalWeight = 1f;
            }
            else if (trackerID.EndsWith("right_elbow"))
            {
                RElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0).gameObject);
                RElbow.transform.SetParent(sphere.transform);
                vrik.solver.rightArm.bendGoal = RElbow.transform;
                vrik.solver.rightArm.bendGoalWeight = 1f;
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

            sphere.transform.GetChild(0).localPosition = Vector3.zero;
            sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(0, -30, 0));

            if (trackerID.EndsWith("knee") || trackerID.EndsWith("elbow"))
            {
                sphere.transform.GetChild(0).localPosition = new Vector3(0, 0, 0.5f);
            }

            if (trackerID.EndsWith("foot"))
            {
                sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(90, -30, 0));
            }

            if (isOwn)
            {
                spheres.Add(sphere);
            }

            return pv.ViewID;
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

            if (!EventsRegisted)
            {
                RegisterEvents();
            }

            if (sceneName == "Loader") return;

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

                    if (viewID == -1) return;

                    List<Il2CppSystem.Object> data = new();
                    data.Add(actorNo);
                    data.Add(id.ToString());
                    data.Add(viewID);
                    RaiseEvent(data, REOptions, SendOptions.SendReliable);

                    trackerIndices.Add(i);
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

                        //if (id.ToString().EndsWith("chest"))
                        //{
                        //    var matrix2 = poses[index].mDeviceToAbsoluteTracking;
                        //    Vector3 position2 = new Vector3(matrix.m3, matrix.m7, -matrix.m11);

                        //    chest.transform.localPosition = position;

                        //    Vector3 forward2 = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
                        //    Vector3 up2 = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

                        //    chest.transform.rotation = Quaternion.LookRotation(forward, up);
                        //}
                        //if (id.ToString().EndsWith("waist"))
                        //{
                        //    var matrix3 = poses[index].mDeviceToAbsoluteTracking;
                        //    Vector3 position3 = new Vector3(matrix.m3, matrix.m7, -matrix.m11);

                        //    pelvis.transform.localPosition = position - pelvisPositionOffset;

                        //    Vector3 forward3 = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
                        //    Vector3 up3 = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

                        //    pelvis.transform.rotation = Quaternion.LookRotation(forward, up);
                        //    Vector3 rotation = pelvis.transform.rotation.eulerAngles;
                        //    rotation += pelvisRotationOffset;
                        //    pelvis.transform.rotation = Quaternion.Euler(rotation);
                        //}
                        //else if (id.ToString().EndsWith("right_foot"))
                        //{
                        //    var matrix3 = poses[index].mDeviceToAbsoluteTracking;
                        //    Vector3 position3 = new Vector3(matrix.m3, matrix.m7, -matrix.m11);

                        //    RFoot.transform.localPosition = position - pelvisPositionOffset;

                        //    Vector3 forward3 = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
                        //    Vector3 up3 = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

                        //    RFoot.transform.rotation = Quaternion.LookRotation(forward, up);
                        //    //Vector3 rotation = RFoot.transform.rotation.eulerAngles;
                        //    //rotation += pelvisRotationOffset;
                        //    //RFoot.transform.rotation = Quaternion.Euler(rotation);
                        //}
                        //else if (id.ToString().EndsWith("left_foot"))
                        //{
                        //    var matrix3 = poses[index].mDeviceToAbsoluteTracking;
                        //    Vector3 position3 = new Vector3(matrix.m3, matrix.m7, -matrix.m11);

                        //    LFoot.transform.localPosition = position - pelvisPositionOffset;

                        //    Vector3 forward3 = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
                        //    Vector3 up3 = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

                        //    LFoot.transform.rotation = Quaternion.LookRotation(forward, up);
                        //    //Vector3 rotation = RFoot.transform.rotation.eulerAngles;
                        //    //rotation += pelvisRotationOffset;
                        //    //RFoot.transform.rotation = Quaternion.Euler(rotation);
                        //}
                        //else if (id.ToString().EndsWith("right_knee"))
                        //{
                        //    var matrix3 = poses[index].mDeviceToAbsoluteTracking;
                        //    Vector3 position3 = new Vector3(matrix.m3, matrix.m7, -matrix.m11);

                        //    RKnee.transform.localPosition = position - pelvisPositionOffset;

                        //    Vector3 forward3 = new Vector3(matrix.m2, matrix.m6, -matrix.m10);
                        //    Vector3 up3 = new Vector3(matrix.m1, matrix.m5, -matrix.m9);

                        //    RKnee.transform.rotation = Quaternion.LookRotation(forward, up);
                        //    //Vector3 rotation = RFoot.transform.rotation.eulerAngles;
                        //    //rotation += pelvisRotationOffset;
                        //    //RFoot.transform.rotation = Quaternion.Euler(rotation);
                        //}
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BootLoaderMeasurementSystem), nameof(BootLoaderMeasurementSystem.DoMeasurement))]
    public static class Patch
    {
        private static void Postfix()
        {
            MelonLogger.Msg("Do the mesauremtnet");
        }
    }
}