using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GcaUpdater.Services;

public static class VersionHelper
{
    private static readonly Regex NumericVersionRegex = new(@"\d+(?:\.\d+){0,3}", RegexOptions.Compiled);

    public static Version ParseLoose(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new Version(0, 0, 0, 0);
        }

        var match = NumericVersionRegex.Match(input);
        if (!match.Success)
        {
            return new Version(0, 0, 0, 0);
        }

        var parts = match.Value.Split('.').Select(int.Parse).ToList();
        while (parts.Count < 4)
        {
            parts.Add(0);
        }

        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }

    public static bool IsRemoteNewer(string currentVersion, string remoteVersion)
    {
        return ParseLoose(remoteVersion) > ParseLoose(currentVersion);
    }
}
