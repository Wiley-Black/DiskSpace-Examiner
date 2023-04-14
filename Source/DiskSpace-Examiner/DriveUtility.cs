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
    /**
     *  For Google Drive, it looks like this is returning the same information as just DriveInfo.  I could
     *  go back to DriveInfo for simplicity, but keeping this around in case it is needed later.
     */
    public class DriveInfoKernel
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailableToCaller,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        public string Name;
        private ulong FreeBytesAvailableToCaller;
        private ulong TotalNumberOfBytes;
        private ulong TotalNumberOfFreeBytes;
        public DriveInfo OtherInfo;

        public DriveInfoKernel(string driveName)
        {
            this.Name = driveName;

            if (!GetDiskFreeSpaceEx(this.Name, out FreeBytesAvailableToCaller, out TotalNumberOfBytes, out TotalNumberOfFreeBytes))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            OtherInfo = new DriveInfo(driveName);
        }

        public long TotalSize
        {
            get
            {
                return (long)TotalNumberOfBytes;
            }
        }

        public long TotalFreeSpace 
        {
            get
            {
                return (long)TotalNumberOfFreeBytes;
            }
        }

        public long AvailableFreeSpace
        {
            get
            {
                return (long)FreeBytesAvailableToCaller;
            }
        }

        public string VolumeLabel
        {
            get
            {
                return OtherInfo.VolumeLabel;
            }
        }

        public DirectoryInfo RootDirectory { get { return OtherInfo.RootDirectory; } }        
    }
}
