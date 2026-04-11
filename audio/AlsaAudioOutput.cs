using System.Runtime.InteropServices;
using libomtnet;

namespace omtplayer.audio
{
    internal sealed class AlsaOpenResult
    {
        public required string DeviceName { get; init; }

        public required int InputChannels { get; init; }

        public required int OutputChannels { get; init; }

        public required int SampleRate { get; init; }

        public required int SamplesPerChannel { get; init; }

        public required int PeriodFrames { get; init; }

        public required int BufferFrames { get; init; }
    }

    internal sealed class AlsaAudioOutput : IDisposable
    {
        private const string AsoundLibrary = "libasound.so.2";
        private const int SND_PCM_STREAM_PLAYBACK = 0;
        private const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;
        private const int SND_PCM_FORMAT_FLOAT_LE = 14;
        private const int TargetPeriodMilliseconds = 10;
        private const int TargetBufferMilliseconds = 40;

        private static readonly string[] DefaultDevices =
        {
            "plughw:vc4hdmi0,0",
            "sysdefault:CARD=vc4hdmi0",
            "hdmi:CARD=vc4hdmi0,DEV=0",
            "default"
        };

        private IntPtr handle = IntPtr.Zero;

        public int OutputChannels { get; private set; }

        public int SampleRate { get; private set; }

        public int PeriodFrames { get; private set; }

        public int BufferFrames { get; private set; }

        public string DeviceName { get; private set; } = string.Empty;

        public AlsaOpenResult Open(AudioStreamFormat format)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException("ALSA playback is only supported on Linux.");
            }

            Close();

            List<string> errors = new List<string>();
            foreach (string deviceName in GetDeviceCandidates())
            {
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    continue;
                }

