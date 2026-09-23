using System.Text.Json;

namespace osu.Game.EzRealmSync.DllVerifier
{
    /// <summary>
    /// job.json 的极简读取。刻意不复用 Ez 侧 DTO：这个进程里不能出现任何 Ez 程序集。
    /// 属性名忽略大小写，避免调用方序列化策略变化时静默读成默认值。
    /// </summary>
    internal static class JobJson
    {
        public static string RequireString(JsonElement root, string name)
        {
            if (!tryGet(root, name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidOperationException($"job.json 缺少字符串字段 {name}。");

            return value.GetString()!;
        }

        public static int ReadInt(JsonElement root, string name, int fallback)
            => tryGet(root, name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : fallback;

        public static bool ReadBool(JsonElement root, string name, bool fallback)
            => tryGet(root, name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

        private static bool tryGet(JsonElement root, string name, out JsonElement value)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        continue;

                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
