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
    internal class DRMBuffer : DRMBase
    {
        private drm_mode_create_dumb buffer;
        private drm_mode_map_dumb buffer_map;
        private UInt32 fb_id = 0;
        private IntPtr mapping;
        private DRMDevice device;

        public DRMBuffer(DRMDevice dev, DRMMode mode)
        {
            this.device = dev;
            buffer = new drm_mode_create_dumb();
            buffer.width = (UInt32)mode.Width;
            buffer.height = (UInt32)mode.Height;
            buffer.bpp = 32;
            int hr = DRMUnmanaged.ioctl(dev.Handle, DRMUnmanaged.DRM_IOCTL_MODE_CREATE_DUMB, ref buffer);
            if (hr != 0) throw new Exception("Unable to create dumb buffer: " + hr);

            UInt32[] handles = { buffer.handle, 0, 0, 0 };
            UInt32[] pitches = { buffer.pitch, 0, 0, 0 };
            UInt32[] offsets = { 0, 0, 0, 0 };

            UInt32 fourcc = DRMUnmanaged.DRM_FORMAT_XRGB8888;
            hr = DRMUnmanaged.drmModeAddFB2(dev.Handle,  buffer.width, buffer.height, fourcc, handles, pitches, offsets, ref fb_id, 0);
            if (hr != 0) throw new Exception("Unable to add frame buffer: " + hr + " " + fourcc.ToString("X"));
        }

        public UInt32 Id { get { return fb_id; } }

        public void CopyFrom(IntPtr src, int srcStride)
        {
            if (srcStride <= 0) return;
            unsafe
            {
                IntPtr dst = Map();
                for (int y = 0; y < buffer.height; y++)
                {
                    Buffer.MemoryCopy((void*)src, (void*)dst, buffer.width * 4, buffer.width * 4);
                    dst += (IntPtr)buffer.pitch;
                    src += (IntPtr)srcStride;
                }
            }
        }

        public IntPtr Map()
        {
            if (mapping == IntPtr.Zero)
            {
                buffer_map.handle = buffer.handle;
                int hr = DRMUnmanaged.ioctl(device.Handle, DRMUnmanaged.DRM_IOCTL_MODE_MAP_DUMB, ref buffer_map);
                if (hr != 0) throw new Exception("Unable to map dumb buffer: " + hr);

                mapping = DRMUnmanaged.mmap(0, (IntPtr)buffer.size, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, device.Handle, (IntPtr)buffer_map.offset);
                if (mapping == (IntPtr)(-1)) throw new Exception("Unable to map buffer");
            }
            return mapping;
        }
        public void UnMap()
        {
            if (mapping != IntPtr.Zero)
            {
                DRMUnmanaged.munmap(mapping, (int)buffer.size);
                mapping = IntPtr.Zero;
            }
        }
        protected override void DisposeInternal()
        {
            UnMap();
            if (fb_id != 0)
            {
                DRMUnmanaged.drmModeRmFB(device.Handle, fb_id);
                fb_id = 0;
            }
            if (buffer.handle != 0)
            {
                drm_mode_destroy_dumb d = new drm_mode_destroy_dumb();
                d.handle = buffer.handle;
                DRMUnmanaged.ioctl(device.Handle, DRMUnmanaged.DRM_IOCTL_MODE_DESTROY_DUMB, ref d);
                buffer.handle = 0;
            }
            base.DisposeInternal();
        }
    }
}