                if (TryOpenDevice(deviceName, format, out AlsaOpenResult? result, out string? error))
                {
                    AlsaOpenResult opened = result!;
                    OutputChannels = opened.OutputChannels;
                    SampleRate = opened.SampleRate;
                    PeriodFrames = opened.PeriodFrames;
                    BufferFrames = opened.BufferFrames;
                    DeviceName = opened.DeviceName;
                    return opened;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    errors.Add(deviceName + ": " + error);
                }
            }

            throw new InvalidOperationException("Unable to open ALSA playback device. " + string.Join(" | ", errors));
        }

        public void Prepare()
        {
            if (handle != IntPtr.Zero)
            {
                int hr = snd_pcm_prepare(handle);
                if (hr < 0)
                {
                    throw new InvalidOperationException("snd_pcm_prepare failed: " + GetError(hr));
                }
            }
        }

        public void Drop()
        {
            if (handle != IntPtr.Zero)
            {
                snd_pcm_drop(handle);
            }
        }

        public int Write(float[] samples, int frameOffset, int frameCount)
        {
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("ALSA output is not open.");
            }

            int sampleOffset = frameOffset * OutputChannels;
            int sampleCount = frameCount * OutputChannels;
            if (sampleOffset < 0 || sampleCount < 0 || sampleOffset + sampleCount > samples.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            unsafe
            {
                fixed (float* ptr = &samples[sampleOffset])
                {
                    long written = snd_pcm_writei(handle, (IntPtr)ptr, (ulong)frameCount);
                    if (written < 0)
                    {
                        int recovered = snd_pcm_recover(handle, (int)written, 1);
                        if (recovered < 0)
                        {
                            throw new InvalidOperationException("snd_pcm_writei failed: " + GetError((int)written));
                        }
                        return 0;
                    }

                    return (int)written;
                }
            }
        }

        private IEnumerable<string> GetDeviceCandidates()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            string? envDevice = Environment.GetEnvironmentVariable("OMTPLAYER_ALSA_DEVICE");
            if (!string.IsNullOrWhiteSpace(envDevice) && seen.Add(envDevice))
            {
                yield return envDevice;
            }

            string settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.xml");
            OMTSettings settings = new OMTSettings(settingsPath);
            string configuredDevice = settings.GetString("AudioAlsaDevice", string.Empty);
            if (!string.IsNullOrWhiteSpace(configuredDevice) && seen.Add(configuredDevice))
            {
                yield return configuredDevice;
            }

            foreach (string device in DefaultDevices)
            {
                if (seen.Add(device))
                {
                    yield return device;
                }
            }
        }

        private bool TryOpenDevice(string deviceName, AudioStreamFormat format, out AlsaOpenResult? result, out string? error)
        {
            result = null;
            error = null;
            IntPtr localHandle = IntPtr.Zero;
            IntPtr hwParams = IntPtr.Zero;
            IntPtr swParams = IntPtr.Zero;

            try
            {
                int hr = snd_pcm_open(ref localHandle, deviceName, SND_PCM_STREAM_PLAYBACK, 0);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_hw_params_malloc(ref hwParams);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_hw_params_any(localHandle, hwParams);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_hw_params_set_access(localHandle, hwParams, SND_PCM_ACCESS_RW_INTERLEAVED);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_hw_params_set_format(localHandle, hwParams, SND_PCM_FORMAT_FLOAT_LE);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                uint requestedRate = (uint)format.SampleRate;
                int dir = 0;
                hr = snd_pcm_hw_params_set_rate_near(localHandle, hwParams, ref requestedRate, ref dir);
                if (hr < 0 || requestedRate != (uint)format.SampleRate)
                {
                    error = "Sample rate " + format.SampleRate + "Hz not supported";
                    return false;
                }

                int outputChannels = SelectOutputChannels(localHandle, hwParams, format.Channels);
                if (outputChannels <= 0)
                {
                    error = "No compatible channel layout for " + format.Channels + " channels";
                    return false;
                }

                uint requestedChannels = (uint)outputChannels;
                hr = snd_pcm_hw_params_set_channels_near(localHandle, hwParams, ref requestedChannels);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                outputChannels = (int)requestedChannels;
                if (outputChannels != format.Channels && outputChannels != 2 && outputChannels != 1)
                {
                    error = "Unsupported negotiated channel count: " + outputChannels;
                    return false;
                }

                ulong periodFrames = (ulong)Math.Max(128, Math.Min(Math.Max((format.SampleRate * TargetPeriodMilliseconds) / 1000, 128), format.SamplesPerChannel));
                dir = 0;
                hr = snd_pcm_hw_params_set_period_size_near(localHandle, hwParams, ref periodFrames, ref dir);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                ulong bufferFrames = Math.Max(periodFrames * 3, (ulong)Math.Max((format.SampleRate * TargetBufferMilliseconds) / 1000, format.SamplesPerChannel * 2));
                hr = snd_pcm_hw_params_set_buffer_size_near(localHandle, hwParams, ref bufferFrames);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_hw_params(localHandle, hwParams);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_sw_params_malloc(ref swParams);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_sw_params_current(localHandle, swParams);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_sw_params_set_start_threshold(localHandle, swParams, periodFrames);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_sw_params_set_avail_min(localHandle, swParams, periodFrames);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                hr = snd_pcm_sw_params(localHandle, swParams);
                if (hr < 0)
                {
                    error = GetError(hr);
                    return false;
                }

                handle = localHandle;
                localHandle = IntPtr.Zero;
                result = new AlsaOpenResult
                {
                    DeviceName = deviceName,
                    InputChannels = format.Channels,
                    OutputChannels = outputChannels,
                    SampleRate = format.SampleRate,
                    SamplesPerChannel = format.SamplesPerChannel,
                    PeriodFrames = (int)periodFrames,
                    BufferFrames = (int)bufferFrames
                };

                return true;
            }
            finally
            {
                if (hwParams != IntPtr.Zero)
                {
                    snd_pcm_hw_params_free(hwParams);
                }
                if (swParams != IntPtr.Zero)
                {
                    snd_pcm_sw_params_free(swParams);
                }
                if (localHandle != IntPtr.Zero)
                {
                    snd_pcm_close(localHandle);
                }
            }
        }

        private static int SelectOutputChannels(IntPtr pcm, IntPtr hwParams, int inputChannels)
        {
            if (snd_pcm_hw_params_test_channels(pcm, hwParams, (uint)inputChannels) == 0)
            {
                return inputChannels;
            }
            if (snd_pcm_hw_params_test_channels(pcm, hwParams, 2) == 0)
            {
                return 2;
            }
            if (snd_pcm_hw_params_test_channels(pcm, hwParams, 1) == 0)
            {
                return 1;
            }
            return 0;
        }

        private static string GetError(int errorCode)
        {
            IntPtr ptr = snd_strerror(errorCode);
            return ptr == IntPtr.Zero ? "ALSA error " + errorCode : Marshal.PtrToStringAnsi(ptr) ?? ("ALSA error " + errorCode);
        }

        public void Close()
        {
            if (handle != IntPtr.Zero)
            {
                snd_pcm_close(handle);
                handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Close();
        }

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_open(ref IntPtr pcm, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int stream, int mode);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_close(IntPtr pcm);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_prepare(IntPtr pcm);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_drop(IntPtr pcm);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern long snd_pcm_writei(IntPtr pcm, IntPtr buffer, ulong size);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_malloc(ref IntPtr ptr);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void snd_pcm_hw_params_free(IntPtr obj);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_any(IntPtr pcm, IntPtr hwParams);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params(IntPtr pcm, IntPtr hwParams);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_set_access(IntPtr pcm, IntPtr hwParams, int access);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_set_format(IntPtr pcm, IntPtr hwParams, int format);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_set_rate_near(IntPtr pcm, IntPtr hwParams, ref uint value, ref int dir);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_set_channels_near(IntPtr pcm, IntPtr hwParams, ref uint value);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_set_period_size_near(IntPtr pcm, IntPtr hwParams, ref ulong frames, ref int dir);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_set_buffer_size_near(IntPtr pcm, IntPtr hwParams, ref ulong frames);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_hw_params_test_channels(IntPtr pcm, IntPtr hwParams, uint channels);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_sw_params_malloc(ref IntPtr ptr);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void snd_pcm_sw_params_free(IntPtr obj);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_sw_params_current(IntPtr pcm, IntPtr swParams);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_sw_params(IntPtr pcm, IntPtr swParams);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_sw_params_set_start_threshold(IntPtr pcm, IntPtr swParams, ulong val);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_sw_params_set_avail_min(IntPtr pcm, IntPtr swParams, ulong val);

        [DllImport(AsoundLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr snd_strerror(int errnum);
    }
}
