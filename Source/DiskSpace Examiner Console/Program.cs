using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;

namespace DiskSpace_Examiner
{
    class Program
    {        
        List<Operation> Operations = new List<Operation>();
        string OutputPath = "DiskSpace Results.txt";        
        string ErrorPath;               // Console, by default.

        StringBuilder ErrorLog = new StringBuilder();

        static void Main(string[] CommandArgs){ Program P = new Program(); P.Execute(CommandArgs); }
        void Execute(string[] CommandArgs)
        {
            long StartTick = Environment.TickCount;

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;

            try
            {
                if (CommandArgs == null) CommandArgs = new string[] { };

#               if DEBUG
                if (CommandArgs.Length == 0)
                {
                    CommandArgs = new string[] { 
                        "-include", "C:\\$Windows.~BT\\Sources\\SafeOS\\SafeOS.Mount\\Windows\\WinSxS", "-depth", "3", "-relative"
//                        "-include", "C:\\", "-depth", "20", "-minimum-size", "100", "MB", "-users", "summary",
//                        "-include", "C:\\Code\\C", "-depth", "20", "-minimum-size", "10", "MB", "-users", "summary",
//                        "-include", "C:\\Local Backups", "-sort", "oldest", "-depth", "3", "-minimum-size", "1.35", "GB", "-users", "include",
//                        "-include", "C:\\Data", "-sort", "largest", "-depth", "2" 
                    };
                }
#               endif

                Console.Write("DiskSpace Examiner\n");
                Console.Write("\tCopyright (C) 2011 by Wiley Black\n");
                Console.Write("\n");

                if (CommandArgs.Length < 1)
                {
                    // Console Size (+/- 2 or 3 characters):
                    //            "--------------------------------------------------------------------------------"
                    Console.Write("Usage:\n");
                    Console.Write("  DiskSpace_Examiner [options]\n");
                    Console.Write("Options:\n");
                    Console.Write("  -include Path [include-options]      Adds a path to the analysis\n");
                    Console.Write("                                       list.  Multiple permitted.\n");
                    Console.Write("  -output File         Specifies the output file.\n");
                    Console.Write("  -errors File         Specifies the error log file.\n");
                    Console.Write("\n");
                    Console.Write("[include-options]:\n");
                    Console.Write("  -depth N             Specifies depth (subfolders) for this path.\n");
                    Console.Write("  -sort largest        Sort contents of this path by largest first.\n");
                    Console.Write("  -sort oldest         Sort contents of this path by oldest first.\n");
                    Console.Write("  -sort none           (Default) Do not sort contents of this path.\n");
                    Console.Write("  -minimum-size size   Hide folders containing less than size megabytes.\n");
                    Console.Write("      size is specified in megabytes, or may include KB, GB or TB postfixes.\n");
                    Console.Write("  -users only          Generate only a by-user report for this path.\n");
                    Console.Write("  -users include       Include a by-user report for this path.\n");
                    Console.Write("  -users summary       Include a by-user summary for this path.\n");
                    Console.Write("  -users none          (Default) Generate no by-user report for this path.\n");
                    Console.Write("  -relative            Shows size relative to top folder instead of drive.\n");
                    Console.Write("\n");
                    return;
                }
                else
                {
                    Operation Last = new Operation();
                    Last.Path = Directory.GetCurrentDirectory();
                    Last.Depth = 100;
                    Last.MinimumFolderSize = 0;
                    Last.Sort = SortResults.Largest;                    

                    for (int ii = 0; ii < CommandArgs.Length; ii++)
                    {
                        /** Global arguments **/

                        if (CommandArgs[ii] == "-errors")
                        {
                            ErrorPath = CommandArgs[ii + 1];
                            ii++;      // Skip attached argument.
                            continue;
                        }
                        if (CommandArgs[ii] == "-output")
                        {
                            OutputPath = CommandArgs[ii + 1];
                            ii++;      // Skip attached argument.
                            continue;
                        }

                        /** Operation-specific arguments **/

                        if (CommandArgs[ii] == "-include")
                        {
                            Last = new Operation();
                            Last.Path = CommandArgs[ii + 1];
                            Last.Depth = 0;
                            Last.MinimumFolderSize = 0;
                            Last.Sort = SortResults.Largest;
                            Operations.Add(Last);
                            ii++;      // Skip attached argument.
                        }
                        else if (CommandArgs[ii] == "-depth")
                        {
                            Last.Depth = int.Parse(CommandArgs[ii + 1]);
                            ii++;      // Skip attached argument.
                        }
                        else if (CommandArgs[ii] == "-sort")
                        {
                            switch (CommandArgs[ii + 1].ToLowerInvariant())
                            {
                                case "largest": Last.Sort = SortResults.Largest; break;
                                case "oldest": Last.Sort = SortResults.Oldest; break;
                                case "none": Last.Sort = SortResults.None; break;
                                default: throw new NotSupportedException("Sorting style '" + CommandArgs[ii + 1] + "' not recognized.");
                            }
                            ii++;
                        }
                        else if (CommandArgs[ii] == "-minimum-size")
                        {
                            string ToParse = CommandArgs[ii + 1];
                            if (CommandArgs.Length > ii + 2)
                            {
                                string nn = CommandArgs[ii + 2].Trim();
                                if (nn == "KB" || nn == "GB" || nn == "MB" || nn == "TB" || nn == "bytes")
                                {
                                    ToParse = ToParse + nn;
                                    ii++;
                                }
                            }
                            Last.MinimumFolderSize = DataSize.Parse(ToParse, DataSize.Unit.Megabytes);
                            ii++;
                        }                        
                        else if (CommandArgs[ii] == "-users")
                        {
                            switch (CommandArgs[ii + 1].ToLowerInvariant())
                            {
                                case "only": Last.UserReport = SubsetOption.Only; break;
                                case "include": Last.UserReport = SubsetOption.Include; break;
                                case "summary": Last.UserReport = SubsetOption.Summary; break;
                                case "none": Last.UserReport = SubsetOption.None; break;
                                default: throw new NotSupportedException("Users report style '" + CommandArgs[ii + 1] + "' not recognized.");
                            }
                            ii++;
                        }
                        else if (CommandArgs[ii] == "-relative") Last.RelativeSize = true;
                        else throw new Exception("Unrecognized command argument '" + CommandArgs[ii] + "'.");
                    }

                    // If the user did not specify any '-include' arguments, then load up the default
                    // current working directory.
                    if (Operations.Count < 1) Operations.Add(Last);                    
                }
            }
            catch (Exception ex)
            {
#               if DEBUG
                Console.Write("Error: " + ex.ToString() + "\n");
                System.Diagnostics.Debug.Write("Error: " + ex.ToString() + "\n");
#               else
                Console.Write("Error: " + ex.Message + "\n");
#               endif
                return;
            }

            try
            {
                // Populate the top-level results so that our "periodic" output will have the
                // right structure.  Later we'll replace these.  It also provides some early
                // error checks, and a good time to print out what we intend to do on the console.                
                foreach (Operation op in Operations)
                {
                    DirectoryInfo di;
                    try
                    {
                        di = new DirectoryInfo(op.Path);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("For path '" + op.Path + "': " + ex.Message, ex);
                    }
                    op.Result = new DirectorySummary(null, di, op);
                    Console.Write(op.SettingsToString() + "\n");
#                   if DEBUG
                    System.Diagnostics.Debug.Write(op.SettingsToString() + "\n");
#                   endif
                }

                /** Perform actual operations **/
                Console.Write("Output File:\t\t\t" + OutputPath + "\n");
                if (ErrorPath != null) Console.Write("Error Log:\t\t\t" + ErrorPath + "\n");
                Console.Write("Results will be written to the output file periodically during analysis.\n");
                foreach (Operation op in Operations)
                {
                    Console.Write("\nStarting work on '" + op.Path + "' now...\n");
                    DirectoryInfo di = new DirectoryInfo(op.Path);
                    op.Result = new DirectorySummary(null, di, op);                    
                    Populate(op.Result, di, op.Depth);
                    op.Result.Finish();
                }
            }
            catch (Exception ex)
            {
#               if DEBUG
                ErrorLog.Append("Fatal Error: " + ex.ToString() + "\r\n");
#               else
                ErrorLog.Append("Fatal Error: " + ex.Message + "\r\n");
#               endif
            }

            // Finish up
            try
            {
                SaveResults();
                TimeSpan ts = new TimeSpan(((long)Environment.TickCount - StartTick) * (long)10000);
                Console.Write("\nExamination complete.  Process required {0}.\n", ts.ToString());
#               if DEBUG
                System.Diagnostics.Debug.Write("Examination complete with no fatal errors.\n");
#               endif
            }
            catch (Exception ex)
            {
#               if DEBUG
                Console.Write("Unable to write results or error log!\nError: " + ex.ToString());
                System.Diagnostics.Debug.Write("Unable to write results or error log!\nError: " + ex.ToString() + "\n");
#               else
                Console.Write("Unable to write results or error log!\nError: " + ex.Message);
#               endif
            }            
        }
        
