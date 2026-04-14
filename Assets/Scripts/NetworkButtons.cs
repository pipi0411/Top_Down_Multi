using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkButtons : MonoBehaviour
{
    public static string LatestJoinCode { get; private set; } = string.Empty;

    [Header("Relay")]
    [SerializeField] private int maxConnections = 3;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeText;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "SampleScene";

    private bool servicesInitialized;

    public void StartHost()
    {
        _ = StartHostWithRelayAsync();
    }

    public void StartClient()
    {
        _ = StartClientWithRelayAsync();
    }

    public async void StartHostWithRelayButton()
    {
        await StartHostWithRelayAsync();
    }

    public async void StartClientWithRelayButton()
    {
        await StartClientWithRelayAsync();
    }

    private async Task StartHostWithRelayAsync()
    {
        try
        {
            await EnsureUnityServicesInitializedAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            LatestJoinCode = joinCode;

            UnityTransport transport = GetTransport();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("StartHost failed.");
                return;
            }

            if (joinCodeText != null)
            {
                joinCodeText.text = joinCode;
            }

            Debug.Log($"Multiplayer session host started. Join code: {joinCode}");

            if (NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Host relay flow failed: {ex.Message}");
        }
    }

    private async Task StartClientWithRelayAsync()
    {
        string joinCode = joinCodeInput != null ? joinCodeInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("Join code is empty.");
            return;
        }

        try
        {
            await EnsureUnityServicesInitializedAsync();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = GetTransport();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("StartClient failed.");
                return;
            }

            Debug.Log("Multiplayer session client joined. Waiting for host scene sync.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Client relay flow failed: {ex.Message}");
        }
    }

    private async Task EnsureUnityServicesInitializedAsync()
    {
        if (servicesInitialized)
        {
            return;
        }

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        servicesInitialized = true;
    }

    private UnityTransport GetTransport()
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            throw new InvalidOperationException("UnityTransport component is missing on NetworkManager.");
        }

        return transport;
    }
}