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

using System.Runtime.InteropServices;

namespace omtplayer.drm
{
    internal class DRMDevice : DRMBase
    {
        private int fd = 0;
        private Thread? eventsThread;
        private bool eventsRunning = false;
        private DRMPresenter? presenter;
        private DRMUnmanaged.DRM_PAGE_FLIP_HANDLER flipHandler;

        public DRMDevice(string devicePath)
        {
            fd = DRMUnmanaged.open(devicePath, FileOpenFlags.O_RDWR);
            if (fd < 0) throw new Exception("Failed to open " + devicePath);

            UInt64 hasDumb = 0;
            int hr = DRMUnmanaged.drmGetCap(fd, DRMUnmanaged.DRM_CAP_DUMB_BUFFER, ref hasDumb);
            if (hr != 0) throw new Exception("drmGetCap Error: " + hr);
            if (hasDumb == 0) throw new Exception("DRM device does not support dumb buffers, maybe wrong graphics device selected?");

            flipHandler = new DRMUnmanaged.DRM_PAGE_FLIP_HANDLER(page_flip_handler);
        }

        private void page_flip_handler(int fd, uint sequence, uint tv_sec, uint tv_usec, IntPtr user_data)
        {
            if (presenter != null)
            {
                presenter.FlippedEvent(user_data);
            }
        }

        public void StartEvents()
        {
            if (eventsRunning == false)
            {
                eventsRunning = true;
                eventsThread = new Thread(EventsThread);
                eventsThread.IsBackground = true;
                eventsThread.Start();
            }
        }

        public void StopEvents()
        {
            eventsRunning = false;
            if (eventsThread != null)
            {
                eventsThread.Join();
                eventsThread = null;
            }
        }

        public void SetPresenter(DRMPresenter? presenter)
        {
            this.presenter = presenter;
        }

        private void EventsThread()
        {
            try
            {
                drmEventContext ctx = new drmEventContext();
                ctx.version = DRMUnmanaged.DRM_EVENT_CONTEXT_VERSION;
                ctx.page_flip_handler = Marshal.GetFunctionPointerForDelegate(flipHandler);

                poll_fd pfd = new poll_fd();
                pfd.fd = fd;
                pfd.events = DRMUnmanaged.POLLIN;
                poll_fd[] fda = { pfd };

                while (eventsRunning)
                {
                    int hr = DRMUnmanaged.poll(fda, 1, 200);
                    if (hr < 0) break;
                    if (hr > 0)
                    {
                        DRMUnmanaged.drmHandleEvent(fd, ref ctx);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        protected override void DisposeInternal()
        {
            StopEvents();
            if (fd >= 0)
            {
                DRMUnmanaged.close(fd);
                fd = -1;
            }
            base.DisposeInternal();
        }

        public DRMConnector? GetFirstActiveConnector()
        {
            DRMConnector[] connectors = GetConnectors();
            foreach (DRMConnector connector in connectors)
            {
                if (connector.IsConnected)
                {
                    if (connector.Encoder != null)
                    {
                        return connector;
                    } 
                }
            }
            return null;
        }

        public DRMConnector[] GetConnectors()
        {
            List<DRMConnector> connectors = new List<DRMConnector>();
            IntPtr pRes = DRMUnmanaged.drmModeGetResources(fd);
            if (pRes == IntPtr.Zero) throw new Exception("Unable to get DRM resources");
            drmModeRes res = Marshal.PtrToStructure<drmModeRes>(pRes);
            for (int i = 0; i < res.count_connectors; i++)
            {
                int connector = Marshal.ReadInt32(res.connectors, IntPtr.Size * i);
                IntPtr pConnector = DRMUnmanaged.drmModeGetConnector(fd, connector);
                if (pConnector == IntPtr.Zero) continue;

                drmModeConnector c = Marshal.PtrToStructure<drmModeConnector>(pConnector);
                connectors.Add(new DRMConnector(this, c));

                DRMUnmanaged.drmModeFreeConnector(pConnector);
            }
            DRMUnmanaged.drmModeFreeResources(pRes);
            return connectors.ToArray();
        }

        public void SetBuffer(DRMBuffer b, DRMConnector c, DRMEncoder e, DRMMode m)
        {
            UInt32[] connectors = { c.Id };
            drmModeModeInfo info = m.Info;
            int hr = DRMUnmanaged.drmModeSetCrtc(fd, e.CRTCId, b.Id, 0, 0, connectors, 1, ref info);
            if (hr != 0) throw new Exception("Could not set buffer: " + hr);
        }

        internal int Handle {  get { return fd; } }
    }
}