        int LastPeriodic = Environment.TickCount;
        int LastBigPeriodic = Environment.TickCount;
        void Periodic()
        {
            if (Environment.TickCount - LastPeriodic < 5000) return;
            LastPeriodic = Environment.TickCount;

            Console.Write(".");

#           if DEBUG
            if (Environment.TickCount - LastBigPeriodic < 10 * 1000) return;
#           else
            if (Environment.TickCount - LastBigPeriodic < 60 * 1000) return;
#           endif
            LastBigPeriodic = Environment.TickCount;
            GC.Collect();

            Console.Write("\n");
            SaveResults();
        }

        void SaveResults()
        {
            using (StreamWriter sw = new StreamWriter(OutputPath, false, Encoding.UTF8))
            {
                foreach (Operation op in Operations)
                {
                    if (op.Result != null)
                    {
                        sw.Write("{0}\r\n\r\n", op.ReportToString());
                    }
                }

                sw.Write("Report generated at " + DateTime.Now);
            }

            if (ErrorLog.Length > 0)
            {
                if (ErrorPath != null)
                {
                    using (StreamWriter sw = new StreamWriter(ErrorPath, false))
                    {
                        sw.Write(ErrorLog.ToString());
                    }
                }
                else
                {
                    Console.Write("\n" + ErrorLog.ToString() + "\n");
                }
            }
            else if (ErrorPath != null && File.Exists(ErrorPath)) File.Delete(ErrorPath);
        }

