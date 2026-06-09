using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Dictio.TTS
{
    public static class AudioPlayer
    {
        private static readonly BlockingCollection<byte[]> _queue = new(new ConcurrentQueue<byte[]>());
        private static readonly Thread _playerThread;

        static AudioPlayer()
        {
            _playerThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "AudioPlayerThread"
            };
            _playerThread.Start();
        }

        /// <summary>
        /// Enqueues audio for playback. Drops the request if it waits more than 30 seconds.
        /// </summary>
        public static void Play(byte[] wavBytes)
        {
            // Use a SemaphoreSlim as a "slot ticket" so the caller can wait with a timeout.
            var slot = new SemaphoreSlim(0, 1);

            // Wrap the audio with its slot so the player thread can release it when ready.
            _pendingSlots.Enqueue((wavBytes, slot));

            bool admitted = slot.Wait(TimeSpan.FromMinutes(2));

            if (!admitted)
            {
                // Timed out — mark it cancelled so the player thread skips it.
                lock (_cancelledSlots)
                    _cancelledSlots.Add(slot);
            }
        }

        private static readonly ConcurrentQueue<(byte[] wav, SemaphoreSlim slot)> _pendingSlots = new();
        private static readonly HashSet<SemaphoreSlim> _cancelledSlots = new();

        private static void ProcessQueue()
        {
            while (true)
            {
                // Spin until there's something to process.
                if (!_pendingSlots.TryDequeue(out var item))
                {
                    Thread.Sleep(10);
                    continue;
                }

                var (wavBytes, slot) = item;

                bool isCancelled;
                lock (_cancelledSlots)
                    isCancelled = _cancelledSlots.Remove(slot);

                if (isCancelled)
                    continue; // Skip — caller already gave up.

                // Signal the caller that playback is starting (they've been admitted).
                slot.Release();

                // Play synchronously — next item won't dequeue until this finishes.
                PlayInternal(wavBytes);
            }
        }

        private static void PlayInternal(byte[] wavBytes)
        {
            using var ms = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(ms);
            using var output = new WaveOutEvent();

            output.Init(reader);
            output.Volume = 1f;

            using var finished = new ManualResetEventSlim(false);
            output.PlaybackStopped += (_, _) => finished.Set();

            output.Play();
            finished.Wait();
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

