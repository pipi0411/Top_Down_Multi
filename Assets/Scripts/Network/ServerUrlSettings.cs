public static class ServerUrlSettings
{
    // Doi server moi thi chi can sua dong nay.
    public const string ProductionBaseUrl = "https://servergame-production-eef4.up.railway.app";

    // Cac URL cu se bi xoa khoi PlayerPrefs neu may nguoi choi tung luu lai.
    public static readonly string[] DeprecatedBaseUrls =
    {
        "https://servergame-production-eee3.up.railway.app",
        "https://servergame-production-7067.up.railway.app"
    };

    public const string PlayerPrefsKey = "serverBaseUrl";

    public const string PrimaryEnvironmentVariable = "TOP_DOWN_MULTI_SERVER_URL";
    public const string SecondaryEnvironmentVariable = "UNITY_SERVER_URL";
}
