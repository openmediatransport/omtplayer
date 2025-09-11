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
    internal class DRMConnector
    {
        private drmModeConnector connector;
        private DRMEncoder? encoder = null;
        private DRMDevice dev;
        private DRMMode[] modes;

        internal DRMConnector(DRMDevice dev, drmModeConnector connector) {
            this.connector = connector;
            this.dev = dev;
            List<DRMMode> modes = new List<DRMMode>();
            if (IsConnected)
            {
                IntPtr pInfo = connector.modes;
                for (int m = 0; m < connector.count_modes; m++)
                {
                    drmModeModeInfo info = Marshal.PtrToStructure<drmModeModeInfo>(pInfo);
                    modes.Add(new DRMMode(info));
                    pInfo += Marshal.SizeOf<drmModeModeInfo>();
                }
                IntPtr pEnc = DRMUnmanaged.drmModeGetEncoder(dev.Handle, connector.encoder_id);
                if (pEnc != IntPtr.Zero)
                {
                    drmModeEncoder de = Marshal.PtrToStructure<drmModeEncoder>(pEnc);
                    DRMUnmanaged.drmModeFreeEncoder(pEnc);
                    this.encoder = new DRMEncoder(de);
                }
            }
            this.modes = modes.ToArray();
        }
        public bool IsConnected {  get { 
                if (connector.connection == drmModeConnection.DRM_MODE_CONNECTED) return true; 
                return false; 
            } }

        public UInt32 Id { get { return connector.connector_id; } }

        public DRMMode[] Modes {  get { return modes; } }

        public DRMEncoder? Encoder {  get { return encoder;  } }

        public DRMMode? FindNearestMode(int width, int height, double refreshRate, bool interlaced)
        {
            refreshRate = Math.Round(refreshRate, 2);
            double refreshRounded = Math.Round(refreshRate, 0);

            //Exact matches
            foreach (DRMMode mode in modes)
            {
                if (mode.Width == width && mode.Height == height && mode.Interlaced == interlaced)
                {
                    if (mode.RefreshRate == refreshRate)
                    {
                        return mode;
                    }
                }
            }

            //Rounded matches
            foreach (DRMMode mode in modes)
            {
                if (mode.Width == width && mode.Height == height && mode.Interlaced == false)
                {
                    if (mode.RefreshRate == refreshRounded)
                    {
                        return mode;
                    }
                }
            }

            //60FPS fallback
            foreach (DRMMode mode in modes)
            {
                if (mode.Width == width && mode.Height == height && mode.Interlaced == false)
                {
                    if (mode.RefreshRate == 60.0d)
                    {
                        return mode;
                    }
                }
            }

            return null;
        }

    }
}
