using System;
using UnityEngine;

public static class ServerEndpointConfig
{
    private const string DefaultBaseUrl = "https://servergame-production-eee3.up.railway.app";
    private const string PlayerPrefsKey = "serverBaseUrl";

    public static string Resolve(string inspectorBaseUrl)
    {
        string savedUrl = Normalize(PlayerPrefs.GetString(PlayerPrefsKey, string.Empty));
        if (IsUsable(savedUrl))
        {
            return savedUrl;
        }

        string envUrl = Normalize(Environment.GetEnvironmentVariable("TOP_DOWN_MULTI_SERVER_URL"));
        if (IsUsable(envUrl))
        {
            return envUrl;
        }

        envUrl = Normalize(Environment.GetEnvironmentVariable("UNITY_SERVER_URL"));
        if (IsUsable(envUrl))
        {
            return envUrl;
        }

        string candidate = Normalize(inspectorBaseUrl);
        if (IsUsable(candidate))
        {
            return candidate;
        }

        return DefaultBaseUrl;
    }

    public static void Save(string serverBaseUrl)
    {
        string normalized = Normalize(serverBaseUrl);
        if (string.IsNullOrEmpty(normalized))
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
        }
        else
        {
            PlayerPrefs.SetString(PlayerPrefsKey, normalized);
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
}