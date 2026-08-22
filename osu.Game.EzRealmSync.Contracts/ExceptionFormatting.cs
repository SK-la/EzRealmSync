using System.Text;

namespace osu.Game.EzRealmSync.Contracts
{
    public static class ExceptionFormatting
    {
        public static string SafeFormat(Exception? ex, int maxDepth = 8)
        {
            if (ex == null)
                return string.Empty;

            var sb = new StringBuilder();
            appendException(sb, ex, 0, maxDepth);
            return sb.ToString().TrimEnd();
        }

        public static string TruncateForDisplay(string text, int maxLength = 500)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text[..maxLength] + "…";
        }

        private static void appendException(StringBuilder sb, Exception ex, int depth, int maxDepth)
        {
            if (depth > maxDepth)
                return;

            if (depth > 0)
                sb.AppendLine("--- Inner Exception ---");

            sb.AppendLine(safeGetTypeName(ex));
            sb.AppendLine(safeGetMessage(ex));

            string? stack = safeGetStackTrace(ex);
            if (!string.IsNullOrWhiteSpace(stack))
                sb.AppendLine(stack);

            if (ex.InnerException != null)
                appendException(sb, ex.InnerException, depth + 1, maxDepth);
        }

        private static string safeGetTypeName(Exception ex)
        {
            try
            {
                return ex.GetType().FullName ?? ex.GetType().Name;
            }
            catch
            {
                return "(unknown exception type)";
            }
        }

        private static string safeGetMessage(Exception ex)
        {
            try
            {
                return string.IsNullOrWhiteSpace(ex.Message) ? "(no message)" : ex.Message;
            }
            catch
            {
                return "(failed to read exception message)";
            }
        }

        private static string? safeGetStackTrace(Exception ex)
        {
            try
            {
                return ex.StackTrace;
            }
            catch
            {
                return null;
            }
        }
    }
}
