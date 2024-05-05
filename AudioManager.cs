using NAudio.Wave;
using OpenVoiceSharp;
using Steamworks;
using SharpAudio;
using System.Diagnostics;

namespace OpenVoiceSharpSteamworks
{
    public enum PlaybackBackend
    {
        NAudio,
        SharpAudio
    }

    public static class AudioManager
    {
        public static PlaybackBackend PlaybackBackend = PlaybackBackend.SharpAudio;

        #region NAudio

        public static WaveFormat WaveFormat = new(VoiceChatInterface.SampleRate, 16, 1); // mono 16 bit 48kHz

        private static Dictionary<SteamId, (BufferedWaveProvider, DirectSoundOut)> WaveOuts = new();

        private static void CreateWaveOut(SteamId steamId)
        {
            if (WaveOuts.ContainsKey(steamId)) return;

            BufferedWaveProvider bufferedWaveProvider = new(WaveFormat);
            DirectSoundOut waveOut = new();

            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            bufferedWaveProvider.ReadFully = true;

            waveOut.Init(bufferedWaveProvider);
            waveOut.Play();

            WaveOuts.TryAdd(steamId, new(bufferedWaveProvider, waveOut));
        }

        private static (BufferedWaveProvider, DirectSoundOut) GetWaveOut(SteamId steamId) => WaveOuts[steamId];

        #endregion NAudio

        #region SharpAudio

        public static AudioEngine AudioEngine = AudioEngine.CreateDefault();
        public static AudioFormat AudioFormat = new()
        {
            SampleRate = VoiceChatInterface.SampleRate,
            Channels = 1,
            BitsPerSample = 16
        };

        private static Dictionary<SteamId, (AudioSource, AudioBuffer)> AudioSources = new();

        private static void CreateAudioSource(SteamId steamId)
        {
            if (AudioSources.ContainsKey(steamId)) return;

            AudioBuffer audioBuffer = AudioEngine.CreateBuffer();
            AudioSource audioSource = AudioEngine.CreateSource();

            AudioSources.TryAdd(steamId, new(audioSource, audioBuffer));
        }

        private static (AudioSource, AudioBuffer) GetAudioSource(SteamId steamId) => AudioSources[steamId];

        #endregion SharpAudio

        #region Common
        public static (dynamic, dynamic) GetAudioPlayback(SteamId steamId)
        {
            return PlaybackBackend switch
            {
                PlaybackBackend.NAudio => GetWaveOut(steamId),
                PlaybackBackend.SharpAudio => GetAudioSource(steamId),
                _ => throw new NotImplementedException(),
            };
        }

        public static void CreateAudioPlayback(SteamId steamId)
        {
            switch (PlaybackBackend)
            {
                case PlaybackBackend.NAudio: CreateWaveOut(steamId); break;
                case PlaybackBackend.SharpAudio: CreateAudioSource(steamId); break;
            }
        }
        public static void RemoveAudioPlayback(SteamId steamId)
        {
            switch (PlaybackBackend)
            {
                case PlaybackBackend.NAudio: WaveOuts.Remove(steamId); break;
                case PlaybackBackend.SharpAudio: AudioSources.Remove(steamId); break;
            }
        }
        public static bool DoesPlaybackExist(SteamId steamId)
        {
            return PlaybackBackend switch
            {
                PlaybackBackend.NAudio => WaveOuts.ContainsKey(steamId),
                PlaybackBackend.SharpAudio => AudioSources.ContainsKey(steamId),
                _ => false,
            };
        }
        public static void QueueDataForPlayback(SteamId steamId, byte[] data, int length)
        {
            switch (PlaybackBackend)
            {
                case PlaybackBackend.NAudio:
                    // get playback and queue samples
                    var (bufferedWaveProvider, _) = GetAudioPlayback(steamId);

                    // add samples
                    bufferedWaveProvider.AddSamples(data, 0, length);
                    break;
                case PlaybackBackend.SharpAudio:
                    // get source and queue samples
                    var (audioSource, audioBuffer) = GetAudioPlayback(steamId);

                    // queue samples
                    audioBuffer.BufferData(data, AudioFormat);
                    audioSource.QueueBuffer(audioBuffer);

                    // play
                    audioSource.Play();

                    break;
            }
        }

        #endregion
    }
}