        void Populate(DirectorySummary ds, DirectoryInfo di, int RemainingDepth)
        {            
            DirectoryInfo[] Subfolders = di.GetDirectories();
            FileInfo[] Files = di.GetFiles();

            Periodic();

            foreach (DirectoryInfo diChild in Subfolders)
            {
                if (ds.IsRoot)
                {
                    if (diChild.Name == "RECYCLER") continue;
                    if (diChild.Name == "System Volume Information") continue;                                         
                }

                DirectorySummary dsChild;
                try
                {
                    dsChild = new DirectorySummary(ds, diChild);
                    Populate(dsChild, diChild, RemainingDepth - 1);
                }
                catch (Exception e)
                {
                    ErrorLog.Append(
                        string.Format("Warning: Skipping folder '{0}' due to error: {1}\r\n", diChild.FullName, e.Message));
                    continue;
                }

                if (RemainingDepth > 0)
                {
                    ds.AddChild(dsChild);
                    dsChild.Finish();
                }
                else
                    ds.MergeChild(dsChild);
            }

            foreach (FileInfo file in Files) ds.MergeChild(file);  
        }
    }

    public enum SortResults
    {
        None,
        Largest,
        Oldest
    }

    public enum SubsetOption
    {
        None,
        Include,
        Summary,
        Only
    }

    public class Operation
    {
        public string Path;
        public int Depth = 0;
        public DataSize MinimumFolderSize = 0;
        public SortResults Sort = SortResults.None;
        public SubsetOption UserReport = SubsetOption.None;
        public bool RelativeSize = false;

        public DirectorySummary Result;

        public string SettingsToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Examining Path '" + Path + "'");
            if (Depth > 0) sb.AppendLine("\tDepth:\t\t\t" + Depth + " folders");
            if (MinimumFolderSize > 0) sb.AppendLine("\tMinimum Folder Size:\t" + MinimumFolderSize.ToFriendlyString());
            if (Sort != SortResults.None) sb.AppendLine("\tSorting:\t\t" + Sort.ToString());
            if (UserReport != SubsetOption.None) sb.Append("\tUser Report:\t\t" + UserReport.ToString());
            if (RelativeSize) sb.AppendLine("\tSize Display:\t\tRelative to top directory");
            return sb.ToString();
        }

