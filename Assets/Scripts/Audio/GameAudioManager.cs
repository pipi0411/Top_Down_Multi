using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 0.85f;

    [Header("Footstep")]
    [SerializeField] float footstepInterval = 0.28f;

    [Header("Multiplayer SFX")]
    [SerializeField] float positionalMaxDistance = 12f;
    [SerializeField] float monsterScreamMinInterval = 0.35f;
    [SerializeField] float killSfxMinInterval = 0.08f;
    [SerializeField] float weaponSfxMinInterval = 0.03f;

    AudioSource musicSource;
    AudioSource sfxSource;
    AudioClip introBgm;
    AudioClip[] mapBgms;
    AudioClip finalBossBgm;
    AudioClip winSong;
    AudioClip footStep;
    AudioClip gun;
    AudioClip sword;
    AudioClip kill;
    AudioClip monsterDead;
    AudioClip[] monsterScreams;
    AudioClip[] playerHurts;
    AudioClip playerDead;
    float nextFootstepTime;
    float nextMonsterScreamTime;
    float nextKillSfxTime;
    float nextWeaponSfxTime;
    AudioClip lastMapBgm;
    string currentMusicName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    public static GameAudioManager Ensure()
    {
        if (Instance != null) return Instance;

        GameAudioManager prefab = Resources.Load<GameAudioManager>("Audio/GameAudioManager");
        if (prefab != null)
        {
            Instance = Instantiate(prefab);
            Instance.name = "GameAudioManager";
        }
        else
        {
            GameObject go = new GameObject("GameAudioManager");
            Instance = go.AddComponent<GameAudioManager>();
        }

        DontDestroyOnLoad(Instance.gameObject);
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildSources();
        LoadClips();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        AutoPlayForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoPlayForScene(scene.name);
    }

    void AutoPlayForScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        if (sceneName == "Main Manager" || sceneName == "IntroStory")
        {
            PlayIntroBgm();
            return;
        }

        if (sceneName == "SampleScene")
            PlayRandomMapBgm();
    }

    void BuildSources()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
    }

    void LoadClips()
    {
        introBgm = Load("IntroBGM");
        mapBgms = new[] { Load("BGM1"), Load("BGM2"), Load("BGM3") };
        finalBossBgm = Load("FinalBossBGM");
        winSong = Load("Win Song");
        footStep = Load("FootStep");
        gun = Load("Gun");
        sword = Load("Sword");
        kill = Load("Kill");
        monsterDead = Load("MonsterDead");
        monsterScreams = new[] { Load("MonsterScream"), Load("MonsterScream2"), Load("MonsterScream3") };
        playerHurts = new[] { Load("playerHurt1"), Load("playerhurt2") };
        playerDead = Load("playerDead");
    }

    AudioClip Load(string clipName)
    {
        AudioClip clip = Resources.Load<AudioClip>("Music/" + clipName);
        if (clip == null)
            Debug.LogWarning($"[GameAudioManager] Missing audio clip Resources/Music/{clipName}");
        return clip;
    }

    public void PlayIntroBgm()
    {
        PlayMusic(introBgm, "IntroBGM", true);
    }

    public void PlayRandomMapBgm()
    {
        AudioClip clip = PickMapBgm();
        PlayMusic(clip, clip != null ? clip.name : "MapBGM", true);
    }

    public void PlayFinalBossBgm()
    {
        PlayMusic(finalBossBgm, "FinalBossBGM", true);
    }

    public void PlayWinSong()
    {
        PlayMusic(winSong, "Win Song", false);
    }

    public void PlayFootstep()
    {
        if (Time.unscaledTime < nextFootstepTime) return;
        nextFootstepTime = Time.unscaledTime + footstepInterval;
        PlaySfx(footStep, 0.45f);
    }

    public void PlayWeapon(bool melee)
    {
        PlayWeapon(melee, null);
    }

    public void PlayWeapon(bool melee, Vector3? worldPosition)
    {
        if (Time.unscaledTime < nextWeaponSfxTime) return;
        nextWeaponSfxTime = Time.unscaledTime + weaponSfxMinInterval;
        PlaySfx(melee ? sword : gun, 1f, worldPosition);
    }

    public void PlayKill()
    {
        if (Time.unscaledTime < nextKillSfxTime) return;
        nextKillSfxTime = Time.unscaledTime + killSfxMinInterval;
        PlaySfx(kill);
    }

    public void PlayKill(Vector3 worldPosition)
    {
        if (Time.unscaledTime < nextKillSfxTime) return;
        nextKillSfxTime = Time.unscaledTime + killSfxMinInterval;
        PlaySfx(kill, 1f, worldPosition);
    }

    public void PlayMonsterDead()
    {
        PlaySfx(monsterDead, 0.8f);
    }

    public void PlayMonsterDead(Vector3 worldPosition)
    {
        PlaySfx(monsterDead, 0.8f, worldPosition);
    }

    public void PlayMonsterScream()
    {
        PlayMonsterScream(null);
    }

    public void PlayMonsterScream(Vector3? worldPosition)
    {
        if (Time.unscaledTime < nextMonsterScreamTime) return;
        nextMonsterScreamTime = Time.unscaledTime + monsterScreamMinInterval;
        PlaySfx(Pick(monsterScreams), 0.7f, worldPosition);
    }

    public void PlayPlayerHurt()
    {
        PlaySfx(Pick(playerHurts));
    }

    public void PlayPlayerDead()
    {
        PlaySfx(playerDead);
    }

    void PlayMusic(AudioClip clip, string clipName, bool loop)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.isPlaying && currentMusicName == clipName) return;

        currentMusicName = clipName;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    void PlaySfx(AudioClip clip, float volumeScale = 1f, Vector3? worldPosition = null)
    {
        if (clip == null || sfxSource == null) return;
        float distanceVolume = worldPosition.HasValue ? GetDistanceVolume(worldPosition.Value) : 1f;
        if (distanceVolume <= 0.001f) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale * distanceVolume));
    }

    float GetDistanceVolume(Vector3 worldPosition)
    {
        Transform listener = GetListenerTransform();
        if (listener == null || positionalMaxDistance <= 0f) return 1f;

        float distance = Vector2.Distance(listener.position, worldPosition);
        if (distance >= positionalMaxDistance) return 0f;
        return Mathf.Lerp(1f, 0.12f, distance / positionalMaxDistance);
    }

    Transform GetListenerTransform()
    {
        AudioListener audioListener = FindAnyObjectByType<AudioListener>();
        if (audioListener != null) return audioListener.transform;
        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    AudioClip PickMapBgm()
    {
        if (mapBgms == null || mapBgms.Length == 0) return null;

        AudioClip fallback = null;
        int validCount = 0;

        foreach (AudioClip clip in mapBgms)
        {
            if (clip == null) continue;
            fallback ??= clip;
            validCount++;
        }

        if (validCount <= 0) return null;
        if (validCount == 1)
        {
            lastMapBgm = fallback;
            return fallback;
        }

        for (int i = 0; i < 12; i++)
        {
            AudioClip clip = mapBgms[Random.Range(0, mapBgms.Length)];
            if (clip != null && clip != lastMapBgm)
            {
                lastMapBgm = clip;
                return clip;
            }
        }

        lastMapBgm = fallback;
        return fallback;
    }

    AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) return clip;
        }

        return null;
    }
}
