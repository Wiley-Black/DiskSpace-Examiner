using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.IO;
using System.Globalization;

namespace DiskSpace_Examiner
{
    public class DirectorySummary : IXmlSerializable
    {
        public string Name;
        public string FullName;
        public DataSize Size = 0;           // Size, including all subfolders & files
        public DateTime Oldest;             // Oldest date of user access of a file in this folder
        public DateTime Newest;             // Newest date of user access of a file in this folder
        public long TotalFiles = 0;         // Count of number of files, including all files in subfolders
        public long TotalSubfolders = 0;    // Count of number of subfolders, including all subfolders of subfolders
        public bool IsRoot = false;         // Indicates that the directory is the root of a file system (i.e. hard drive root)
        public List<DirectorySummary> Subfolders = new List<DirectorySummary>();

        /// <summary>
        /// LastScanUtc indicates the completion time of the most recent scan.  If it is DateTime.MinValue, then the directory is
        /// listed as part of the hierarchy but has not been scanned at all, or the results are not being retained.  Note that
        /// when an update scan is being made, a fresh DirectorySummary object should be created so that any deleted Subfolders
        /// are not included.
        /// </summary>
        public DateTime LastScanUtc = DateTime.MinValue;

        [XmlIgnore]
        public bool HasBeenScanned { get { return LastScanUtc > DateTime.MinValue; } }

        /// <summary>
        /// Indicates the parent of this directory.  A Parent can be null either when the directory is a root directory of a file system or
        /// when it is the highest level scanned in the current dataset.  Attaching the DirectorySummary to the ScanResultFile can alter
        /// this value, as it then becomes part of a larger hierarchy even if that did not come from the present scan.
        /// </summary>
        [XmlIgnore]
        public DirectorySummary Parent = null;        

        [XmlIgnore]
        private DriveInfo m_Drive;

        [XmlIgnore]
        public DriveInfo Drive
        {
            get
            {
                if (m_Drive == null)
                {
                    DirectoryInfo di = new DirectoryInfo(FullName);
                    m_Drive = new DriveInfo(di.Root.FullName);
                }
                return m_Drive;
            }
        }

        [XmlIgnore]
        public long TotalChildren { get { return TotalFiles + TotalSubfolders; } }

        public DirectorySummary() { }       // For deserialization

        public DirectorySummary(DirectorySummary Parent, DirectoryInfo di)
        {
            Name = di.Name;
            FullName = di.FullName;
            Oldest = di.LastWriteTime;
            Newest = di.LastWriteTime;
            TotalFiles = 0;
            IsRoot = (di.Parent == null);
            this.Parent = Parent;
            if (IsRoot) m_Drive = new DriveInfo(FullName);            
        }

        public class DeltaCounters
        {
            public DataSize Size;
            public long TotalFiles = 0, TotalSubfolders = 0;

            public DeltaCounters() { }
            public static DeltaCounters operator +(DeltaCounters a, DeltaCounters b)
            {
                DeltaCounters ret = new DeltaCounters();
                ret.Size = a.Size + b.Size;
                ret.TotalFiles = a.TotalFiles + b.TotalFiles;
                ret.TotalSubfolders = a.TotalSubfolders + b.TotalSubfolders;
                return ret;
            }
        }

        public void Adjust(DeltaCounters Delta)
        {
            Size += Delta.Size;
            TotalFiles += Delta.TotalFiles;
            TotalSubfolders += Delta.TotalSubfolders;

            // Oldest and Newest are invalid at this time.  They need to be retabulated from the bottom-up.
            Oldest = DateTime.MaxValue;
            Newest = DateTime.MinValue;
        }

        #region "Memory and Disk Conservation Rules (Culling)"

        static DataSize MinimumFolderSize = DataSize.Gigabyte;
        const int MinimumChildCount = 250;

        [XmlIgnore]
        public bool ShouldCull
        {
            get
            {
                return (Size < MinimumFolderSize && TotalChildren < MinimumChildCount);
//                return false;
            }
        }

        public void CullDetails()
        {
            try
            {
                if (IsRoot) Size = Drive.TotalSize - Drive.AvailableFreeSpace;

                /** Cull small subfolders from storage **/

                for (int ii = 0; ii < Subfolders.Count; )
                {
                    if (Subfolders[ii].ShouldCull)                    
                    {
                        Subfolders.RemoveAt(ii);
                    }
                    else
                    {
                        ii++;
                    }
                }
            }
            catch (Exception ex) { throw new Exception("While culling folder information details: " + ex.Message, ex); }
        }

        #endregion

        #region "IXmlSerializable implementation"

        bool ContainsScanData
        {
            get
            {
                if (LastScanUtc > DateTime.MinValue && !ShouldCull) return true;
                foreach (DirectorySummary ds in Subfolders)
                {
                    if (ds.ContainsScanData) return true;
                }
                return false;
            }
        }        

        public XmlSchema GetSchema() { return null; }

