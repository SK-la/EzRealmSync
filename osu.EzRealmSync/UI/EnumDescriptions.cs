using System.ComponentModel;
using System.Reflection;

namespace osu.EzRealmSync.UI
{
    internal static class EnumDescriptions
    {
        public static string Get<T>(T value) where T : struct, Enum
        {
            var field = typeof(T).GetField(value.ToString()!)!;
            return field.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString()!;
        }
    }
}
