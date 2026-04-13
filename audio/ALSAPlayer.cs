using System.Runtime.InteropServices;

internal class ALSAPlayer : IDisposable
{
    private const string Lib = "libasound.so.2";
    private IntPtr pcmHandle;
    private bool disposedValue;
    private const uint LATENCY_US = 60000; //Where 1,000,000 = 1 second
    private const uint BYTES_PER_SAMPLE = 4;

    private uint channels;
    private uint sampleRate;

    private bool running;

    private float[] srcSamples = { };
    private float[] dstSamples = { };

    private enum _snd_pcm_format_t
    {
        SND_PCM_FORMAT_UNKNOWN = -1,
        SND_PCM_FORMAT_S8 = 0,
        SND_PCM_FORMAT_U8,
        SND_PCM_FORMAT_S16_LE,
        SND_PCM_FORMAT_S16_BE,
        SND_PCM_FORMAT_U16_LE,
        SND_PCM_FORMAT_U16_BE,
        SND_PCM_FORMAT_S24_LE,
        SND_PCM_FORMAT_S24_BE,
        SND_PCM_FORMAT_U24_LE,
        SND_PCM_FORMAT_U24_BE,
        SND_PCM_FORMAT_S32_LE,
        SND_PCM_FORMAT_S32_BE,
        SND_PCM_FORMAT_U32_LE,
        SND_PCM_FORMAT_U32_BE,
        SND_PCM_FORMAT_FLOAT_LE,
        SND_PCM_FORMAT_FLOAT_BE,
    }

    private enum _snd_pcm_access_t
    {
        SND_PCM_ACCESS_MMAP_INTERLEAVED = 0,
        SND_PCM_ACCESS_MMAP_NONINTERLEAVED,
        SND_PCM_ACCESS_MMAP_COMPLEX,
        SND_PCM_ACCESS_RW_INTERLEAVED,
        SND_PCM_ACCESS_RW_NONINTERLEAVED,
        SND_PCM_ACCESS_LAST = SND_PCM_ACCESS_RW_NONINTERLEAVED
    }

    [DllImport(Lib, CharSet = CharSet.Ansi)]
    private static extern int snd_pcm_open(ref IntPtr pcm, string name, int stream, int mode);

    [DllImport(Lib)]
    private static extern int snd_pcm_close(IntPtr pcm);

    [DllImport(Lib)]
    private static extern int snd_pcm_set_params(IntPtr pcm, _snd_pcm_format_t format, _snd_pcm_access_t access, uint channels, uint rate, int soft_resample, uint latency);

    [DllImport(Lib)]
    private static extern int snd_pcm_writei(IntPtr pcm, IntPtr buffer, uint size);

    [DllImport(Lib)]
    private static extern int snd_pcm_writei(IntPtr pcm, [MarshalAs(UnmanagedType.LPArray)] byte[] buffer, uint size);

    [DllImport(Lib)]
    private static extern int snd_pcm_writei(IntPtr pcm, [MarshalAs(UnmanagedType.LPArray)] float[] buffer, uint size);

    [DllImport(Lib)]
    private static extern int snd_pcm_prepare(IntPtr pcm);

    [DllImport(Lib)]
    private static extern int snd_pcm_start(IntPtr pcm);

    [DllImport(Lib)]
    private static extern int snd_pcm_avail_update(IntPtr pcm);

    public ALSAPlayer(string deviceName, uint sampleRate, uint channels)
    {
        this.channels = channels;
        this.sampleRate = sampleRate;
        int hr = snd_pcm_open(ref pcmHandle, deviceName, 0, 0);
        if (hr != 0)
        {
            throw new Exception("Unable to open audio device " + deviceName + ": " + hr);
        }
        hr = snd_pcm_set_params(pcmHandle, _snd_pcm_format_t.SND_PCM_FORMAT_FLOAT_LE, _snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED, channels, sampleRate, 1, LATENCY_US);
        if (hr != 0)
        {
            throw new Exception("Unable to set audio device format: " + deviceName + " " + sampleRate + " " + channels + ": " + hr);
        }
    }

    public uint Channels { get { return channels; } }
    public uint SampleRate { get { return sampleRate; } }

    private void StartStream()
    {
        if (!running)
        {
            int hr = snd_pcm_prepare(pcmHandle);
            if (hr != 0) throw new Exception("Unable to prepare audio device: " + hr);
            hr = snd_pcm_start(pcmHandle);
            if (hr != 0) throw new Exception("Unable to start audio device: " + hr);
            running = true;
        }
    }

    public int WriteInterleaved(IntPtr buffer, uint samplesPerChannel)
    {
        StartStream();
        int frames = snd_pcm_writei(pcmHandle, buffer, samplesPerChannel);
        return frames;
    }
    public int WriteInterleaved(byte[] buffer, uint samplesPerChannel)
    {
        StartStream();
        int frames = snd_pcm_writei(pcmHandle, buffer, samplesPerChannel);
        return frames;
    }
    public int WriteInterleaved(float[] buffer, uint samplesPerChannel)
    {
        StartStream();
        int frames = snd_pcm_writei(pcmHandle, buffer, samplesPerChannel);
        return frames;
    }

    public int WritePlanar(IntPtr buffer, uint samplesPerChannel)
    {
        uint len = samplesPerChannel * channels;
        if (srcSamples.Length < len)
        {
            srcSamples = new float[len];
            dstSamples = new float[len];
        }
        Marshal.Copy(buffer, srcSamples, 0, (int)len);
        int pos = 0;
        for (int i = 0; i < samplesPerChannel; i++)
        {
            for (int c = 0; c < channels; c++)
            {
                dstSamples[pos] = srcSamples[(samplesPerChannel * c) + i];
                pos++;
            }
        }
        return WriteInterleaved(dstSamples, samplesPerChannel);
    }

    public int GetBufferAvailable()
    {
        return snd_pcm_avail_update(pcmHandle);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (pcmHandle != IntPtr.Zero)
            {
                snd_pcm_close(pcmHandle);
                pcmHandle = IntPtr.Zero;
            }
            disposedValue = true;
        }
    }

    ~ALSAPlayer()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
