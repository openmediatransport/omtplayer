using System.Buffers;
using System.Runtime.InteropServices;
using libomtnet;

namespace omtplayer.audio
{
    internal sealed class AudioPlaybackService : IDisposable
    {
        private readonly Action<string> log;
        private readonly AudioJitterBuffer jitterBuffer = new AudioJitterBuffer();
        private readonly object stateSync = new object();
        private readonly AutoResetEvent workSignal = new AutoResetEvent(false);
        private readonly Thread worker;

        private volatile bool running = true;
        private volatile bool flushRequested = false;
        private volatile bool formatChangePending = false;
        private AudioStreamFormat requestedFormat;
        private int startThresholdFrames = 0;
        private int maxBufferedFrames = 0;
        private bool playbackStarted = false;
        private bool underrunLogged = false;

        public AudioPlaybackService(Action<string> log)
        {
            this.log = log;
            worker = new Thread(PlaybackLoop);
            worker.IsBackground = true;
            worker.Name = "OMT Audio Playback";
            worker.Start();
        }

        public void Submit(OMTMediaFrame frame)
        {
            AudioStreamFormat format = new AudioStreamFormat(frame.SampleRate, frame.Channels, frame.SamplesPerChannel);
            if (!format.IsValid || frame.Data == IntPtr.Zero || frame.DataLength <= 0)
            {
                return;
            }

            int sampleCount = frame.Channels * frame.SamplesPerChannel;
            float[] samples = ArrayPool<float>.Shared.Rent(sampleCount);
            Marshal.Copy(frame.Data, samples, 0, sampleCount);

            bool clearBuffer = false;
            int maxFrames;
            lock (stateSync)
            {
                if (requestedFormat != format)
                {
                    requestedFormat = format;
                    formatChangePending = true;
                    clearBuffer = true;
                    startThresholdFrames = Math.Max(format.SampleRate / 10, format.SamplesPerChannel * 3);
                    maxBufferedFrames = Math.Max(format.SampleRate / 2, format.SamplesPerChannel * 8);
                }

                maxFrames = maxBufferedFrames > 0 ? maxBufferedFrames : Math.Max(format.SampleRate / 2, format.SamplesPerChannel * 8);
            }

            if (clearBuffer)
            {
                jitterBuffer.Clear();
            }

            AudioInputBlock block = new AudioInputBlock(format, samples, sampleCount);
            jitterBuffer.Enqueue(block, maxFrames);
            workSignal.Set();
        }

        public void Reset(string reason)
        {
            lock (stateSync)
            {
                flushRequested = true;
                formatChangePending = false;
                requestedFormat = default;
                playbackStarted = false;
                underrunLogged = false;
            }

            jitterBuffer.Clear();
            log("Audio.Reset: " + reason);
            workSignal.Set();
        }

        private void PlaybackLoop()
        {
            using AlsaAudioOutput output = new AlsaAudioOutput();
            AudioInputBlock? currentBlock = null;
            float[]? interleavedBuffer = null;
            AudioStreamFormat activeFormat = default;

            while (running)
            {
                try
                {
                    if (HandlePendingState(output, ref activeFormat))
                    {
                        currentBlock?.Dispose();
                        currentBlock = null;
                    }

                    if (!activeFormat.IsValid)
                    {
                        workSignal.WaitOne(100);
                        continue;
                    }

                    if (!playbackStarted)
                    {
                        if (jitterBuffer.QueuedFrames < startThresholdFrames)
                        {
                            workSignal.WaitOne(25);
                            continue;
                        }

                        output.Prepare();
                        playbackStarted = true;
                        underrunLogged = false;
                    }

                    if (currentBlock == null)
                    {
                        if (!jitterBuffer.TryDequeue(out currentBlock))
                        {
                            if (!underrunLogged)
                            {
                                log("Audio.BufferUnderrun");
                                underrunLogged = true;
                            }
                            playbackStarted = false;
                            workSignal.WaitOne(25);
                            continue;
                        }
                    }

                    if (currentBlock.Format != activeFormat)
                    {
                        currentBlock.Dispose();
                        currentBlock = null;
                        playbackStarted = false;
                        continue;
                    }

                    interleavedBuffer = EnsureInterleavedBuffer(interleavedBuffer, currentBlock.FrameCount * output.OutputChannels);
                    ConvertPlanarToOutput(currentBlock, interleavedBuffer, output.OutputChannels);

                    int framesRemaining = currentBlock.FrameCount;
                    int frameOffset = 0;
                    while (framesRemaining > 0 && running)
                    {
                        int written = output.Write(interleavedBuffer, frameOffset, framesRemaining);
                        if (written <= 0)
                        {
                            if (!underrunLogged)
                            {
                                log("Audio.BufferUnderrun");
                                underrunLogged = true;
                            }
                            playbackStarted = false;
                            break;
                        }

                        frameOffset += written;
                        framesRemaining -= written;
                    }

                    currentBlock.Dispose();
                    currentBlock = null;
                }
                catch (Exception ex)
                {
                    log("Audio.Error: " + ex.Message);
                    playbackStarted = false;
                    workSignal.WaitOne(250);
                }
            }

            currentBlock?.Dispose();
            if (interleavedBuffer != null)
            {
                ArrayPool<float>.Shared.Return(interleavedBuffer);
            }
        }

