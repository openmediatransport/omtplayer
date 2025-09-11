/*
* MIT License
*
* Copyright (c) 2025 Open Media Transport Contributors
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*
*/

namespace omtplayer.drm
{
    internal class DRMPresenter : DRMBase
    {
        private DRMDevice device;
        private DRMMode mode;
        private DRMConnector connector;
        private DRMBuffer[] buffers;
        private Queue<DRMBuffer> presentQueue;
        private Queue<DRMBuffer> writeQueue;

        private DRMBuffer? frontBuffer;

        private bool presentEmpty = false;

        private UInt32 crtcId = 0;

        public DRMPresenter(DRMDevice dev, DRMConnector connector, DRMMode mode, int frameCount)
        {
            if (frameCount < 2) frameCount = 2;

            if (connector.IsConnected == false) { throw new Exception("DRM Connector is not ready"); }
            if (connector.Encoder == null) { throw new Exception("DRM Encoder is not available");  }

            this.buffers = new DRMBuffer[frameCount];
            this.presentQueue = new Queue<DRMBuffer>();
            this.writeQueue = new Queue<DRMBuffer>();
            this.connector = connector;
            this.mode = mode;
            this.device = dev;
            for (int i = 0; i < frameCount; i++)
            {
                this.buffers[i] = new DRMBuffer(dev, mode);
                this.writeQueue.Enqueue(this.buffers[i]);
            }
            DRMBuffer b = this.writeQueue.Dequeue();
            dev.SetBuffer(b, this.connector, this.connector.Encoder, this.mode);
            crtcId = this.connector.Encoder.CRTCId;

            Flip(b);
        }

        public DRMMode Mode { get { return mode; } }

        public void Enqueue(IntPtr src, int stride)
        {
            DRMBuffer? b = null;
            lock (writeQueue)
            {
                if (writeQueue.Count > 0)
                {
                    b = writeQueue.Dequeue();
                }
            }
            if (b != null)
            {
                b.CopyFrom(src, stride);
                lock (presentQueue)
                {
                    if (presentEmpty)
                    {
                        presentEmpty = false;
                        Flip(b);
                    } else
                    {
                        presentQueue.Enqueue(b);
                    }  
                }
            } else
            {
                Console.WriteLine("Enqueue.Full");
            }
        }

        private bool Flip(DRMBuffer buffer)
        {
            int hr = DRMUnmanaged.drmModePageFlip(device.Handle, crtcId, buffer.Id, DRMUnmanaged.DRM_MODE_PAGE_FLIP_EVENT, (IntPtr)buffer.Id);
            if (hr != 0)
            {
                Console.WriteLine("Flip.Skip");
                return false;
            } else
            {
                return true;
            }
        }

        internal void FlippedEvent(IntPtr user_data)
        {
            if (frontBuffer != null)
            {
                lock (writeQueue)
                {
                    writeQueue.Enqueue(frontBuffer);
                }
                frontBuffer = null;                
            }
            foreach (DRMBuffer buffer in this.buffers)
            {
                if (buffer.Id == (uint)user_data)
                {
                    frontBuffer = buffer;
                    break;
                }
            }
            lock (presentQueue)
            {
                if (presentQueue.Count > 0)
                {
                    DRMBuffer b = presentQueue.Dequeue();
                    Flip(b);
                } else
                {
                    presentEmpty = true;
                }
            }
        }

        protected override void DisposeInternal()
        {
            if (buffers != null)
            {
                foreach (DRMBuffer buffer in buffers)
                {
                    if (buffer != null)
                    {
                        buffer.Dispose();
                    }
                }
            }
            if (presentQueue != null)
            {
                presentQueue.Clear();
            }
            if (writeQueue != null)
            {
                writeQueue.Clear();
            }
            base.DisposeInternal();
        }
    }
}
