using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;

namespace DiskSpace_Examiner
{
    public class FileUtility
    {
        public static long GetFileSizeOnDisk(string file)
        {
            FileInfo info = new FileInfo(file);
            return GetFileSizeOnDisk(info);
        }

        public static long GetFileSizeOnDisk(FileInfo info)
        {
#if false
            uint dummy, sectorsPerCluster, bytesPerSector;
            int result = GetDiskFreeSpaceW(info.Directory.Root.FullName, out sectorsPerCluster, out bytesPerSector, out dummy, out dummy);
            if (result == 0) throw new Win32Exception(result);
            uint clusterSize = sectorsPerCluster * bytesPerSector;            
            uint hosize;
            uint losize = GetCompressedFileSizeW(info.FullName, out hosize);
            long size;
            size = (long)hosize << 32 | losize;
            return ((size + clusterSize - 1) / clusterSize) * clusterSize;
#else
            long ret;

            SafeFileHandle safeHandle;
            IntPtr handle = CreateFile(info.FullName, GENERIC_READ, FILE_SHARE_READ,
                                 IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL,
                                 IntPtr.Zero);            
            if (handle != INVALID_HANDLE_VALUE)
            {
                using (safeHandle = new SafeFileHandle(handle, true))
                {
                    ret = 0;
                    if (!GetFileSizeEx(safeHandle, ref ret))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    Debug.Assert(ret >= 0);             // Assertion: file size should always be a positive number.
                    return ret;
                }
            }
            else
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
#endif
        }

        // Define constants.
        protected const uint GENERIC_READ = 0x80000000;
        protected const uint FILE_SHARE_READ = 0x00000001;
        protected const uint OPEN_EXISTING = 3;
        protected const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        protected static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const int INVALID_FILE_SIZE = unchecked((int)0xFFFFFFFF);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        protected static extern IntPtr CreateFile(
                                  string lpFileName, uint dwDesiredAccess,
                                  uint dwShareMode, IntPtr lpSecurityAttributes,
                                  uint dwCreationDisposition, uint dwFlagsAndAttributes,
                                  IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetFileSizeEx(SafeHandle hFile, ref long lpFileSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
           [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
        static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
           out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
           out uint lpTotalNumberOfClusters);
    }
}