        private bool HandlePendingState(AlsaAudioOutput output, ref AudioStreamFormat activeFormat)
        {
            bool clearCurrentBlock = false;
            bool localFlush = false;
            bool localFormatChange = false;
            AudioStreamFormat nextFormat = default;

            lock (stateSync)
            {
                if (flushRequested)
                {
                    localFlush = true;
                    flushRequested = false;
                }

                if (formatChangePending)
                {
                    localFormatChange = true;
                    nextFormat = requestedFormat;
                }
            }

            if (localFlush)
            {
                output.Drop();
                output.Close();
                activeFormat = default;
                clearCurrentBlock = true;
            }

            if (localFormatChange)
            {
                AudioStreamFormat previousFormat = activeFormat;
                if (activeFormat.IsValid)
                {
                    output.Drop();
                    output.Close();
                }

                AlsaOpenResult openResult = output.Open(nextFormat);
                lock (stateSync)
                {
                    if (requestedFormat == nextFormat)
                    {
                        formatChangePending = false;
                    }
                }
                activeFormat = nextFormat;
                playbackStarted = false;
                underrunLogged = false;
                clearCurrentBlock = true;

                log("Audio.DeviceOpened: " + openResult.DeviceName + " " + openResult.SampleRate + "Hz " + openResult.OutputChannels + "ch");
                log("Audio.FormatDetected: " + nextFormat);
                if (openResult.OutputChannels != openResult.InputChannels)
                {
                    log("Audio.Downmix: " + openResult.InputChannels + "ch -> " + openResult.OutputChannels + "ch");
                }

                if (previousFormat.IsValid)
                {
                    log("Audio.RestartedAfterFormatChange: " + previousFormat + " -> " + nextFormat);
                }
            }

            return clearCurrentBlock;
        }

        private static float[] EnsureInterleavedBuffer(float[]? current, int requiredSamples)
        {
            if (current != null && current.Length >= requiredSamples)
            {
                return current;
            }

            if (current != null)
            {
                ArrayPool<float>.Shared.Return(current);
            }

            return ArrayPool<float>.Shared.Rent(requiredSamples);
        }

        private static void ConvertPlanarToOutput(AudioInputBlock block, float[] destination, int outputChannels)
        {
            int samplesPerChannel = block.Format.SamplesPerChannel;
            int sourceChannels = block.Format.Channels;
            float[] source = block.Samples;

            if (outputChannels == sourceChannels)
            {
                for (int sample = 0; sample < samplesPerChannel; sample++)
                {
                    int dstBase = sample * outputChannels;
                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        destination[dstBase + channel] = source[(channel * samplesPerChannel) + sample];
                    }
                }

                return;
            }

            if (outputChannels == 1)
            {
                for (int sample = 0; sample < samplesPerChannel; sample++)
                {
                    float mono = 0.0f;
                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        mono += source[(channel * samplesPerChannel) + sample];
                    }
                    destination[sample] = mono / sourceChannels;
                }

                return;
            }

            for (int sample = 0; sample < samplesPerChannel; sample++)
            {
                float left = source[sample];
                float right = sourceChannels > 1 ? source[samplesPerChannel + sample] : left;

                for (int channel = 2; channel < sourceChannels; channel++)
                {
                    float value = source[(channel * samplesPerChannel) + sample];
                    if (channel == 2)
                    {
                        left += value * 0.70710677f;
                        right += value * 0.70710677f;
                    }
                    else if (channel == 3)
                    {
                        left += value * 0.5f;
                        right += value * 0.5f;
                    }
                    else if ((channel & 1) == 0)
                    {
                        left += value * 0.70710677f;
                    }
                    else
                    {
                        right += value * 0.70710677f;
                    }
                }

                int dstBase = sample * outputChannels;
                destination[dstBase] = left;
                destination[dstBase + 1] = right;
                for (int channel = 2; channel < outputChannels; channel++)
                {
                    destination[dstBase + channel] = 0.0f;
                }
            }
        }

        public void Dispose()
        {
            running = false;
            workSignal.Set();
            worker.Join();
            jitterBuffer.Dispose();
            workSignal.Dispose();
        }
    }
}
