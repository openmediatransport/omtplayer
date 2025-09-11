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
    internal class DRMMode
    {
        private drmModeModeInfo info;
        private double refreshRate;
        private bool interlaced = false;

        public DRMMode(drmModeModeInfo info)
        {
            this.info = info;
            double rate = 1;
            this.refreshRate = Math.Round(((double)info.clock * 1000.0D / ((double)info.htotal * (double)info.vtotal)) * rate, 2);
            if ((info.flags & DRMUnmanaged.DRM_MODE_FLAG_INTERLACE) == DRMUnmanaged.DRM_MODE_FLAG_INTERLACE)
            {
                interlaced = true;
            }
        }

        internal drmModeModeInfo Info { get { return info; } }

        public double RefreshRate { get { return refreshRate; } }

        public int Width {  get { return info.hdisplay; } }
        public int Height { get { return info.vdisplay; } }

        public override string? ToString()
        {
            if (Interlaced)
            {
                return Width + "x" + Height + " " + RefreshRate + "i";
            } else
            {
                return Width + "x" + Height + " " + RefreshRate + "p";
            }            
        }

        public bool Interlaced {  get {
                return interlaced;
            } }

    }
}
