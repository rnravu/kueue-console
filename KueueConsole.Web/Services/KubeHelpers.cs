namespace KueueConsole.Web.Services;

internal static class KubeHelpers
{
    internal static string ToAge(DateTimeOffset createdAt)
    {
        var span = DateTimeOffset.UtcNow - createdAt.ToUniversalTime();
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m";
        return $"{(int)span.TotalSeconds}s";
    }

    internal static string ParseAge(System.Text.Json.JsonElement metadata)
    {
        if (metadata.TryGetProperty("creationTimestamp", out var ts)
            && DateTimeOffset.TryParse(ts.GetString(), out var created))
            return ToAge(created);
        return "";
    }

    internal static string GetString(System.Text.Json.JsonElement element, string property)
        => element.TryGetProperty(property, out var v)
            && v.ValueKind == System.Text.Json.JsonValueKind.String
                ? v.GetString() ?? ""
                : "";

    internal static int GetInt(System.Text.Json.JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.TryGetInt32(out var i) ? i : 0;

    /// <summary>Derives Running / Pending / Failed / Completed from a workload status block.</summary>
    internal static string DeriveWorkloadStatus(System.Text.Json.JsonElement item)
    {
        bool admitted = false;
        bool finished = false;
        bool failed = false;

        if (!item.TryGetProperty("status", out var status))
            return "Pending";

        if (status.TryGetProperty("admitted", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.True)
            admitted = true;
        if (status.TryGetProperty("finished", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.True)
            finished = true;

        if (status.TryGetProperty("conditions", out var conds) && conds.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var c in conds.EnumerateArray())
            {
                var type = c.TryGetProperty("type", out var t) ? t.GetString() : null;
                var condStatus = c.TryGetProperty("status", out var s) ? s.GetString() : null;
                var reason = c.TryGetProperty("reason", out var r) ? r.GetString() : null;

                if (type == "Admitted" && condStatus == "True") admitted = true;
                if (type == "Finished" && condStatus == "True") finished = true;
                if (type == "Finished" && condStatus == "True"
                    && reason is "Failed" or "Error" or "PodFailed") failed = true;
            }
        }

        if (finished && failed) return "Failed";
        if (finished) return "Completed";
        if (admitted) return "Running";
        return "Pending";
    }
}
