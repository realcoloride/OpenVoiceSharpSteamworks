using CSCore;
using CSCore.SoundOut;
using CSCore.Streams;
using NAudio.Wave;
using OpenVoiceSharp;
using Steamworks;
using System.Runtime.InteropServices;

namespace OpenVoiceSharpSteamworks
{
    public enum PlaybackBackend
    {
        NAudio,
        CSCore
    }

    public static class AudioManager
    {
        public static PlaybackBackend PlaybackBackend = PlaybackBackend.CSCore;

        #region NAudio

        private static NAudio.Wave.WaveFormat NAudioWaveFormat = new(VoiceChatInterface.SampleRate, 16, 1); // mono 16 bit 48kHz

        private static Dictionary<SteamId, (BufferedWaveProvider, NAudio.Wave.DirectSoundOut)> WaveOuts = new();

        private static void CreateWaveOut(SteamId steamId)
        {
            if (WaveOuts.ContainsKey(steamId)) return;

            BufferedWaveProvider bufferedWaveProvider = new(NAudioWaveFormat);
            NAudio.Wave.DirectSoundOut waveOut = new();

            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            bufferedWaveProvider.ReadFully = true;

            waveOut.Init(bufferedWaveProvider);
            waveOut.Play();

            WaveOuts.TryAdd(steamId, new(bufferedWaveProvider, waveOut));
        }

        private static (BufferedWaveProvider, NAudio.Wave.DirectSoundOut) GetWaveOut(SteamId steamId) => WaveOuts[steamId];

        #endregion NAudio

        #region CSCore

        private static CSCore.WaveFormat CSCoreWaveFormat = new(VoiceChatInterface.SampleRate, 16, 1); // mono
        

        private static Dictionary<SteamId, (WriteableBufferingSource, CSCore.SoundOut.WasapiOut)> AudioSources = new();

        private static void CreateAudioSource(SteamId steamId)
        {
            if (AudioSources.ContainsKey(steamId)) return;

            WriteableBufferingSource audioSource = new(CSCoreWaveFormat) { FillWithZeros = true };

            CSCore.SoundOut.WasapiOut soundOut = new();
            soundOut.Initialize(audioSource);
            soundOut.Play();

            AudioSources.TryAdd(steamId, new(audioSource, soundOut));
        }

        private static (WriteableBufferingSource, CSCore.SoundOut.WasapiOut) GetAudioSource(SteamId steamId) => AudioSources[steamId];

        #endregion CSCore

        #region Common
        public static (dynamic, dynamic) GetAudioPlayback(SteamId steamId)
        {
            return PlaybackBackend switch
            {
                PlaybackBackend.NAudio => GetWaveOut(steamId),
                PlaybackBackend.CSCore => GetAudioSource(steamId),
                _ => throw new NotImplementedException(),
            };
        }

        public static void CreateAudioPlayback(SteamId steamId)
        {
            switch (PlaybackBackend)
            {
                case PlaybackBackend.NAudio: CreateWaveOut(steamId); break;
                case PlaybackBackend.CSCore: CreateAudioSource(steamId); break;
            }
        }
        public static void RemoveAudioPlayback(SteamId steamId)
        {
            switch (PlaybackBackend)
            {
                case PlaybackBackend.NAudio: WaveOuts.Remove(steamId); break;
                case PlaybackBackend.CSCore: AudioSources.Remove(steamId); break;
            }
        }
        public static bool DoesPlaybackExist(SteamId steamId)
        {
            return PlaybackBackend switch
            {
                PlaybackBackend.NAudio => WaveOuts.ContainsKey(steamId),
                PlaybackBackend.CSCore => AudioSources.ContainsKey(steamId),
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
                case PlaybackBackend.CSCore:
                    // get source and queue samples
                    var (source, _) = GetAudioPlayback(steamId);

                    // queue samples
                    WriteableBufferingSource audioSource = (WriteableBufferingSource)source;

                    audioSource.Write(data, 0, length);

                    break;
            }
        }

        #endregion
    }
}
