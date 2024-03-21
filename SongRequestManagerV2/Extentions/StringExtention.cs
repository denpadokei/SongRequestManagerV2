using BGLib.Polyglot;

namespace SongRequestManagerV2.Extentions
{
    public static class StringExtention
    {
        public static string LocalizationGetOr(this string key, string defaultValue)
        {
            var localize = Localization.Get(key);
            return localize == key ? defaultValue : localize;
        }
    }
}
