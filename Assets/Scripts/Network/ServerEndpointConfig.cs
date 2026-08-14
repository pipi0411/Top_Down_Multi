using System;
using UnityEngine;

public static class ServerEndpointConfig
{
    public static string Resolve(string inspectorBaseUrl)
    {
        string savedUrl = Normalize(PlayerPrefs.GetString(ServerUrlSettings.PlayerPrefsKey, string.Empty));
        if (IsDeprecated(savedUrl))
        {
            PlayerPrefs.DeleteKey(ServerUrlSettings.PlayerPrefsKey);
            savedUrl = string.Empty;
        }
        if (IsUsable(savedUrl))
        {
            return savedUrl;
        }

        string envUrl = Normalize(Environment.GetEnvironmentVariable(ServerUrlSettings.PrimaryEnvironmentVariable));
        if (IsUsable(envUrl))
        {
            return envUrl;
        }

        envUrl = Normalize(Environment.GetEnvironmentVariable(ServerUrlSettings.SecondaryEnvironmentVariable));
        if (IsUsable(envUrl))
        {
            return envUrl;
        }

        string candidate = Normalize(inspectorBaseUrl);
        if (IsUsable(candidate))
        {
            return candidate;
        }

        return ServerUrlSettings.ProductionBaseUrl;
    }

    public static void Save(string serverBaseUrl)
    {
        string normalized = Normalize(serverBaseUrl);
        if (string.IsNullOrEmpty(normalized))
        {
            PlayerPrefs.DeleteKey(ServerUrlSettings.PlayerPrefsKey);
        }
        else
        {
            PlayerPrefs.SetString(ServerUrlSettings.PlayerPrefsKey, normalized);
        }

        PlayerPrefs.Save();
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().TrimEnd('/');
    }

    private static bool IsUsable(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        return !string.Equals(url, "http://localhost:3000", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(url, "https://localhost:3000", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(url, "http://127.0.0.1:3000", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(url, "https://127.0.0.1:3000", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeprecated(string url)
    {
        foreach (string deprecatedUrl in ServerUrlSettings.DeprecatedBaseUrls)
        {
            if (string.Equals(url, Normalize(deprecatedUrl), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
