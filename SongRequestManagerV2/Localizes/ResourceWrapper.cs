using SongRequestManagerV2.Utils;
using System.Reflection;

namespace SongRequestManagerV2.Localizes
{
    internal class ResourceWrapper
    {
        public static string Get(string key)
        {
            if (Utility.IsAprilFool()) {
                var resourceType = typeof(Properties.Resource_kansai).GetProperty(key, BindingFlags.NonPublic | BindingFlags.Static);
                return resourceType == null ? string.Empty : (string)resourceType.GetValue(resourceType, null);
            }
            else {
                var resourceType = typeof(Properties.Resource).GetProperty(key, BindingFlags.NonPublic | BindingFlags.Static);
                return resourceType == null ? string.Empty : (string)resourceType.GetValue(resourceType, null);
            }
        }
    }
}
