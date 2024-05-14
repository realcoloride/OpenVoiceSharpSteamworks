using OpenVoiceSharp;
using OpenVoiceSharpSteamworks;
using Steamworks;
using Steamworks.Data;

// play back?
const bool DisableLoopback = false;
AudioManager.PlaybackBackend = PlaybackBackend.CSCore;

Lobby? lobby = null;
SteamClient.Init(480);

// steam
// store playback naudio objects
SteamMatchmaking.OnLobbyMemberJoined += (lobby, friend) =>
{
    if (friend.Id == SteamClient.SteamId) return;

    Console.WriteLine($"{friend.Name} requests to join");
    SteamNetworking.AcceptP2PSessionWithUser(friend.Id);

    Console.WriteLine($"{friend.Name} joined");
    AudioManager.CreateAudioPlayback(friend.Id);
};
SteamMatchmaking.OnLobbyMemberLeave += (lobby, friend) =>
{
    if (!AudioManager.DoesPlaybackExist(friend.Id)) return;

    Console.WriteLine($"{friend.Name} left");

    // clean garbage
    AudioManager.RemoveAudioPlayback(friend.Id);
};

void SetupLobby()
{
    // get past members and assign them their waveouts
    foreach (var member in lobby?.Members)
        AudioManager.CreateAudioPlayback(member.Id);
}
SteamMatchmaking.OnLobbyEntered += (joinedLobby) =>
{
    Console.WriteLine("Joined lobby");

    // set to current lobby
    lobby = joinedLobby;

    // setup
    SetupLobby();
};
SteamMatchmaking.OnLobbyCreated += (_, createdLobby) =>
{
    Console.WriteLine("Created lobby");

    // set to current lobby
    lobby = createdLobby;

    // setup
    SetupLobby();
};

SteamFriends.OnGameLobbyJoinRequested += async (joinedLobby, friend) =>
{
    Console.WriteLine("Accepted invite");

    // set to current lobby
    lobby = joinedLobby;

    // setup
    await joinedLobby.Join();
    SetupLobby();
};

// voice chat
VoiceChatInterface voiceChatInterface = new(16000);
BasicMicrophoneRecorder microphoneRecorder = new();

microphoneRecorder.DataAvailable += (pcmData, length) =>
{
    if (lobby == null) return;

    // encode packet
    (byte[] encodedData, int encodedLength) = voiceChatInterface.SubmitAudioData(pcmData, length);

    // send packet to everyone
    foreach (var member in lobby?.Members)
    {
        AudioManager.CreateAudioPlayback(member.Id);
        SteamNetworking.SendP2PPacket(member.Id, encodedData, encodedLength, 0, P2PSend.Reliable);
    }
};

if (Environment.GetCommandLineArgs().Contains("--host"))
{
    var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(4);
    if (createLobbyOutput == null) return;
    lobby = createLobbyOutput.Value;

    lobby?.SetPublic();
    lobby?.SetJoinable(true);

    Console.WriteLine("--host specified, creating lobby");

    await lobby?.Join();
}

microphoneRecorder.StartRecording();

void HandleMessageFrom(SteamId steamId, byte[] data)
{
    // avoid receiving messages from self
    if (DisableLoopback && steamId == SteamClient.SteamId) return;

    (byte[] decodedData, int decodedLength) = voiceChatInterface.WhenDataReceived(data, data.Length);
    AudioManager.QueueDataForPlayback(steamId, decodedData, decodedLength);
}

while (true)
{
    SteamClient.RunCallbacks();

    while (SteamNetworking.IsP2PPacketAvailable())
    {
        var packet = SteamNetworking.ReadP2PPacket();
        if (!packet.HasValue) continue;

        HandleMessageFrom(packet.Value.SteamId, packet.Value.Data);
    }
}