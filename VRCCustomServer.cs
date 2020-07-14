using ExitGames.Client.Photon;
using Harmony;
using MelonLoader;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using VRC.UI;
using PhotonNetwork = ObjectPublicAbstractSealedStPhStObInSeSiObBoStUnique;

[assembly: MelonModGame("VRChat", "VRChat")]
[assembly: MelonModInfo(typeof(VRCCustomServer.VRCCustomServer), "VRCCustomServer", "0.2", "Slaynash")]

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
            harmonyInstance.Patch(typeof(VRCApplicationSetup).GetProperty("prop_String_0", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("PhotonProdAppIdPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            harmonyInstance.Patch(typeof(VRCApplicationSetup).GetProperty("prop_String_1", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("PhotonDevAppIdPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            // Workaround for Unsequanced datas
            harmonyInstance.Patch(typeof(PeerBase).Assembly.GetType("ExitGames.Client.Photon.EnetPeer").GetMethod("CreateAndEnqueueCommand", (BindingFlags)(-1)), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("CreateAndEnqueueCommandPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            // TODO fix for il2cpp //harmonyInstance.Patch(typeof(ApiWorld).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "GetNewInstance" && m.GetParameters().Length == 1), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("GetNewInstancePrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            // TODO fix for il2cpp //harmonyInstance.Patch(typeof(VRCFlowManager).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "EnterWorld" && m.GetParameters().Length == 5), null, new HarmonyMethod(typeof(VRCCustomServer).GetMethod("EnterWorldPostfix", BindingFlags.NonPublic | BindingFlags.Static)));

            Log("Patch done");

            MelonCoroutines.Start(InitAfterFrame());
        }

        private IEnumerator InitAfterFrame()
        {
            yield return null;
            ss = PhotonNetwork.field_Public_Static_ServerSettings_0;
            MelonModLogger.Log("ss: " + ss?.ToString() ?? "null");
            MelonModLogger.Log("AppSettings: " + ss?.AppSettings?.ToString() ?? "null");

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
            }

            MelonModLogger.Log("Waiting for InitUI");

            yield return InitUI();

            MelonModLogger.Log("Done InitUI");
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
                MelonModLogger.LogError("Unable to load dropdown Assetbundle");
                yield break;
            }

            //Load main prefab
            AssetBundleRequest abrMain = request.assetBundle.LoadAssetWithSubAssetsAsync("Assets/Prefabs/Dropdown.prefab");
            while (!abrMain.isDone)
                yield return null;
            if (abrMain.asset == null)
            {
                MelonModLogger.LogError("Unable to load Dropdown prefab from Assetbundle (prefab is null)");
                yield break;
            }
            Dropdown prefab = abrMain.asset.Cast<GameObject>().GetComponent<Dropdown>();

            if (prefab == null)
            {
                MelonModLogger.LogError("Invalid Dropdown prefab: Missing Dropdown script");
                yield break;
            }

            Log("Dropdown prefab is valid");

            serverDropdown = GameObject.Instantiate(prefab, pri.GetComponent<RectTransform>());
            RectTransform ddRT = serverDropdown.GetComponent<RectTransform>();
            ddRT.localPosition += new Vector3(250, 150);
            ddRT.sizeDelta += new Vector2(200, 0);

            defaultServer.appVersion = GameObject.FindObjectOfType<VRCApplicationSetup>().Method_Public_String_2();

            serverList = new List<ServerDef> { defaultServer, ServerDef.DedicatedServer("Slaynash EUW Server", "31.204.91.102", 5055)/*, ServerDef.DedicatedServer("Local Server", "127.0.0.1", 5055)*/ };
            serverDropdown.ClearOptions();
            Il2CppSystem.Collections.Generic.List<string> options = new Il2CppSystem.Collections.Generic.List<string>(serverList.Count);
            foreach (ServerDef serverdef in serverList)
                options.Add(serverdef.name);
            serverDropdown.AddOptions(options);
        }

        private static bool PhotonProdAppIdPrefix(ref string __result)
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

        private static bool PhotonDevAppIdPrefix(ref string __result)
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

                    Resources.FindObjectsOfTypeAll<VRCFlowNetworkManager>()[0].Method_Public_Void_0();
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

                    Resources.FindObjectsOfTypeAll<VRCFlowNetworkManager>()[0].Method_Public_Void_0();
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

                    ObjectPublicIPhotonPeerListenerObStBoStObCoDiBo2ObUnique loadbalancingclient = PhotonNetwork.field_Public_Static_ObjectPublicIPhotonPeerListenerObStBoStObCoDiBo2ObUnique_0;
                    PhotonPeerPublicTyDi2ByObUnique loadbalancingpeer = loadbalancingclient.prop_PhotonPeerPublicTyDi2ByObUnique_0;
                    loadbalancingpeer.SerializationProtocolType = SerializationProtocol.GpBinaryV18;

                    Resources.FindObjectsOfTypeAll<VRCFlowNetworkManager>()[0].Method_Public_Void_0();
                }
            }
        }

        private static void Log(string s)
        {
            MelonModLogger.Log(ConsoleColor.Blue, s);
        }
    }
}
