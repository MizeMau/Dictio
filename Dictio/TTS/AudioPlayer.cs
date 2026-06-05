using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dictio.TTS
{
    public class AudioPlayer
    {
        public static void Play(byte[] wavBytes)
        {
            using var ms = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(ms);
            using var output = new WaveOutEvent();

            output.Init(reader);
            output.Volume = 1f; // Max volume
                                  // Use a ManualResetEvent so we can wait for playback to finish cleanly.
            using var finished = new ManualResetEventSlim(false);
            output.PlaybackStopped += (_, _) => finished.Set();

            output.Play();
            finished.Wait();
        }

        /// <summary>
        /// Saves <paramref name="wavBytes"/> to <paramref name="filePath"/>.
        /// </summary>
        public static async Task SaveAsync(byte[] wavBytes, string filePath, CancellationToken ct = default)
        {
            await File.WriteAllBytesAsync(filePath, wavBytes, ct);
        }
    }
}

///// <summary>
///// Plays WAV bytes at amplified volume.
///// <paramref name="amplification"/> is a multiplier applied at the sample level:
/////   1.0 = unity gain (original volume)
/////   2.0 = double amplitude (~+6 dB)
/////   4.0 = quadruple amplitude (~+12 dB)
///// Values above ~3–4 will start to clip on loud source audio.
///// </summary>
//public static void Play(byte[] wavBytes, float amplification = 2.0f)
//{
//    using var ms = new MemoryStream(wavBytes);
//    using var reader = new WaveFileReader(ms);

//    // Convert to float samples so we can boost without integer overflow
//    using var sampleChannel = new SampleChannel(reader)
//    {
//        Volume = amplification   // SampleChannel.Volume goes above 1.0 — this is the boost
//    };

//    // Convert back to the format WaveOutEvent expects
//    using var provider = new SampleToWaveProvider16(sampleChannel);
//    using var output = new WaveOutEvent { Volume = 1f };

//    output.Init(provider);

//    using var finished = new ManualResetEventSlim(false);
//    output.PlaybackStopped += (_, _) => finished.Set();

//    output.Play();
//    finished.Wait();
//}