        public string ReportToString()
        {
            StringBuilder sb = new StringBuilder();
            if (UserReport != SubsetOption.Only)
            {
                sb.Append(Result.ToString(0));
            }

            if (UserReport != SubsetOption.None)
            {
                foreach (KeyValuePair<string, Ownership> Pair in Result.Ownerships)
                {
                    Account UserAccount = Pair.Value as Account;
                    sb.Append(Result.ToString(0, UserAccount, UserReport));
                }
            }

            return sb.ToString();
        }
    }

    public class Account
    {
        public string FullName;
        public string Domain;
        public string User;

        public Account()
        {
        }

        public Account(string FullName)
        {
            this.FullName = FullName;
            int iSlash = FullName.IndexOf('\\');
            if (iSlash < 0) { User = FullName; Domain = ""; return; }
            Domain = FullName.Substring(0, iSlash);
            User = FullName.Substring(iSlash + 1);
        }

        public override string ToString(){ return FullName; }
        public override int GetHashCode(){ return FullName.GetHashCode(); }        
    }

    public class Ownership : Account, IEqualityComparer<Ownership>
    {
        public DataSize Size;
        public DateTime Oldest;       // Oldest date of user access of a file in this folder
        public DateTime Newest;       // Newest date of user access of a file in this folder        

        public Ownership(string FullName)
            : base(FullName)
        {
            Size = 0;
            Oldest = DateTime.MaxValue;
            Newest = DateTime.MinValue;
        }

        public bool Equals(Ownership a, Ownership b)
        {
            if (a == null) { if (b == null) return true; return false; }
            if (b == null) return false;
            return (a.FullName == b.FullName && a.Size == b.Size);
        }

        public int GetHashCode(Ownership a)
        {
            return FullName.GetHashCode();
        }
    }

    public class DirectorySummary
    {
        public string Name;
        public string FullName;
        public DataSize Size;         // Size, including all subfolders & files
        public DateTime Oldest;       // Oldest date of user access of a file in this folder
        public DateTime Newest;       // Newest date of user access of a file in this folder
        public long TotalFiles;       // Count of number of files, including all files in subfolders
        public DirectorySummary Parent = null;
        public List<DirectorySummary> Subfolders = new List<DirectorySummary>();
        public bool IsRoot = false;
        public bool Completed = false;
        public Dictionary<string,Ownership> Ownerships = new Dictionary<string,Ownership>();        

        private Operation m_Settings;
        public Operation Settings
        {
            get
            {
                if (m_Settings == null) return Parent.Settings;
                return m_Settings;
            }
            set
            {
                m_Settings = value;
            }
        }

        private DriveInfo m_Drive;
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

        /// <summary>
        /// If the -relative option is not specified, we want to produce graphs comparing size used to total disk
        /// size, so ContainerSize is the Drive's total size.  If the -relative option is specified, we want to
        /// produce graphs comparing size used to the top-most folder in the graph, so ContainerSize provides the
        /// tree root's size.
        /// </summary>
        public long ContainerSize
        {
            get
            {
                if (Settings.RelativeSize)
                {
                    if (Parent == null) return Size;
                    DirectorySummary Cont = Parent;
                    for (; ; )
                    {
                        if (Cont.Parent != null) Cont = Cont.Parent;
                        else return Cont.Size;
                    }
                }
                else
                {
                    return Drive.TotalSize;
                }
            }
        }

        public DirectorySummary(DirectorySummary Parent, DirectoryInfo di, Operation Settings)
            : this(Parent,di)
        {
            this.Settings = Settings;            
        }

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

        public void AddChild(DirectorySummary dsChild)
        {
            MergeChild(dsChild);
            if (dsChild.Parent != this) throw new Exception();
            Subfolders.Add(dsChild);
        }

        public void MergeChild(DirectorySummary dsChild)
        {
            Size += dsChild.Size;
            if (dsChild.Oldest < Oldest) Oldest = dsChild.Oldest;
            if (dsChild.Newest > Newest) Newest = dsChild.Newest;
            TotalFiles += dsChild.TotalFiles;

            if (Settings.UserReport != SubsetOption.None)
            {
                foreach (KeyValuePair<string, Ownership> Pair in dsChild.Ownerships)
                {
                    string FullName = Pair.Key;
                    Ownership ChildOwner = Pair.Value;

                    if (!Ownerships.ContainsKey(FullName)) Ownerships.Add(FullName, new Ownership(FullName));
                    Ownership Owner;
                    Owner = Ownerships[FullName];

                    Owner.Size += ChildOwner.Size;
                    if (ChildOwner.Oldest < Owner.Oldest) Owner.Oldest = ChildOwner.Oldest;
                    if (ChildOwner.Newest > Owner.Newest) Owner.Newest = ChildOwner.Newest;
                }
            }
        }        

        public void MergeChild(FileInfo file)
        {
//            Size += file.Length;
            Size += FileUtility.GetFileSizeOnDisk(file);
            if (file.LastWriteTime < Oldest) Oldest = file.LastWriteTime;
            if (file.LastWriteTime > Newest) Newest = file.LastWriteTime;
            TotalFiles++;

            if (Settings.UserReport != SubsetOption.None)
            {
                FileSecurity fs = File.GetAccessControl(file.FullName);
                IdentityReference sid = fs.GetOwner(typeof(SecurityIdentifier));
                NTAccount ntAccount = sid.Translate(typeof(NTAccount)) as NTAccount;
                string FullName = ntAccount.ToString();
                
                if (!Ownerships.ContainsKey(FullName)) Ownerships.Add(FullName, new Ownership(FullName));
                Ownership Owner;
                Owner = Ownerships[FullName];

                Owner.Size += file.Length;
                if (file.LastWriteTime < Owner.Oldest) Owner.Oldest = file.LastWriteTime;
                if (file.LastWriteTime > Owner.Newest) Owner.Newest = file.LastWriteTime;
            }
        }

        public void Finish()
        {
            try
            {
                if (IsRoot) Size = Drive.TotalSize - Drive.AvailableFreeSpace;                

                /** Cull any subfolders which do not meet criteria **/
                // The subfolders have already been included in Size and age when they
                // were added.
                DataSize MinimumFolderSize = Settings.MinimumFolderSize;
                for (int ii = 0; ii < Subfolders.Count; )
                {
                    if (Subfolders[ii].Size < MinimumFolderSize)
                    {
                        Subfolders.RemoveAt(ii);
                    }
                    else
                    {
                        string[] OwnerNames = Subfolders[ii].Ownerships.Keys.ToArray();
                        foreach (string OwnerName in OwnerNames)
                        {
                            Ownership Owner = Subfolders[ii].Ownerships[OwnerName];
                            if (Owner.Size < MinimumFolderSize) Subfolders[ii].Ownerships.Remove(Owner.FullName);
                        }

                        ii++;
                    }
                }

                switch (Settings.Sort)
                {
                    case SortResults.None: break;
                    case SortResults.Largest: Subfolders.Sort(CompareByLargest); break;
                    case SortResults.Oldest: Subfolders.Sort(CompareByOldest); break;
                    default: throw new NotSupportedException();
                }

                Completed = true;
            }
            catch (Exception ex) { throw new Exception("While finishing folder: " + ex.Message, ex); }
        }

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

        private void GenerateGraph(StringBuilder sb, long This, long Parent, long Drive)
        {
            const int GraphLength = 60;

            const char Block = '\u2588';        // Filled block (solid)
            //const char Block3Q = '\u2593';      // 3/4th full block
            const char Block2Q = '\u2592';      // 1/2 full block
            //const char Block1Q = '\u2591';      // 1/4th full block

            int ParentLength = (int)Math.Round(GraphLength * (double)Parent / (double)Drive);
            int ThisLength = (int)Math.Round(GraphLength * (double)This / (double)Drive);
            int ii = 0;
            sb.Append('[');
            for (ii = 0; ii < ThisLength; ii++) sb.Append(Block);
            for (; ii < ParentLength; ii++) sb.Append(Block2Q);
            for (; ii < GraphLength; ii++) sb.Append(' ');
            sb.Append("] ");
        }

        private void GenerateBlankGraph(StringBuilder sb)
        {
            const int GraphLength = 60;
            sb.Append(' ');            
            for (int ii = 0; ii < GraphLength; ii++) sb.Append(' ');
            sb.Append("  ");
        }

        public string ToString(int Indent)
        {
            const int Column2 = 75;

            // Column #1: Folder/Drive name
            StringBuilder sb = new StringBuilder();
            for (int ii=0; ii < Indent; ii++) sb.Append("    ");
            sb.Append(DisplayName);
            for (int ii = sb.Length; ii < Column2; ii++) sb.Append(" ");

            // Column #2: Size graph and numeric            
            if (!IsRoot && !Completed)
            {
                sb.Append("(Calculating...)\r\n");
            }
            else
            {
                DataSize DiskTotal = Drive.TotalSize;                
                DataSize ParentTotal = 0;
                if (Parent != null) ParentTotal = Parent.DisplaySize;
                DataSize ThisTotal = DisplaySize;
                
                if (Parent == null)
                {
                    if (Settings.RelativeSize) GenerateBlankGraph(sb);
                    else GenerateGraph(sb, ThisTotal, ParentTotal, ContainerSize);
                    sb.Append(ThisTotal.ToFriendlyString() + " used of " + DiskTotal.ToFriendlyString());
                }
                else
                {
                    GenerateGraph(sb, ThisTotal, ParentTotal, ContainerSize);
                    sb.Append(ThisTotal.ToFriendlyString() + " used");
                }
                sb.Append("\r\n");
            }
            
            foreach (DirectorySummary sub in Subfolders) sb.Append(sub.ToString(Indent + 1));
            if (IsRoot && !Completed)
            {
                for (int ii=0; ii < Indent + 1; ii++) sb.Append("    ");
                sb.Append("(Examining...)\r\n");
            }

#           if DEBUG            
            if (Parent == null)
            {
                for (int ii = 0; ii < Indent + 1; ii++) sb.Append("    ");
                sb.Append("Total Files:     " + TotalFiles.ToString() + "\r\n");
            }
#           endif

            return sb.ToString();
        }

        public string ToString(int Indent, Account UserAccount, SubsetOption ReportStyle)
        {
            const int Column2 = 75;         

            // Column #1: Folder/Drive name
            StringBuilder sb = new StringBuilder();
            for (int ii = 0; ii < Indent; ii++) sb.Append("    ");
            if (Parent == null)
            {
                if (ReportStyle == SubsetOption.Only)            
                    sb.Append("User: " + UserAccount.FullName + " (" + DisplayName + ")");
                else
                    sb.Append("User: " + UserAccount.FullName);
            }
            else sb.Append(DisplayName);            
            for (int ii = sb.Length; ii < Column2; ii++) sb.Append(" ");

            // Column #2: Size graph and numeric            
            if (!Completed)
            {
                sb.Append("(Calculating...)\r\n");
            }
            else
            {
                Ownership Owner = Ownerships[UserAccount.FullName];

                DataSize DiskTotal = Drive.TotalSize;
                DataSize ParentTotal = 0;
                if (Parent != null) ParentTotal = Parent.Ownerships[UserAccount.FullName].Size;
                DataSize ThisTotal = Owner.Size;

                GenerateGraph(sb, ThisTotal, ParentTotal, DiskTotal);
                if (Parent == null)
                    sb.Append(ThisTotal.ToFriendlyString() + " used of " + DiskTotal.ToFriendlyString());
                else
                    sb.Append(ThisTotal.ToFriendlyString() + " used");
                sb.Append("\r\n");
            }

            if (ReportStyle != SubsetOption.Summary)
            {
                foreach (DirectorySummary sub in Subfolders)
                {
                    if (sub.Ownerships.ContainsKey(UserAccount.FullName))
                    {
                        sb.Append(sub.ToString(Indent + 1, UserAccount, ReportStyle));
                    }
                }
            }
            return sb.ToString();
        }
    }

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
                    if (!GetFileSizeEx(safeHandle, ref ret)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    return ret;
                }
            }
            else
                throw new Win32Exception(Marshal.GetLastWin32Error());                
#endif
        }

        // Define constants.
        protected const uint GENERIC_READ = 0x80000000;
        protected const uint FILE_SHARE_READ = 0x00000001;
        protected const uint OPEN_EXISTING = 3;
        protected const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        protected static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const int INVALID_FILE_SIZE = unchecked((int) 0xFFFFFFFF);
        
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        protected static extern IntPtr CreateFile (
                                  string lpFileName, uint dwDesiredAccess, 
                                  uint dwShareMode, IntPtr lpSecurityAttributes, 
                                  uint dwCreationDisposition, uint dwFlagsAndAttributes, 
                                  IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetFileSizeEx(SafeHandle hFile, ref long lpFileSize);

        [DllImport("kernel32.dll")]
        static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
           [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
        static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
           out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
           out uint lpTotalNumberOfClusters);
    }
}
