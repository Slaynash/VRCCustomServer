using ExitGames.Client.Photon;
using Harmony;
using MelonLoader;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using VRC.Core;
using VRC.UI;
//using PhotonNetwork = ObjectPublicAbstractSealedStPhStObInSeSiObBoStUnique;

[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonInfo(typeof(VRCCustomServer.VRCCustomServer), "VRCCustomServer", "0.2", "Slaynash")]

namespace VRCCustomServer
{
    public class VRCCustomServer : MelonMod
    {
        private static ServerSettings ss;
        private static ServerDef defaultServer;
        private static Dropdown serverDropdown;
        private static List<ServerDef> serverList;

        public override void OnApplicationStart()
        {
            Log("Patching Photon*AppId");
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("slaynash.VRCCustomServer");

            harmonyInstance.Patch(
                typeof(VRCApplicationSetup).GetProperty("PhotonProdAppId", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                //typeof(VRCApplicationSetup).GetProperty("PhotonProdAppId", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                new HarmonyMethod(typeof(VRCCustomServer).GetMethod("PhotonAppIdPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            harmonyInstance.Patch(
                typeof(VRCApplicationSetup).GetProperty("PhotonDevAppId", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                //typeof(VRCApplicationSetup).GetProperty("PhotonDevAppId", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                new HarmonyMethod(typeof(VRCCustomServer).GetMethod("PhotonAppIdPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            
            // Workaround for Unsequanced datas - Should be fixed with Photon Server v5
            
            harmonyInstance.Patch(
                typeof(PeerBase).Assembly.GetType("ExitGames.Client.Photon.EnetPeer").GetMethod("CreateAndEnqueueCommand", (BindingFlags)(-1)),
                new HarmonyMethod(typeof(VRCCustomServer).GetMethod("CreateAndEnqueueCommandPrefix", BindingFlags.NonPublic | BindingFlags.Static)));


            harmonyInstance.Patch(
                typeof(ApiWorld).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "GetNewInstance" && m.GetParameters().Length == 1),
                prefix: new HarmonyMethod(typeof(VRCCustomServer).GetMethod("GetNewInstancePrefix", BindingFlags.NonPublic | BindingFlags.Static)));

            /*
            MethodInfo enterWorldMethod = typeof(VRCFlowManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    //.FirstOrDefault(m => m.Name == "Method_Public_Void_String_String_ObjectPublicObDi2StStUnique_Action_1_String_Boolean_0");
                    .FirstOrDefault(m => m.Name == "Method_Public_Void_String_String_ObjectPublicObDi2StStUnique_Action_1_String_Boolean_0");

            if (enterWorldMethod == null)
            {
                MelonLogger.Error("Failed to find 'void VRCFlowManager::EnterWorld(string, string, TransitionInfo, Action<string>, bool)'");
                MelonLogger.Error("Portal instance resolver will not work.");
            }
            else
            {
                harmonyInstance.Patch(
                    enterWorldMethod,
                    postfix: new HarmonyMethod(typeof(VRCCustomServer).GetMethod("EnterWorldPostfix", BindingFlags.NonPublic | BindingFlags.Static)));
            }
            */

            harmonyInstance.Patch(
                    typeof(VRCFlowManager).GetMethod("EnterWorld"),
                    postfix: new HarmonyMethod(typeof(VRCCustomServer).GetMethod("EnterWorldPostfix", BindingFlags.NonPublic | BindingFlags.Static)));

            Log("Patch done");

            MelonCoroutines.Start(InitAfterFrame());
        }

        private IEnumerator InitAfterFrame()
        {
            yield return null;
            //ss = PhotonNetwork.field_Public_Static_ServerSettings_0;
            ss = PhotonNetwork.PhotonServerSettings;
            MelonLogger.Msg("ss: " + ss?.ToString() ?? "null");
            MelonLogger.Msg("AppSettings: " + ss?.AppSettings?.ToString() ?? "null");

            defaultServer = ServerDef.CloudServer("VRChat Cloud Server USW", ss.AppSettings.FixedRegion, ss.AppSettings.AppIdRealtime, ss.AppSettings.AppVersion);

            foreach (string text in Environment.GetCommandLineArgs())
            {
                if (text.StartsWith("--photonUseNS="))
                {
                    string value = text.Substring("--photonUseNS=".Length);
                    if (value == "false" || value == "0")
                    {
                        Log("setting UserNameServer to false. Old value was " + ss.AppSettings.UseNameServer);
                        ss.AppSettings.UseNameServer = false;
                    }
                    else if (value == "true" || value == "1")
                    {
                        Log("setting UserNameServer to true. Old value was " + ss.AppSettings.UseNameServer);
                        ss.AppSettings.UseNameServer = true;
                    }
                    else
                        Log("--photonUseNS was passed with an invalid value");
                }
                else if (text.StartsWith("--photonFixedRegion="))
                {
                    Log("setting FixedRegion to " + text.Substring("--photonFixedRegion=".Length) + ". Old value was " + ss.AppSettings.FixedRegion);
                    ss.AppSettings.FixedRegion = text.Substring("--photonFixedRegion=".Length);
                }
                else if (text.StartsWith("--photonServer="))
                {
                    Log("setting Server to " + text.Substring("--photonServer=".Length) + ". Old value was " + ss.AppSettings.Server);
                    ss.AppSettings.Server = text.Substring("--photonServer=".Length);
                }
                else if (text.StartsWith("--photonPort="))
                {
                    if (int.TryParse(text.Substring("--photonPort=".Length), out int valueInt))
                    {
                        Log("setting Port to " + valueInt + ". Old value was " + ss.AppSettings.Port);
                        ss.AppSettings.Port = valueInt;
                        Log("Port set to " + ss.AppSettings.Port);
                    }
                    else
                        Log("--photonPort was passed with an invalid value");
                }
                else if (text.StartsWith("--photonId"))
                {
                    //Log("setting Id to " + text.Substring("--photonId=".Length) + ". Old value was " + VRCApplicationSetup.field_Private_Static_VRCApplicationSetup_0.gameServerVersionOverride);
                    //VRCApplicationSetup.field_Private_Static_VRCApplicationSetup_0.gameServerVersionOverride = text.Substring("--photonId=".Length);
                    //Log("Id set to " + VRCApplicationSetup.field_Private_Static_VRCApplicationSetup_0.gameServerVersionOverride);
                    Log("setting Id to " + text.Substring("--photonId=".Length) + ". Old value was " + VRCApplicationSetup.Instance.gameServerVersionOverride);
                    VRCApplicationSetup.Instance.gameServerVersionOverride = text.Substring("--photonId=".Length);
                    Log("Id set to " + VRCApplicationSetup.Instance.gameServerVersionOverride);
                }
            }

            MelonLogger.Msg("Waiting for InitUI");

            yield return null;// InitUI();

            MelonLogger.Msg("Done InitUI");
        }

        private IEnumerator InitUI()
        {
            PopupRoomInstance[] pris;
            while ((pris = Resources.FindObjectsOfTypeAll<PopupRoomInstance>()).Length == 0)
                yield return null;

            PopupRoomInstance pri = pris[0];

            RectTransform nameTextRT = pri.transform.Find("Popup/NameText").GetComponent<RectTransform>();
            nameTextRT.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;



            byte[] data = Properties.Resources.dropdown;
            AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(data);
            while (!request.isDone)
            {
                yield return null;
            }
            if (request.assetBundle == null)
            {
                MelonLogger.Error("Unable to load dropdown Assetbundle");
                yield break;
            }

            //Load main prefab
            AssetBundleRequest abrMain = request.assetBundle.LoadAssetWithSubAssetsAsync("Assets/Prefabs/Dropdown.prefab");
            while (!abrMain.isDone)
                yield return null;
            if (abrMain.asset == null)
            {
                MelonLogger.Error("Unable to load Dropdown prefab from Assetbundle (prefab is null)");
                yield break;
            }
            Dropdown prefab = abrMain.asset.Cast<GameObject>().GetComponent<Dropdown>();

            if (prefab == null)
            {
                MelonLogger.Error("Invalid Dropdown prefab: Missing Dropdown script");
                yield break;
            }

            Log("Dropdown prefab is valid");

            serverDropdown = GameObject.Instantiate(prefab, pri.GetComponent<RectTransform>());
            RectTransform ddRT = serverDropdown.GetComponent<RectTransform>();
            ddRT.localPosition += new Vector3(250, 150);
            ddRT.sizeDelta += new Vector2(200, 0);

            //defaultServer.appVersion = GameObject.FindObjectOfType<VRCApplicationSetup>().Method_Public_String_2();
            defaultServer.appVersion = GameObject.FindObjectOfType<VRCApplicationSetup>().appVersion;

            serverList = new List<ServerDef> { defaultServer, ServerDef.DedicatedServer("Slaynash EUW Server", "31.204.91.102", 5055)/*, ServerDef.DedicatedServer("Local Server", "127.0.0.1", 5055)*/ };
            serverDropdown.ClearOptions();
            Il2CppSystem.Collections.Generic.List<string> options = new Il2CppSystem.Collections.Generic.List<string>(serverList.Count);
            foreach (ServerDef serverdef in serverList)
                options.Add(serverdef.name);
            serverDropdown.AddOptions(options);
        }

        private static bool PhotonAppIdPrefix(ref string __result)
        {
            foreach (string text in Environment.GetCommandLineArgs())
            {
                if (text.StartsWith("--photonId="))
                {
                    __result = text.Substring("--photonId=".Length);
                    return false;
                }
            }
            return true;
        }

        private static void CreateAndEnqueueCommandPrefix(ref byte commandType)
        {
            if (commandType == 14 && ss != null && !ss.AppSettings.UseNameServer) // DeliveryMode.ReliableUnsequenced > DeliveryMode.Reliable
                commandType = 6;
            if (commandType == 11 && ss != null && !ss.AppSettings.UseNameServer) // DeliveryMode.UnreliableUnsequenced > DeliveryMode.Unreliable
                commandType = 7;
        }

        private static void GetNewInstancePrefix(ref string tags)
        {
            MelonLogger.Msg("Mono Stacktrace:\n" + new StackTrace().ToString());
            MelonLogger.Msg("Il2Cpp Stacktrace:\n" + new Il2CppSystem.Diagnostics.StackTrace().ToString());
            ServerDef targetServer;
            if (new StackTrace().GetFrame(2).GetMethod().DeclaringType == typeof(PopupRoomInstance))
                targetServer = serverList[serverDropdown.value];
            else
                targetServer = ss.AppSettings.UseNameServer ? ServerDef.CloudServer("", ss.AppSettings.FixedRegion, ss.AppSettings.AppIdRealtime, ss.AppSettings.AppVersion) : ServerDef.DedicatedServer("", ss.AppSettings.Server, ss.AppSettings.Port);

            if (targetServer != defaultServer)
            {
                if (targetServer.cloud)
                    tags += "~cloud(" + targetServer.region + "," + targetServer.appId + "," + targetServer.appVersion + ")";
                else
                    tags += "~server(" + targetServer.address + "," + targetServer.port + ")";
            }

            MelonLogger.Msg("generated instance id: " + tags);
        }

        private static void EnterWorldPostfix(string __1)
        {
            if (__1.Contains("~server("))
            {
                string[] tags = __1.Split('~');
                string server = tags.First(s => s.StartsWith("server("));
                server = server.Substring(7, server.Length - 8);
                string[] addressParts = server.Split(',');
                
                if(addressParts[0] != ss.AppSettings.Server || addressParts[1] != ss.AppSettings.Port.ToString())
                {
                    Log("Switching to server " + addressParts[0] + ":" + addressParts[1]);

                    ss.AppSettings.UseNameServer = false;
                    ss.AppSettings.Server = addressParts[0];
                    ss.AppSettings.Port = int.Parse(addressParts[1]);

                    //VRCFlowNetworkManager.Instance.Method_Public_Void_0();
                    VRCFlowNetworkManager.Instance.Disconnect();
                }
            }
            else if (__1.Contains("~cloud("))
            {
                string[] tags = __1.Split('~');
                string server = tags.First(s => s.StartsWith("cloud("));
                server = server.Substring(6, server.Length - 7);
                string[] addressParts = server.Split(',');

                if (!ss.AppSettings.UseNameServer || ss.AppSettings.FixedRegion != addressParts[0] || ss.AppSettings.AppIdRealtime != addressParts[1] || ss.AppSettings.AppVersion != addressParts[2])
                {
                    Log("Switching to cloud " + addressParts[0] + ":" + addressParts[1] + ":" + addressParts[2]);

                    ss.AppSettings.UseNameServer = true;
                    ss.AppSettings.Server = "";
                    ss.AppSettings.Port = 0;
                    ss.AppSettings.FixedRegion = addressParts[0];
                    ss.AppSettings.AppIdRealtime = addressParts[1];
                    ss.AppSettings.AppVersion = addressParts[2];

                    //VRCFlowNetworkManager.Instance.Method_Public_Void_0();
                    VRCFlowNetworkManager.Instance.Disconnect();
                }
            }
            else
            {
                if (!ss.AppSettings.UseNameServer || ss.AppSettings.FixedRegion != defaultServer.region || ss.AppSettings.AppIdRealtime != defaultServer.appId || ss.AppSettings.AppVersion != defaultServer.appVersion)
                {
                    Log("Switching to " + defaultServer.name);

                    ss.AppSettings.UseNameServer = true;
                    ss.AppSettings.Server = "";
                    ss.AppSettings.Port = 0;
                    ss.AppSettings.FixedRegion = defaultServer.region;
                    ss.AppSettings.AppIdRealtime = defaultServer.appId;
                    ss.AppSettings.AppVersion = defaultServer.appVersion;

                    //ObjectPublicIPhotonPeerListenerObStBoStObCoDiBo2ObUnique loadbalancingclient = PhotonNetwork.field_Public_Static_ObjectPublicIPhotonPeerListenerObStBoStObCoDiBo2ObUnique_0;
                    //PhotonPeerPublicTyDi2ByObUnique loadbalancingpeer = loadbalancingclient.prop_PhotonPeerPublicTyDi2ByObUnique_0;
                    LoadBalancingClient loadbalancingclient = PhotonNetwork.NetworkingClient;
                    LoadBalancingPeer loadbalancingpeer = loadbalancingclient.LoadBalancingPeer;
                    loadbalancingpeer.SerializationProtocolType = SerializationProtocol.GpBinaryV18;

                    //VRCFlowNetworkManager.Instance.Method_Public_Void_0();
                    VRCFlowNetworkManager.Instance.Disconnect();
                }
            }
        }

        private static void Log(string s)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(s);
            //MelonLogger.Msg(ConsoleColor.Blue, s);
            Console.ResetColor();
        }
    }
}
