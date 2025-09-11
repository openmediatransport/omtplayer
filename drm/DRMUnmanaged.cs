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
    public enum FileOpenFlags
    {
        O_RDONLY = 0x00,
        O_RDWR = 0x02,
        O_NONBLOCK = 0x800,
        O_SYNC = 0x101000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drmModeRes
    {
        public int count_fbs;
        public IntPtr fbs;
        public int count_crtcs;
        public IntPtr crtcs;
        public int count_connectors;
        public IntPtr connectors;
        public int count_encoders;
        public IntPtr encoders;
        public UInt32 min_width;
        public UInt32 max_width;
        public UInt32 min_height;
        public UInt32 max_height;
    }

    public enum drmModeConnection
    {
        DRM_MODE_CONNECTED = 1,
        DRM_MODE_DISCONNECTED = 2,
        DRM_MODE_UNKNOWNCONNECTION = 3
    }

    public enum drmModeSubPixel
    {
        DRM_MODE_SUBPIXEL_UNKNOWN = 1,
        DRM_MODE_SUBPIXEL_HORIZONTAL_RGB = 2,
        DRM_MODE_SUBPIXEL_HORIZONTAL_BGR = 3,
        DRM_MODE_SUBPIXEL_VERTICAL_RGB = 4,
        DRM_MODE_SUBPIXEL_VERTICAL_BGR = 5,
        DRM_MODE_SUBPIXEL_NONE = 6
    }

    
    [StructLayout(LayoutKind.Sequential)]
    public struct drmModeConnector
    {
        public UInt32 connector_id;
        public UInt32 encoder_id;
        public UInt32 connector_type;
        public UInt32 connector_type_id;
        public drmModeConnection connection;
        public UInt32 mmWidth;
        public UInt32 mmHeight;
        public drmModeSubPixel subPixel;
        public int count_modes;
        public IntPtr modes;
        public int count_props;
        public IntPtr props;
        public IntPtr props_values;
        public int count_encoders;
        public IntPtr encoders;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drmModeEncoder
    {
        public UInt32 encoder_id;
        public UInt32 encoder_type;
        public UInt32 crtc_id;
        public UInt32 possible_crtcs;
        public UInt32 possible_clones;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drmModeModeInfo
    {
        public UInt32 clock;

        public UInt16 hdisplay;
        public UInt16 hsync_start;
        public UInt16 hsync_end;
        public UInt16 htotal;
        public UInt16 hskew;

        public UInt16 vdisplay;
        public UInt16 vsync_start;
        public UInt16 vsync_end;
        public UInt16 vtotal;
        public UInt16 vscan;

        public UInt32 vrefresh;

        public UInt32 flags;
        public UInt32 type;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] name;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drm_mode_create_dumb
    {
        public UInt32 height;
        public UInt32 width;
        public UInt32 bpp;
        public UInt32 flags;
        public UInt32 handle;
        public UInt32 pitch;
        public UInt64 size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drm_mode_map_dumb
    {
        public UInt32 handle;
        public UInt32 pad;
        public UInt64 offset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drm_mode_destroy_dumb
    {
        public UInt32 handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct poll_fd
    {
        public int fd;
        public short events;
        public short revents;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct drmEventContext
    {
        public int version;
        public IntPtr vblank_handler;
        public IntPtr page_flip_handler;
        public IntPtr page_flip_handler2;
        public IntPtr sequence_handler;
    }

    [Flags]
    public enum MemoryMappedProtections
    {
        PROT_NONE = 0x0,
        PROT_READ = 0x1,
        PROT_WRITE = 0x2,
        PROT_EXEC = 0x4
    }

    [Flags]
    public enum MemoryMappedFlags
    {
        MAP_SHARED = 0x01,
        MAP_PRIVATE = 0x02,
        MAP_FIXED = 0x10
    }

    internal class DRMUnmanaged
    {
        private const string DLL_PATH = "libc";
        private const string DLL_PATH_DRM = "libdrm.so.2";

        const int _IOC_NONE = 0;
        const int _IOC_WRITE = 1;
        const int _IOC_READ = 2;
        const int _IOC_SIZEBITS = 14;
        const int _IOC_NRBITS = 8;
        const int _IOC_NRSHIFT = 0;
        const int _IOC_TYPEBITS = 8;
        const int _IOC_TYPESHIFT = _IOC_NRSHIFT + _IOC_NRBITS;
        const int _IOC_SIZESHIFT = _IOC_TYPESHIFT + _IOC_TYPEBITS;
        const int _IOC_DIRSHIFT = _IOC_SIZESHIFT + _IOC_SIZEBITS;
        internal static uint _IOC(uint dir, uint type, uint nr, uint size)
           => ((dir) << _IOC_DIRSHIFT) | ((type) << _IOC_TYPESHIFT) | ((nr) << _IOC_NRSHIFT) | ((size) << _IOC_SIZESHIFT);

        public static uint DRM_IOCTL_MODE_CREATE_DUMB = _IOC(_IOC_READ | _IOC_WRITE, 'd', 0xB2, 32);
        public static uint DRM_IOCTL_MODE_MAP_DUMB = _IOC(_IOC_READ | _IOC_WRITE, 'd', 0xB3, 16);
        public static uint DRM_IOCTL_MODE_DESTROY_DUMB = _IOC(_IOC_READ | _IOC_WRITE, 'd', 0xB4, 4);

        public const UInt64 DRM_CAP_DUMB_BUFFER = 1;
        public const int DRM_EVENT_CONTEXT_VERSION = 4;
        public const int DRM_MODE_PAGE_FLIP_EVENT = 1;

        public const UInt32 DRM_MODE_FLAG_INTERLACE = (1 << 4);
        public const UInt32 DRM_MODE_FLAG_DBLSCAN = (1 << 5);

        public const UInt32 DRM_FORMAT_ARGB8888 = 0x34325241;
        public const UInt32 DRM_FORMAT_XRGB8888 = 0x34325258;

        public const int POLLIN = 0x001;

        public delegate void DRM_PAGE_FLIP_HANDLER(int fd, uint sequence, uint tv_sec, uint tv_usec, IntPtr user_data);

        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, FileOpenFlags flags);
        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern int close(int fd);
        [DllImport(DLL_PATH)]
        public static extern int poll([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] poll_fd[] fds, int nfds, int timeout);
        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern int ioctl(int fd, uint request, ref drm_mode_create_dumb c);
        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern int ioctl(int fd, uint request, ref drm_mode_map_dumb c);
        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern int ioctl(int fd, uint request, ref drm_mode_destroy_dumb c);
        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern IntPtr mmap(IntPtr addr, IntPtr length, MemoryMappedProtections prot, MemoryMappedFlags flags, int fd, IntPtr offset);
        [DllImport(DLL_PATH, SetLastError = true)]
        public static extern IntPtr munmap(IntPtr addr, IntPtr length);

        [DllImport(DLL_PATH_DRM)]
        public static extern int drmGetCap(int fd, UInt64 capability, ref UInt64 value);
        [DllImport(DLL_PATH_DRM)]
        public static extern IntPtr drmModeGetResources(int fd);
        [DllImport(DLL_PATH_DRM)]
        public static extern void drmModeFreeResources(IntPtr ptr);
        [DllImport(DLL_PATH_DRM)]
        public static extern int drmHandleEvent(int fd, ref drmEventContext evctx);
        [DllImport(DLL_PATH_DRM)]
        public static extern IntPtr drmModeGetConnector(int fd, int connectorId);
        [DllImport(DLL_PATH_DRM)]
        public static extern void drmModeFreeConnector(IntPtr ptr);
        [DllImport(DLL_PATH_DRM)]
        public static extern int drmModeAddFB2(int fd, UInt32 width, UInt32 height, UInt32 pixel_format, [MarshalAs(UnmanagedType.LPArray, SizeConst=4)] UInt32[] bo_handles, [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] UInt32[] pitches, [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] UInt32[] offsets, ref UInt32 buf_id, UInt32 flags);
        [DllImport(DLL_PATH_DRM)]
        public static extern int drmModeRmFB(int fd, UInt32 bufferId);
        [DllImport(DLL_PATH_DRM)]
        public static extern int drmModePageFlip(int fd, UInt32 crtc_id, UInt32 fb_id, UInt32 flags, IntPtr user_data);
        [DllImport(DLL_PATH_DRM)]
        public static extern int drmModeSetCrtc(int fd,  UInt32 crtcId, UInt32 bufferId, UInt32 x, UInt32 y, [MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] UInt32[] connectors, int count, ref drmModeModeInfo mode);
        [DllImport(DLL_PATH_DRM)]
        public static extern IntPtr drmModeGetEncoder(int fd, UInt32 encoder_id);
        [DllImport(DLL_PATH_DRM)]
        public static extern void drmModeFreeEncoder(IntPtr ptr);

    }
}
