using ExitGames.Client.Photon;
using Harmony;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using VRC.Core;
using VRC.UI;
using VRLoader.Attributes;
using VRLoader.Modules;

namespace VRCCustomServer
{
    [ModuleInfo("VRCCustomServer", "0.1", "Slaynash")]
    public class VRCCustomServer : VRModule
    {
        private static ServerSettings ss;
        private static ServerDef defaultServer;
        private static Dropdown serverDropdown;
        private static List<ServerDef> serverList;

        private static Type GetPhotonNetworkType => typeof(QuickMenu).Assembly.GetTypes().FirstOrDefault(t => t.GetFields(BindingFlags.Public | BindingFlags.Static).Any(m => m?.FieldType == typeof(ServerSettings)));

        public void Awake()
        {
            Log("Patching Photon*AppId");
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("slaynash.VRCCustomServer");
            harmonyInstance.Patch(typeof(VRCApplicationSetup).GetMethod("get_PhotonProdAppId", BindingFlags.Instance | BindingFlags.Public), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("PhotonProdAppIdPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            harmonyInstance.Patch(typeof(VRCApplicationSetup).GetMethod("get_PhotonDevAppId", BindingFlags.Instance | BindingFlags.Public), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("PhotonDevAppIdPrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            // Workaround for Unsequanced datas
            harmonyInstance.Patch(typeof(PeerBase).Assembly.GetType("ExitGames.Client.Photon.EnetPeer").GetMethod("CreateAndEnqueueCommand", (BindingFlags)(-1)), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("CreateAndEnqueueCommandPrefix", BindingFlags.NonPublic | BindingFlags.Static)));

            harmonyInstance.Patch(typeof(ApiWorld).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "GetNewInstance" && m.GetParameters().Length == 1), new HarmonyMethod(typeof(VRCCustomServer).GetMethod("GetNewInstancePrefix", BindingFlags.NonPublic | BindingFlags.Static)));
            harmonyInstance.Patch(typeof(VRCFlowManager).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "EnterWorld" && m.GetParameters().Length == 5), null, new HarmonyMethod(typeof(VRCCustomServer).GetMethod("EnterWorldPostfix", BindingFlags.NonPublic | BindingFlags.Static)));

            Log("Patch done");
            if (GetPhotonNetworkType != null)
            {
                Log("Found ServerSettings field");
                ss = GetPhotonNetworkType.GetFields(BindingFlags.Public | BindingFlags.Static).First(m => m?.FieldType == typeof(ServerSettings)).GetValue(null) as ServerSettings;

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
            }
            else
            {
                Console.WriteLine("[VRCCustomServer] ServerSettings field not found!");
                return;
            }

            StartCoroutine(InitUI());
        }

        private IEnumerator InitUI()
        {
            PopupRoomInstance[] pris;
            while ((pris = Resources.FindObjectsOfTypeAll<PopupRoomInstance>()).Length == 0)
            {
                yield return null;
            }
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[VRCCustomServer] Unable to load dropdown Assetbundle");
                Console.ForegroundColor = ConsoleColor.White;
                yield break;
            }

            //Load main prefab
            AssetBundleRequest abrMain = request.assetBundle.LoadAssetWithSubAssetsAsync("Assets/Prefabs/Dropdown.prefab");
            while (!abrMain.isDone)
                yield return null;
            if (abrMain.asset == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[VRCCustomServer] Unable to load Dropdown prefab from Assetbundle (prefab is null)");
                Console.ForegroundColor = ConsoleColor.White;
                yield break;
            }
            Dropdown prefab = ((GameObject)abrMain.asset).GetComponent<Dropdown>();

            if (prefab == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid Dropdown prefab: Missing Dropdown script");
                Console.ForegroundColor = ConsoleColor.White;
                yield break;
            }

            Log("Dropdown prefab is valid");

            serverDropdown = GameObject.Instantiate(prefab, pri.GetComponent<RectTransform>());
            RectTransform ddRT = serverDropdown.GetComponent<RectTransform>();
            ddRT.localPosition += new Vector3(250, 150);
            ddRT.sizeDelta += new Vector2(200, 0);

            defaultServer.appVersion = FindObjectOfType<VRCApplicationSetup>().GetGameServerVersion();

            serverList = new List<ServerDef> { defaultServer, ServerDef.DedicatedServer("Slaynash EUW Server", "31.204.91.102", 5055)/*, ServerDef.DedicatedServer("Local Server", "127.0.0.1", 5055)*/ };
            serverDropdown.ClearOptions();
            serverDropdown.AddOptions(serverList.ConvertAll(s => s.name));
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

                    Resources.FindObjectsOfTypeAll<VRCFlowNetworkManager>()[0].Disconnect();
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

                    Resources.FindObjectsOfTypeAll<VRCFlowNetworkManager>()[0].Disconnect();
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

                    object loadbalancingclient = GetPhotonNetworkType.GetFields(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(f => f.FieldType.GetInterfaces().Any(i => i == typeof(IPhotonPeerListener)))?.GetValue(null);
                    object loadbalancingpeer = loadbalancingclient.GetType().BaseType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(f => f.FieldType.IsSubclassOf(typeof(PhotonPeer)))?.GetValue(loadbalancingclient);
                    typeof(PhotonPeer).GetMethod("set_SerializationProtocolType", BindingFlags.Public | BindingFlags.Instance).Invoke(loadbalancingpeer, new object[] { SerializationProtocol.GpBinaryV18 });

                    Resources.FindObjectsOfTypeAll<VRCFlowNetworkManager>()[0].Disconnect();
                }
            }
        }

        private static void Log(string s)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("[VRCCustomServer] " + s);
            UnityEngine.Debug.Log("[<color=blue>VRCCustomServer</color>] " + s);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
