using System.Buffers;

namespace omtplayer.audio
{
    internal sealed class AudioInputBlock : IDisposable
    {
        public AudioInputBlock(AudioStreamFormat format, float[] samples, int sampleCount)
        {
            Format = format;
            Samples = samples;
            SampleCount = sampleCount;
        }

        public AudioStreamFormat Format { get; }

        public float[] Samples { get; }

        public int SampleCount { get; }

        public int FrameCount => Format.SamplesPerChannel;

        public void Dispose()
        {
            ArrayPool<float>.Shared.Return(Samples);
        }
    }

    internal sealed class AudioJitterBuffer : IDisposable
    {
        private readonly object sync = new object();
        private readonly Queue<AudioInputBlock> blocks = new Queue<AudioInputBlock>();
        private int queuedFrames = 0;

        public int QueuedFrames
        {
            get
            {
                lock (sync)
                {
                    return queuedFrames;
                }
            }
        }

        public void Enqueue(AudioInputBlock block, int maxBufferedFrames)
        {
            lock (sync)
            {
                while (blocks.Count > 0 && queuedFrames + block.FrameCount > maxBufferedFrames)
                {
                    DropOldestBlock();
                }

                blocks.Enqueue(block);
                queuedFrames += block.FrameCount;
            }
        }

        public bool TryDequeue(out AudioInputBlock? block)
        {
            lock (sync)
            {
                if (blocks.Count > 0)
                {
                    block = blocks.Dequeue();
                    queuedFrames -= block.FrameCount;
                    return true;
                }
            }

            block = null;
            return false;
        }

        public void Clear()
        {
            lock (sync)
            {
                while (blocks.Count > 0)
                {
                    blocks.Dequeue().Dispose();
                }
                queuedFrames = 0;
            }
        }

        private void DropOldestBlock()
        {
            AudioInputBlock block = blocks.Dequeue();
            queuedFrames -= block.FrameCount;
            block.Dispose();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
