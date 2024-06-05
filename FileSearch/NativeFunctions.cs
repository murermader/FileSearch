using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace FileSearch;

public class NativeFunctions
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, SHGFI uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    };

    enum SHGFI : uint
    {
        SmallIcon = 0x00000001,
        LargeIcon = 0x00000000,
        Icon = 0x00000100,
        DisplayName = 0x00000200,
        Typename = 0x00000400,
        SysIconIndex = 0x00004000,
        UseFileAttributes = 0x00000010
    }

    private static Dictionary<string, Icon> _iconCache = new();
    
    public static Icon? ExtractIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (_iconCache.TryGetValue(extension, out Icon? icon))
        {
            return icon;
        }
        
        SHFILEINFO shinfo = new SHFILEINFO();
        IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI.Icon | SHGFI.LargeIcon);
        if (shinfo.hIcon != IntPtr.Zero)
        {
            Icon ico = Icon.FromHandle(shinfo.hIcon);
            _iconCache[extension] = Icon.FromHandle(shinfo.hIcon);
            return ico;
        }
        _iconCache[extension] = null;
        return null;
    }
}