using CSCore.Streams;
using NAudio.Wave;
using OpenVoiceSharp;
using Steamworks;

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

        private static NAudio.Wave.WaveFormat NAudioWaveFormat = new(VoiceChatInterface.SampleRate, 16, 2); // mono 16 bit 48kHz

        private static Dictionary<SteamId, (BufferedWaveProvider, DirectSoundOut)> WaveOuts = new();

        private static void CreateWaveOut(SteamId steamId)
        {
            if (WaveOuts.ContainsKey(steamId)) return;

            BufferedWaveProvider bufferedWaveProvider = new(NAudioWaveFormat);
            DirectSoundOut waveOut = new();

            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            bufferedWaveProvider.ReadFully = true;

            waveOut.Init(bufferedWaveProvider);
            waveOut.Play();

            WaveOuts.TryAdd(steamId, new(bufferedWaveProvider, waveOut));
        }

        private static (BufferedWaveProvider, DirectSoundOut) GetWaveOut(SteamId steamId) => WaveOuts[steamId];

        #endregion NAudio

        #region CSCore

        private static CSCore.WaveFormat CSCoreWaveFormat = new(VoiceChatInterface.SampleRate, 16, 2); // mono
        

        private static Dictionary<SteamId, (WriteableBufferingSource, CSCore.SoundOut.WasapiOut)> AudioSources = new();

        private static void CreateAudioSource(SteamId steamId)
        {
            if (AudioSources.ContainsKey(steamId)) return;

            // create the buffering source to supply our data
            WriteableBufferingSource audioSource = new(CSCoreWaveFormat) { FillWithZeros = true };

            // create an audio source for the playback
            CSCore.SoundOut.WasapiOut soundOut = new();
            soundOut.Initialize(audioSource);
            soundOut.Play();

            // store it for later
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
            // cleaning and disposing
            switch (PlaybackBackend)
            {
                case PlaybackBackend.NAudio:
                    var (provider, directSoundOut) = GetAudioPlayback(steamId);

                    var bufferedWaveProvider = (BufferedWaveProvider)provider;
                    var waveOut = (DirectSoundOut)directSoundOut;

                    bufferedWaveProvider.ClearBuffer();
                    waveOut.Stop();
                    waveOut.Dispose();

                    WaveOuts.Remove(steamId); 
                    break;
                case PlaybackBackend.CSCore:
                    var (source, wasapiOut) = GetAudioPlayback(steamId);

                    var audioSource = (WriteableBufferingSource)source;
                    var soundOut = (CSCore.SoundOut.WasapiOut)wasapiOut;

                    soundOut.Stop();
                    soundOut.Dispose();
                    audioSource.Dispose();

                    AudioSources.Remove(steamId); 
                    break;
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
                    var (audioSource, _) = GetAudioPlayback(steamId);

                    // queue samples
                    audioSource.Write(data, 0, length);
                    break;
            }
        }

        #endregion
    }
}