        static CultureInfo XmlCulture = CultureInfo.CreateSpecificCulture("en-US");

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("Name", Name);
            writer.WriteAttributeString("FullName", FullName);
            writer.WriteAttributeString("SizeInBytes", Size.Size.ToString());
            writer.WriteAttributeString("Oldest", Oldest.ToUniversalTime().ToString("u", XmlCulture));
            writer.WriteAttributeString("Newest", Newest.ToUniversalTime().ToString("u", XmlCulture));
            writer.WriteAttributeString("TotalFiles", TotalFiles.ToString());
            writer.WriteAttributeString("TotalSubfolders", TotalSubfolders.ToString());
            if (IsRoot) writer.WriteAttributeString("IsRoot", IsRoot.ToString());
            if (LastScanUtc > DateTime.MinValue) writer.WriteAttributeString("LastScanUtc", LastScanUtc.ToUniversalTime().ToString("u", XmlCulture));

            bool StartedList = false;            
            foreach (DirectorySummary Subfolder in Subfolders)
            {
                if (Subfolder.ContainsScanData)
                {
                    if (!StartedList)
                    {
                        writer.WriteStartElement("Subfolders");
                        StartedList = true;
                    }

                    writer.WriteStartElement("DirectorySummary");
                    ((IXmlSerializable)Subfolder).WriteXml(writer);
                    writer.WriteEndElement();
                }
            }
            if (StartedList) writer.WriteEndElement();
        }        

        public void ReadXml(XmlReader reader)
        {
            DateTimeStyles UtcStyle = DateTimeStyles.AdjustToUniversal;

            reader.MoveToContent();
            Name = reader.GetAttribute("Name");
            FullName = reader.GetAttribute("FullName");
            Size = long.Parse(reader.GetAttribute("SizeInBytes"));
            Oldest = DateTime.Parse(reader.GetAttribute("Oldest"), XmlCulture, UtcStyle);
            Newest = DateTime.Parse(reader.GetAttribute("Newest"), XmlCulture, UtcStyle);
            TotalFiles = long.Parse(reader.GetAttribute("TotalFiles"));
            TotalSubfolders = long.Parse(reader.GetAttribute("TotalSubfolders"));
            string IsRootString = reader.GetAttribute("IsRoot");
            if (IsRootString == null) IsRoot = false; else IsRoot = bool.Parse(IsRootString);
            string LastScanUtcString = reader.GetAttribute("LastScanUtc");
            if (LastScanUtcString == null) LastScanUtc = DateTime.MinValue; else LastScanUtc = DateTime.Parse(LastScanUtcString, XmlCulture, UtcStyle);

            bool IsEmpty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (IsEmpty) return;

            for (;;)
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "DirectorySummary") { reader.ReadEndElement(); return; }
                else if (reader.NodeType == XmlNodeType.Element && reader.Name == "Subfolders" && !reader.IsEmptyElement)
                {
                    reader.ReadStartElement();          // Read <Subfolders>
                    for (; ; )
                    {
                        if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "Subfolders") { reader.ReadEndElement(); break; }
                        else if (reader.NodeType == XmlNodeType.Element && reader.Name == "DirectorySummary")
                        {
                            DirectorySummary dsChild = new DirectorySummary();
                            dsChild.ReadXml(reader);
                            Subfolders.Add(dsChild);
                        }
                        else reader.Read();
                    }
                }
                else reader.Read();
            }
        }
        #endregion

        private static int CompareByLargest(DirectorySummary a, DirectorySummary b)
        {
            // Return (-1) b is greater, (0) Equal, (+1) a is greater.

            if (a == null)
            {
                if (b == null) return 0;
                else return -1;
            }
            else
            {
                if (b == null) return 1;
                else
                {
                    if (a.Size == b.Size) return 0;
                    return (a.Size < b.Size) ? 1 : -1;
                }
            }
        }

        private static int CompareByOldest(DirectorySummary a, DirectorySummary b)
        {
            // Return (-1) b is greater, (0) Equal, (+1) a is greater.

            if (a == null)
            {
                if (b == null) return 0;
                else return -1;
            }
            else
            {
                if (b == null) return 1;
                else
                {
                    if (a.Newest == b.Newest) return 0;
                    // Since we are looking for the Oldest, being newest is not greater...
                    // However, "greater" seems to be the opposite sorting order that we want,
                    // so we flip again.
                    return (a.Newest > b.Newest) ? 1 : -1;
                }
            }
        }

        [XmlIgnore]
        public string DisplayName
        {
            get
            {
                if (IsRoot)
                {
                    string VL = Drive.VolumeLabel;
                    if (VL.Length > 0) return VL;
                    return Drive.RootDirectory.FullName + " Drive";
                }
                else
                {
                    if (Name == "$RECYCLE.BIN") return "Recycle Bin";
                    return "\\" + Name;
                }
            }
        }

        [XmlIgnore]
        public DataSize DisplaySize
        {
            get
            {
                if (IsRoot)
                {
                    return Drive.TotalSize - Drive.AvailableFreeSpace;
                }
                else return Size;
            }
        }

        public override string ToString()
        {
            return FullName + " (" + Size.ToFriendlyString() + ")";
        }
    }
}
