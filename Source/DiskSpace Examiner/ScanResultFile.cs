using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

using System.Diagnostics;

namespace DiskSpace_Examiner
{
    public class ScanResultFile
    {
        /// <summary>
        /// Roots contains a hierarchal listing of all previous results that are retained (smaller folders are reduced to a
        /// summary).  There can be more than one entry at the top level of Roots as there can be more than one top-level
        /// scan conducted (i.e. multiple hard drives).
        /// </summary>
        public List<DirectorySummary> Roots = new List<DirectorySummary>();

        private static XmlSerializer Serializer = new XmlSerializer(typeof(ScanResultFile));

        void SerializeTo(Stream str) { Serializer.Serialize(str, this); }
        void SerializeTo(TextWriter tw) { Serializer.Serialize(tw, this); }

        static ScanResultFile Deserialize(Stream str) { return Serializer.Deserialize(str) as ScanResultFile; }
        static ScanResultFile Deserialize(TextReader tr) { return Serializer.Deserialize(tr) as ScanResultFile; }

        public static ScanResultFile OpenFile;
        public static void Load()
        {
            try
            {
                string ScanResultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskSpace_Examiner_2016_Database.xml");
                using (FileStream fs = new FileStream(ScanResultPath, FileMode.Open)) OpenFile = Deserialize(fs);
            }
            catch (Exception) { OpenFile = new ScanResultFile(); }
        }
        public static void Save()
        {
            string ScanResultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskSpace_Examiner_2016_Database.xml");
            using (FileStream fs = new FileStream(ScanResultPath, FileMode.Create)) OpenFile.SerializeTo(fs);

            #if DEBUG
            // Serialize to a MemoryStream, Deserialize it, and Reserialize it again - then make sure it is identical to the original.
            // This validation ensures that our IXmlSerializable custom ReadXml() and WriteXml() are consistent and repeatable.

            try
            {
                using (MemoryStream s1 = new MemoryStream())
                {
                    OpenFile.SerializeTo(s1);
                    s1.Seek(0, SeekOrigin.Begin);

                    ScanResultFile FromS1 = Deserialize(s1);
                    using (MemoryStream s2 = new MemoryStream())
                    {
                        FromS1.SerializeTo(s2);

                        s1.Seek(0, SeekOrigin.Begin);
                        s2.Seek(0, SeekOrigin.Begin);
                        byte[] buf1 = new byte[4096];
                        byte[] buf2 = new byte[4096];
                        for (; ; )
                        {
                            int Count1 = s1.Read(buf1, 0, buf1.Length);
                            int Count2 = s2.Read(buf2, 0, buf2.Length);
                            if (Count1 != Count2) throw new Exception("Verification of serialization-deserialization failed due to different lengths.");

                            for (int ii = 0; ii < Count1; ii++)
                            {
                                if (buf1[ii] != buf2[ii]) throw new Exception("Verification of serialization-deserialization failed due to mismatch.");
                            }

                            if (Count1 < buf1.Length) break;        // EOF reached.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string VerifyPath;
                try
                {
                    Load();
                    VerifyPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskSpace_Examiner_2016_Database.verify.xml");
                    using (FileStream fs = new FileStream(VerifyPath, FileMode.Create)) OpenFile.SerializeTo(fs);                    
                }
                catch (Exception)
                {
                    throw ex;
                }
                throw new Exception(ex.Message + "\n\nTo compare the original serialization to the deserialize-serialize, compare the database file (" + ScanResultPath + ") to the verification file (" + VerifyPath + ").", ex);
            }
            #endif

        }

        /// <summary>
        /// Find(FullName) locates the object in the hierarchy that corresponds to the requested path.  The search
        /// is not case sensitive.  Null is returned if the object is not found in the existing hierarchy.
        /// </summary>
        /// <param name="FullName"></param>
        /// <returns>The DirectorySummary corresponding to the requested path name.</returns>
        public DirectorySummary Find(string FullName)
        {
            string ParentFullName = Path.GetDirectoryName(FullName);
            if (ParentFullName == null)
            {
                // We have found a root (or an error).
                foreach (DirectorySummary Root in Roots)
                {
                    if (Root.FullName.Equals(FullName, StringComparison.OrdinalIgnoreCase)) return Root;
                }
                return null;            // The requested entry does not exist in the tree.
            }
            else
            {
                DirectorySummary dsParent = Find(ParentFullName);
                if (dsParent == null) return null;
                foreach (DirectorySummary Child in dsParent.Subfolders)
                {
                    if (Child.FullName.Equals(FullName, StringComparison.OrdinalIgnoreCase)) return Child;
                }
                return null;            // The requested entry does not exist in the tree.
            }
        }

        /// <summary>
        /// MergeResults replaces or adds NewResults to the existing hierarchy.  Placeholder DirectorySummary entries
        /// will be created as necessary in order to attach the new entry to a root entry.  If the directory already
        /// exists in the hierarchy, it is replaced by NewResults.  NewResults.Parent may be updated.
        /// </summary>
        /// <param name="NewResults">The directory to add/update to the scan result file hierarchy.</param>
        public void MergeResults(DirectorySummary NewResults)
        {
            if (NewResults.IsRoot)
            {
                for (int ii = 0; ii < Roots.Count; )
                {
                    if (Object.ReferenceEquals(Roots[ii], NewResults)) return;         // Already merged.
                    if (Roots[ii].FullName.Equals(NewResults.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        // We have found an existing entry for this path.  Replace it with our new results.
                        Roots.RemoveAt(ii);
                    }
                    else ii++;
                }
                // The NewResults for the new results is not in the hierarchy.  
                Roots.Add(NewResults);
                return;
            }
            else
            {
                // So NewResults is not a root.  We may need to construct DirectorySummary objects (with LastScanUtc marked as MinValue, never scanned) until
                // we can attach this directory to an existing tree or root.  For each level, we have to check whether the directory exists or a placeholder 
                // is needed, then we can work up.

                string ParentPath = Path.GetDirectoryName(NewResults.FullName);
                DirectorySummary ExistingParent = Find(ParentPath);
                if (ExistingParent != null)
                {
                    for (int ii = 0; ii < ExistingParent.Subfolders.Count; )
                    {
                        if (Object.ReferenceEquals(ExistingParent.Subfolders[ii], NewResults)) return;         // Already merged.
                        if (ExistingParent.Subfolders[ii].FullName.Equals(NewResults.FullName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Remove it.  Even if it happens to be pointing to the object we're trying to add, we'll re-add it in a moment.
                            ExistingParent.Subfolders.RemoveAt(ii);
                        }
                        else ii++;
                    }
                    ExistingParent.Subfolders.Add(NewResults);
                    return;
                }
                
                // The parent is not present in the hierarchy.  We'll need to add at least one layer of unscanned directory in order to advance up a level and repeat.
                DirectorySummary Unscanned = new DirectorySummary(null, new DirectoryInfo(ParentPath));
                Unscanned.Subfolders.Add(NewResults);
                // Note: we don't bother merging counts (i.e. calling Unscanned.MergeChild()) into the unscanned because it is incomplete anyway.  It's just a 
                //       hierarchy placeholder.
                MergeResults(Unscanned);
            }
        }
    }
}
