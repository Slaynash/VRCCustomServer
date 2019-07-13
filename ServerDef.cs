using System.Collections.Generic;

namespace VRCCustomServer
{
    public struct ServerDef
    {
        public string name;
        public bool cloud;

        public string region;
        public string appId;
        public string appVersion;

        public string address;
        public int port;

        public static ServerDef DedicatedServer(string name, string address, int port)
        {
            return new ServerDef()
            {
                cloud = false,
                name = name,
                address = address,
                port = port
            };
        }

        public static ServerDef CloudServer(string name, string region, string appId, string appVersion)
        {
            return new ServerDef()
            {
                cloud = true,
                name = name,
                region = region,
                appId = appId,
                appVersion = appVersion
            };
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(ServerDef))
                return false;

            ServerDef s = (ServerDef)obj;
            return s.cloud == cloud && s.cloud ? (s.region == region && s.appId == appId && s.appVersion == appVersion) : (s.address == address && s.port == port);
        }

        public static bool operator ==(ServerDef s1, object obj) => s1.Equals(obj);
        public static bool operator !=(ServerDef s1, object obj) => !s1.Equals(obj);

        public override int GetHashCode()
        {
            var hashCode = 1737237104;
            hashCode = hashCode * -1521134295 + cloud.GetHashCode();
            if (cloud)
            {
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(region);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(appId);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(appVersion);
            }
            else
            {
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(address);
                hashCode = hashCode * -1521134295 + port.GetHashCode();
            }
            return hashCode;
        }

        public override string ToString()
        {
            return cloud ? "[" + region + "," + appId + "," + appVersion + "]" : "[" + address + "," + port + "]";
        }
    }
}