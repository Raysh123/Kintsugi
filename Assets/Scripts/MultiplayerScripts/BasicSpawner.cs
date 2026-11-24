using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;
    [Header("Scene Characters")]
    [SerializeField] private NetworkObject player1;
    [SerializeField] private NetworkObject player2;

    private readonly List<PlayerRef> joinedPlayers = new();

    private bool sceneReady;
    private bool authorityAssigned;

    async void StartGame(GameMode mode)
    {
        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // Create the NetworkSceneInfo from the current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // Start or join (depends on gamemode) a session with a specific name
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }
    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
            {
                StartGame(GameMode.Host);
            }

            if (GUI.Button(new Rect(0, 40, 200, 40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
    }
    private void TryAssignCharacters()
    {
        if (_runner == null || !_runner.IsServer)
            return;

        if (!sceneReady || joinedPlayers.Count < 2)
            return;

        if (authorityAssigned)
            return;

        if (player1 == null || player2 == null)
        {
            Debug.LogError("Assign Player 1 and Player 2 in the Inspector.");
            return;
        }

        // First player (Host) controls Player 1
        player1.AssignInputAuthority(joinedPlayers[0]);

        // Second player (Client) controls Player 2
        player2.AssignInputAuthority(joinedPlayers[1]);

        authorityAssigned = true;

        Debug.Log("Player 1 assigned to: " + joinedPlayers[0]);
        Debug.Log("Player 2 assigned to: " + joinedPlayers[1]);
    }
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) 
    {
        if (!runner.IsServer)
            return;
        if (!joinedPlayers.Contains(player))
        {
            joinedPlayers.Add(player);
        }

        Debug.Log("Player joined: " + player);

        TryAssignCharacters();
    }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) 
    {
        if (!runner.IsServer)
            return;

        int playerIndex = joinedPlayers.IndexOf(player);

        if (playerIndex == 1 && player2 != null)
        {
            player2.RemoveInputAuthority();
        }

        joinedPlayers.Remove(player);

        authorityAssigned = false;

        Debug.Log("Player left: " + player);
    }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) 
    {
        var data = new NetworkInputData();

        if (InputHandler.instance.move > 0)
        {
            data.direction += Vector3.forward;
        }
        input.Set(data);
    }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
    {
        Debug.Log("Runner shutdown: " + shutdownReason);
    }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    [Obsolete]
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
    {
        sceneReady = true;

        Debug.Log("Network scene loaded.");

        TryAssignCharacters();
    }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
}
