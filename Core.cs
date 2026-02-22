using Il2CppExitGames.Client.Photon;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using Il2CppRootMotion.FinalIK;
using Il2CppRUMBLE.Players;
using MelonLoader;
using RumbleModdingAPI;
using System.Collections;
using UnityEngine;
using Valve.OpenVR;
using Player = Il2CppRUMBLE.Players.Player;

[assembly: MelonInfo(typeof(FullBodyBending.Core), "FullBodyBending", "1.0.1", "Orangenal", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace FullBodyBending
{
    public class Core : Calls.Utilities.RumbleMod
    {
        List<GameObject> spheres;
        public static List<uint> trackerIndices = new List<uint>();
        private CVRSystem system;
        VRIK vrik;

        GameObject chest;
        GameObject pelvis;
        GameObject RKnee;
        GameObject RFoot;
        GameObject LKnee;
        GameObject LFoot;
        GameObject RElbow;
        GameObject LElbow;

        int nextViewID = 138664;
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
            Calls.onMapInitialized += OnSceneWasLoaded;

            LoggerInstance.Msg("Initialised.");
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

            MelonLogger.Msg("Got here!");
            MelonLogger.Msg($"Actor No: {actorNo}");
            Player player = Calls.Players.GetPlayerByActorNo(actorNo);
            MelonLogger.Msg(Calls.Players.GetAllPlayers().Count);
            MelonLogger.Msg(player == null);
            MelonLogger.Msg("Got here!2");
            string id = data[1].ToString();
            MelonLogger.Msg($"Got here!3");

            Transform Visuals = player.Controller.transform.GetChild(1).transform;
            MelonLogger.Msg("got here 3");
            Transform originalPelvis = Visuals.GetChild(1).GetChild(0);

            chest = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).gameObject);
            pelvis = GameObject.Instantiate(originalPelvis.gameObject);
            RKnee = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).gameObject);
            LKnee = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).gameObject);
            RFoot = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).GetChild(0).gameObject);
            LFoot = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).GetChild(0).gameObject);
            RElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0).gameObject);
            LElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0).gameObject);

            MelonLogger.Msg("got here 4");

            vrik = Visuals.GetComponent<VRIK>();
            MelonLogger.Msg("got here 5");
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(player.Controller.transform.GetChild(2));
            sphere.transform.localPosition = Vector3.zero;
            sphere.GetComponent<MeshRenderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            sphere.GetComponent<SphereCollider>().enabled = false;
            sphere.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            MelonLogger.Msg("got here 6");

            PhotonView pv = sphere.AddComponent<PhotonView>();
            pv.ViewID = viewID;
            nextViewID = viewID + 1;

            PhotonTransformView transformView = sphere.AddComponent<PhotonTransformView>();
            pv.ObservedComponents = new();
            pv.ObservedComponents.Add(transformView);

            if (id.ToString().EndsWith("chest"))
            {
                chest.transform.SetParent(sphere.transform);
                vrik.solver.spine.chestGoal = chest.transform;
                vrik.solver.spine.chestGoalWeight = 1f;
                //GameObject.Destroy(sphere);
                //continue;
            }
            else if (id.ToString().EndsWith("waist"))
            {
                pelvis.transform.SetParent(sphere.transform);
                vrik.solver.spine.pelvisTarget = pelvis.transform;
                vrik.solver.spine.pelvisPositionWeight = 1f;
                vrik.solver.spine.pelvisRotationWeight = 1f;
            }
            else if (id.ToString().EndsWith("right_foot"))
            {
                RFoot.transform.SetParent(sphere.transform);
                vrik.solver.rightLeg.target = RFoot.transform;
                vrik.solver.rightLeg.positionWeight = 1f;
                //vrik.solver.rightLeg.rotationWeight = 1f;
            }
            else if (id.ToString().EndsWith("left_foot"))
            {
                LFoot.transform.SetParent(sphere.transform);
                vrik.solver.leftLeg.target = LFoot.transform;
                vrik.solver.leftLeg.positionWeight = 1f;
                //vrik.solver.rightLeg.rotationWeight = 1f;
            }
            else if (id.ToString().EndsWith("right_knee"))
            {
                RKnee.transform.SetParent(sphere.transform);
                vrik.solver.rightLeg.bendGoal = RKnee.transform;
                vrik.solver.rightLeg.bendGoalWeight = 1f;
            }
            else if (id.ToString().EndsWith("left_knee"))
            {
                LKnee.transform.SetParent(sphere.transform);
                vrik.solver.leftLeg.bendGoal = LKnee.transform;
                vrik.solver.leftLeg.bendGoalWeight = 1f;
            }
            else if (id.ToString().EndsWith("left_elbow"))
            {
                LElbow.transform.SetParent(sphere.transform);
                vrik.solver.leftArm.bendGoal = LElbow.transform;
                vrik.solver.leftArm.bendGoalWeight = 1f;
            }
            else if (id.ToString().EndsWith("right_elbow"))
            {
                RElbow.transform.SetParent(sphere.transform);
                vrik.solver.rightArm.bendGoal = RElbow.transform;
                vrik.solver.rightArm.bendGoalWeight = 1f;
            }

            else if (id.ToString() == "liv_virtualcamera")
            {
                GameObject.Destroy(sphere);
                yield break;
            }
            else
            {
                MelonLogger.Msg($"Unknown tracker id: {id}");
                GameObject.Destroy(sphere);
                yield break;
            }

            sphere.transform.GetChild(0).localPosition = Vector3.zero;
            if (id.ToString().EndsWith("knee") || id.ToString().EndsWith("elbow"))
            {
                sphere.transform.GetChild(0).localPosition = new Vector3(0, 0, 0.5f);
            }

            if (id.ToString().EndsWith("foot"))
            {
                sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(90, -30, 0));
            }

            sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(0, -30, 0));
            yield break;
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            spheres = [];
            trackerIndices = [];
        }

        public void OnSceneWasLoaded()
        {
            spheres = [];
            trackerIndices = [];
            string sceneName = Calls.Scene.GetSceneName();

            if (sceneName == "Gym")
            {
                RegisterEvents();
            }

            if (sceneName == "Loader") return;
            MelonLogger.Msg("got here 2");
            Transform Visuals = Calls.Players.GetLocalPlayer().Controller.transform.GetChild(1).transform;
            MelonLogger.Msg("got here 3");
            Transform originalPelvis = Visuals.GetChild(1).GetChild(0);

            chest = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).gameObject);
            pelvis = GameObject.Instantiate(originalPelvis.gameObject);
            RKnee = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).gameObject);
            LKnee = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).gameObject);
            RFoot = GameObject.Instantiate(originalPelvis.GetChild(3).GetChild(0).GetChild(0).gameObject);
            LFoot = GameObject.Instantiate(originalPelvis.GetChild(2).GetChild(0).GetChild(0).gameObject);
            RElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0).gameObject);
            LElbow = GameObject.Instantiate(originalPelvis.GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0).gameObject);
            MelonLogger.Msg("got here 4");

            //Visuals.GetChild(2).gameObject.active = false;
            //Visuals.GetComponent<Animator>().enabled = false;
            //Visuals.GetComponent<PlayerAnimator>().enabled = false;
            //Visuals.GetComponent<PlayerIK>().enabled = false;
            //Visuals.GetComponent<VRIK>().enabled = false;

            vrik = Visuals.GetComponent<VRIK>();
            MelonLogger.Msg("got here 5");
            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
            for (uint i = 0; i < poses.Length; i++)
            {
                if (OpenVR.System.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    MelonLogger.Msg("got here 6");
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.SetParent(Calls.Players.GetLocalPlayer().Controller.transform.GetChild(2));
                    sphere.transform.localPosition = Vector3.zero;
                    sphere.GetComponent<MeshRenderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                    sphere.GetComponent<SphereCollider>().enabled = false;
                    sphere.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

                    //GameObject square = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    //square.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
                    //square.transform.SetParent(sphere.transform);
                    //square.transform.localPosition = new Vector3(0, 0, 1);
                    //square.GetComponent<MeshRenderer>().material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                    //square.GetComponent<Collider>().enabled = false;

                    MelonLogger.Msg("got here 7");
                    var id = new System.Text.StringBuilder(64);
                    ETrackedPropertyError error = new();

                    OpenVR.System.GetStringTrackedDeviceProperty(
                        i,
                        ETrackedDeviceProperty.Prop_ControllerType_String,
                        id, 64, ref error
                    );

                    int numPlayers = Calls.Players.GetAllPlayers().Count;
                    if (numPlayers > 1)
                    {
                        MelonLogger.Msg($"{numPlayers} players in lobby");
                        nextViewID += numPlayers * 6;
                    }

                    PhotonView pv = sphere.AddComponent<PhotonView>();
                    pv.ViewID = nextViewID;
                    nextViewID++;

                    PhotonTransformView transformView = sphere.AddComponent<PhotonTransformView>();
                    pv.ObservedComponents = new();
                    pv.ObservedComponents.Add(transformView);

                    List<Il2CppSystem.Object> data = new();
                    data.Add(Calls.Players.GetLocalPlayer().Data.GeneralData.ActorNo);
                    data.Add(id.ToString());
                    data.Add(nextViewID - 1);
                    RaiseEvent(data, REOptions, SendOptions.SendReliable);

                    if (id.ToString().EndsWith("chest"))
                    {
                        chest.transform.SetParent(sphere.transform);
                        vrik.solver.spine.chestGoal = chest.transform;
                        vrik.solver.spine.chestGoalWeight = 1f;
                        //GameObject.Destroy(sphere);
                        //continue;
                    }
                    else if (id.ToString().EndsWith("waist"))
                    {
                        pelvis.transform.SetParent(sphere.transform);
                        vrik.solver.spine.pelvisTarget = pelvis.transform;
                        vrik.solver.spine.pelvisPositionWeight = 1f;
                        vrik.solver.spine.pelvisRotationWeight = 1f;
                    }
                    else if (id.ToString().EndsWith("right_foot"))
                    {
                        RFoot.transform.SetParent(sphere.transform);
                        vrik.solver.rightLeg.target = RFoot.transform;
                        vrik.solver.rightLeg.positionWeight = 1f;
                        vrik.solver.rightLeg.rotationWeight = 1f;
                    }
                    else if (id.ToString().EndsWith("left_foot"))
                    {
                        LFoot.transform.SetParent(sphere.transform);
                        vrik.solver.leftLeg.target = LFoot.transform;
                        vrik.solver.leftLeg.positionWeight = 1f;
                        vrik.solver.leftLeg.rotationWeight = 1f;
                    }
                    else if (id.ToString().EndsWith("right_knee"))
                    {
                        RKnee.transform.SetParent(sphere.transform);
                        vrik.solver.rightLeg.bendGoal = RKnee.transform;
                        vrik.solver.rightLeg.bendGoalWeight = 1f;
                    }
                    else if (id.ToString().EndsWith("left_knee"))
                    {
                        LKnee.transform.SetParent(sphere.transform);
                        vrik.solver.leftLeg.bendGoal = LKnee.transform;
                        vrik.solver.leftLeg.bendGoalWeight = 1f;
                    }
                    else if (id.ToString().EndsWith("left_elbow"))
                    {
                        LElbow.transform.SetParent(sphere.transform);
                        vrik.solver.leftArm.bendGoal = LElbow.transform;
                        vrik.solver.leftArm.bendGoalWeight = 1f;
                    }
                    else if (id.ToString().EndsWith("right_elbow"))
                    {
                        RElbow.transform.SetParent(sphere.transform);
                        vrik.solver.rightArm.bendGoal = RElbow.transform;
                        vrik.solver.rightArm.bendGoalWeight = 1f;
                    }

                    else if (id.ToString() == "liv_virtualcamera")
                    {
                        GameObject.Destroy(sphere);
                        continue;
                    }
                    else
                    {
                        MelonLogger.Msg($"Unknown tracker id: {id}");
                        GameObject.Destroy(sphere);
                        continue;
                    }
                    sphere.transform.GetChild(0).localPosition = Vector3.zero;
                    sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(0, -30, 0));

                    // Prevent knees and elbows bending backwards
                    if (id.ToString().EndsWith("knee") || id.ToString().EndsWith("elbow"))
                    {
                        sphere.transform.GetChild(0).localPosition = sphere.transform.GetChild(0).forward;
                    }

                    if (id.ToString().EndsWith("foot"))
                    {
                        sphere.transform.GetChild(0).localRotation = Quaternion.Euler(new Vector3(90, -30, 0));
                    }

                    spheres.Add(sphere);
                    trackerIndices.Add(i); // Store tracker index, not pose
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

    //[HarmonyPatch(typeof(BootLoaderMeasurementSystem), nameof(BootLoaderMeasurementSystem.DoMeasurement))]
    //public static class Patch
    //{
    //    private static void Postfix()
    //    {
    //        MelonLogger.Msg("Do the mesauremtnet");
    //    }
    //}
}