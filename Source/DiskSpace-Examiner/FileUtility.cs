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
#if true
            uint fattr = GetFileAttributesW(info.FullName);
            if ((fattr & FILE_ATTRIBUTE_REPARSE_POINT) != 0) throw new Exception("Unable to determine file size for a reparse point.");

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
            // The following code doesn't work on compressed files or certain soft-linked files, i.e. those referenced by OneDrive but
            // not presently stored locally.

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
        protected const uint FILE_ATTRIBUTE_READONLY                = 0x1;
        protected const uint FILE_ATTRIBUTE_HIDDEN                  = 0x2;
        protected const uint FILE_ATTRIBUTE_SYSTEM                  = 0x4;
        protected const uint FILE_ATTRIBUTE_DIRECTORY               = 0x10;
        protected const uint FILE_ATTRIBUTE_ARCHIVE                 = 0x20;
        protected const uint FILE_ATTRIBUTE_DEVICE                  = 0x40;
        protected const uint FILE_ATTRIBUTE_NORMAL                  = 0x80;
        protected const uint FILE_ATTRIBUTE_TEMPORARY               = 0x100;
        protected const uint FILE_ATTRIBUTE_SPARSE_FILE             = 0x200;
        protected const uint FILE_ATTRIBUTE_REPARSE_POINT           = 0x400;
        protected const uint FILE_ATTRIBUTE_COMPRESSED              = 0x800;                                
        protected const uint FILE_ATTRIBUTE_OFFLINE                 = 0x1000;        
        protected const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED     = 0x2000;
        protected const uint FILE_ATTRIBUTE_ENCRYPTED               = 0x4000;
        protected const uint FILE_ATTRIBUTE_INTEGRITY_STREAM        = 0x8000;
        protected const uint FILE_ATTRIBUTE_VIRTUAL                 = 0x10000;
        protected const uint FILE_ATTRIBUTE_NO_SCRUB_DATA           = 0x20000;
        protected const uint FILE_ATTRIBUTE_RECALL_ON_OPEN          = 0x40000;
        protected const uint FILE_ATTRIBUTE_PINNED                  = 0x80000;
        protected const uint FILE_ATTRIBUTE_UNPINNED                = 0x100000;
        protected const uint FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS   = 0x400000;                                                                        
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

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint GetFileAttributesW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName);        
    }
}
