using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace DiskSpace_Examiner_2016
{
    public class DiskScan : IDisposable
    {
        /** Constants **/
        static DataSize MinimumFolderSizeToRetain = DataSize.Megabyte;
        const int MinimumChildCountToRetain = 50;
        bool ShouldCull(DirectorySummary Folder) { return Folder.Size < MinimumFolderSizeToRetain && Folder.TotalChildren < MinimumChildCountToRetain; }

        int LastCommit = Environment.TickCount;
        const int CommitFrequencyInMilliseconds = 2 /*minutes/commit*/ * 60000 /*seconds/minute*/;

        /// <summary>UpdateInterval is the DataSize threshold at which we interrupt processing in order to 
        /// propagate Deltas up to the top-level so as to update the display.</summary>
        static DataSize UpdateInterval = DataSize.Gigabyte;
       
        public DiskScan(string TopPath)
        {
            this.TopPath = TopPath;
            Worker = new Thread(WorkerThread);
            Worker.Start();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        bool Closing = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                // Free managed resources
                Closing = true;
                if (Worker != null) { Worker.Join(); Worker = null; }
            }

            // Free unmanaged resources
            disposed = true;
        }

        object WorkerExceptionLock = new object();
        Exception WorkerException;
        public void CheckHealth()
        {
            lock (WorkerExceptionLock)
            {
                if (WorkerException != null)
                {
                    Exception exc = WorkerException;
                    WorkerException = null;
                    throw exc;
                }
            }
        }

        Thread Worker;
        string TopPath;

        private DateTime ScanStartedUtc;

        /// <summary>
        /// ScanRoot contains the in-progress and final results of the scan.  Any given DirectorySummary should be lock'd 
        /// before reading.
        /// </summary>
        public DirectorySummary ScanRoot;

        /// <summary>
        /// Apply lock to the DiskScan object before reading these values
        /// </summary>
        public long FilesScanned = 0;
        public long FoldersScanned = 0;

        public enum Activities
        {
            ScanningFolders,
            ScanningNewFolders,
            RescanningOldFolders,
            CommittingPartialResults,
            CommittingFinalResults,
            ScanComplete
        }
        public Activities CurrentActivity;          // For display/informational purposes.  Guarded by a lock on the DiskScan object.
        public bool IsScanComplete { get { return CurrentActivity == Activities.ScanComplete; } }

        void WorkerThread()
        {
            #if !DEBUG          // In debug mode, we'd rather the exception go uncaught so the debugger can find it.
            try
            {
            #endif
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                ScanStartedUtc = DateTime.UtcNow;

                lock (this) CurrentActivity = Activities.ScanningFolders;

                lock (ScanResultFile.OpenFile)
                {
                    LastCommit = Environment.TickCount;

                    /** Processing Sequence:
                     *                      
                     *  1st Pass: Enumerate immediate subfolders.  Replace any entries in the enumeration that have previous
                     *            versions with references to those previous versions.  Check for previous versions that are
                     *            no longer present in the enumeration and reduce the parent's counters accordingly.  The
                     *            folder being scanned can either be a previous result folder or a new folder, but when it is
                     *            a new result folder it simply contains an empty Subfolders list and all subfolders are
                     *            identified as "new additions".
                     *  2nd Pass: Recurse into subfolders and perform 1st and 2nd pass within them.  When the 1st pass
                     *            results in a size reduction in counters, apply that here and pass it up.  Sizes can decrease
                     *            as we discover subfolders that are no longer present, but there should be no cause to increase 
                     *            any sizes at this stage as we are not enumerating files yet.  Subfolder counts can increase.
                     *  3rd Pass: Recurse into any subfolders that do not have a previous version.  Completely tabulate them
                     *            and increase size on parent when completed.  Oldest and Newest stamps will be accurate for
                     *            these leaves, but not up the tree.                     
                     *  4th Pass: Recurse into any subfolders that do have a previous version.  Completely tabulate them
                     *            and track the delta size.  Update parent with delta size.  Start this process at the bottoms
                     *            of the tree so as the have minimal impact on the display until we have new results to show.
                     *            By starting at the bottom of the tree and propagating upward, we can also propagate up
                     *            new and accurate Oldest and Newest stamps.
                     *
                     */
                    ScanRoot = ScanResultFile.OpenFile.Find(TopPath);
                    if (ScanRoot == null) 
                    {
                        DirectoryInfo diRoot = new DirectoryInfo(TopPath);
                        ScanRoot = new DirectorySummary(null, diRoot);
                    }
                                        
                    FirstPass(ScanRoot);
                    if (Closing) return;

                    SecondPass(ScanRoot, true);
                    if (Closing) return;

                    ScanResultFile.OpenFile.MergeResults(ScanRoot);
                    lock (this) CurrentActivity = Activities.ScanningNewFolders;

                    bool Completed = false;
                    while (!Completed)
                    {
                        ThirdOrFourthPass(ScanRoot, false, out Completed);
                        if (Closing) return;
                    }

                    lock (this) CurrentActivity = Activities.RescanningOldFolders;
                    Completed = false;
                    while (!Completed)
                    {
                        ThirdOrFourthPass(ScanRoot, true, out Completed);
                        if (Closing) return;
                    }

                    lock (this) CurrentActivity = Activities.CommittingFinalResults;
                    ScanResultFile.Save();

                    lock (this) CurrentActivity = Activities.ScanComplete;
                }
            #if !DEBUG
            }
            catch (Exception exc)
            {
                lock (WorkerExceptionLock) { WorkerException = exc; }
            }
            #endif
        }                        

        /// <summary>
        /// Enumerate immediate subfolders.  Replace any entries in the enumeration that have previous
        /// versions with references to those previous versions.  Check for previous versions that are
        /// no longer present in the enumeration and reduce the parent's counters accordingly.  The
        /// folder being scanned can either be a previous result folder or a new folder, but when it is
        /// a new result folder it simply contains an empty Subfolders list and all subfolders are
        /// identified as "new additions".
        /// </summary>
        DirectorySummary.DeltaCounters FirstPass(DirectorySummary Current)
        {
            DirectorySummary.DeltaCounters Delta = new DirectorySummary.DeltaCounters();
            long LocalFoldersScanned = 0;            

            DirectoryInfo[] Folders;
            try
            {
                DirectoryInfo diInProgress = new DirectoryInfo(Current.FullName);            
                Folders = diInProgress.GetDirectories();
            }
            catch (Exception) { Folders = new DirectoryInfo[0]; }                // Skip/treat empty directories that we can't access.  They should show up as "unaccounted" or "outside".

            try
            {                
                lock (Current)
                {
                    // Current might be a DirectorySummary pulled from the ScanResultFile or it might be entirely new.
                    // We don't really need to distinguish here, as in the case of "entirely new" it will simply have
                    // an empty Subfolder list and everything will qualify as an "add".  

                    // First, look for subfolders that have been removed since the previous version.                    
                    for (int ii=0; ii < Current.Subfolders.Count; )
                    {
                        DirectorySummary ds = Current.Subfolders[ii];
                        if (Closing) { Current.Adjust(Delta); return Delta; }

                        bool WasRemoved = true;
                        foreach (DirectoryInfo di in Folders)
                        {
                            if (di.Name.Equals(ds.Name, StringComparison.OrdinalIgnoreCase)) { WasRemoved = false; break; }
                        }
                        if (WasRemoved)
                        {
                            Delta.Size -= ds.Size;
                            Delta.TotalFiles -= ds.TotalFiles;
                            Delta.TotalSubfolders -= ds.TotalSubfolders;
                            Delta.TotalSubfolders--;
                            Current.Subfolders.RemoveAt(ii);
                        }
                        else ii++;                        
                    }

                    // Now, look for new subfolders.
                    
                    // This will involve a comparison for each file system folder against all folders in record.  That can be a painful
                    // operation.  To help, we first make a modifiable copy of the Current.Subfolders list and remove entries as they
                    // are found.  Sort of like checking them off a list, they won't be a burden on the next search through the list.
                    List<DirectorySummary> PreviousList = new List<DirectorySummary>(Current.Subfolders);

                    foreach (DirectoryInfo di in Folders)
                    {
                        if (Closing) { Current.Adjust(Delta); return Delta; }

                        // Check if there is a previous scan on this folder and use that as a starting point.
                        bool FoundPrevious = false;
                        for (int ii=0; ii < PreviousList.Count; )
                        {
                            if (PreviousList[ii].Name.Equals(di.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                PreviousList.RemoveAt(ii);          // Remove from the PreviousList, but it still exists in Current.Subfolders (the real listing).
                                FoundPrevious = true;
                                break;
                            }
                            else ii++;
                        }

                        if (!FoundPrevious)
                        {
                            Delta.TotalSubfolders++;
                            Current.Subfolders.Add(new DirectorySummary(Current, di));
                        }

                        LocalFoldersScanned++;
                    }

                    // Apply the accumulated counter changes to Current.  We will still need to propagate these deltas up, as well.
                    // This step also invalidates any Oldest/Newest timestamps, which will need to be reconsidered from the bottom up.
                    Current.Adjust(Delta);
                }
            }
            catch (Exception exc)
            {
                throw new Exception(exc.Message + " (while examining directories)", exc);
            }

            lock (this) { FoldersScanned += LocalFoldersScanned; }
            return Delta;
        }

        
        /// <summary>
        /// Recurse into subfolders and perform 1st and 2nd pass within them.  When the 1st pass
        /// results in a size reduction in counters, apply that here and pass it up.  Sizes can decrease
        /// as we discover subfolders that are no longer present, but there should be no cause to increase 
        /// any sizes at this stage as we are not enumerating files yet.  Subfolder counts can increase.
        /// </summary>
        DirectorySummary.DeltaCounters SecondPass(DirectorySummary Current, bool AlwaysCommit = false)
        {
            DirectorySummary.DeltaCounters Delta = new DirectorySummary.DeltaCounters();

            foreach (DirectorySummary dsFolder in Current.Subfolders)
            {
                Delta += FirstPass(dsFolder);
                if (Closing) { lock (Current) Current.Adjust(Delta); return Delta; }
            }

            foreach (DirectorySummary dsFolder in Current.Subfolders)
            {
                Delta += SecondPass(dsFolder);
                if (Closing) { lock (Current) Current.Adjust(Delta); return Delta; }
            }

            lock (Current)
            {
                Current.Adjust(Delta); 
            }
            return Delta;
        }

        /// <summary>
        /// Recurse into any subfolders that do not have a previous version.  Completely tabulate them
        /// and increase size on parent when completed.  Oldest and Newest stamps will be accurate for
        /// these leaves, but not up the tree.
        /// </summary>        
        /// <param name="Criteria">For the third pass, this should be DateTime.MinValue.  For the fourth pass, this should
        /// be ScanStartedUtc.  This sets the time threshold to skip processing each folder.</param>
        /// <param name="Completed">True if processing on Current has completed.  False if we are just quitting momentarily
        /// to update the deltas and need to revisit.</param>
        DirectorySummary.DeltaCounters ThirdOrFourthPass(DirectorySummary Current, bool FourthPass, out bool Completed)
        {
            DateTime Criteria = FourthPass ? ScanStartedUtc : DateTime.MinValue;

            DirectorySummary.DeltaCounters Absolute = new DirectorySummary.DeltaCounters();
            DirectorySummary.DeltaCounters Delta = new DirectorySummary.DeltaCounters();            
            DateTime Newest = DateTime.MinValue;
            DateTime Oldest = DateTime.MaxValue;

            foreach (DirectorySummary dsFolder in Current.Subfolders)
            {
                // Check whether we've completed this one (3rd or 4th pass), or if this one has previous version information (3rd pass).
                if (dsFolder.LastScanUtc <= Criteria)
                {
                    bool SubCompleted;
                    Delta += ThirdOrFourthPass(dsFolder, FourthPass, out SubCompleted);
                    if (Closing) { lock (Current) Current.Adjust(Delta); Completed = false; return Delta; }

                    if (!SubCompleted || Delta.Size >= UpdateInterval) { lock (Current) Current.Adjust(Delta); Completed = false; return Delta; }
                }

                // dsFolder has now completed its third/fourth pass and can be counted.  This may have been from previous scans or a previous call to ThirdOrFourthPass,
                // or it may have completed just now, but it's complete.

                Absolute.Size += dsFolder.Size;
                Absolute.TotalFiles += dsFolder.TotalFiles;
                Absolute.TotalSubfolders += dsFolder.TotalSubfolders;
                Absolute.TotalSubfolders++;
                if (dsFolder.Oldest < Oldest) Oldest = dsFolder.Oldest;
                if (dsFolder.Newest > Newest) Newest = dsFolder.Newest;
            }
            
            FileInfo[] Files;
            try
            {
                DirectoryInfo diInProgress = new DirectoryInfo(Current.FullName);
                Files = diInProgress.GetFiles();
            }
            catch (Exception) { Files = new FileInfo[0]; }                // Skip/treat empty directories that we can't access.  They should show up as "unaccounted" or "outside".

            // We don't retain any information on individual files, so we cannot compute a Delta as we go.  However, we know the size of all of our subfolders (we've been
            // counting in Absolute) and we are about the count the size of the files as they stand on disk.  From this, we can compute a final delta.

            long AddnFilesScanned = Absolute.TotalFiles;
            int SinceUpdate = Environment.TickCount;
            try
            {                
                foreach (FileInfo fi in Files)
                {
                    if (Closing) { lock (Current) { Current.Adjust(Delta); Completed = false; return Delta; } }

                    if (fi.LastWriteTimeUtc < Oldest) Oldest = fi.LastWriteTimeUtc;
                    if (fi.LastWriteTimeUtc > Newest) Newest = fi.LastWriteTimeUtc;
                    Absolute.Size += FileUtility.GetFileSizeOnDisk(fi);
                    Absolute.TotalFiles ++;
                    AddnFilesScanned++;

                    if ((Environment.TickCount - SinceUpdate) > 5000)
                    {
                        lock (this) { FilesScanned += AddnFilesScanned; AddnFilesScanned = 0; }
                        SinceUpdate = Environment.TickCount;
                    }
                }
            }
            catch (Exception) { }                        

            Delta.Size = Absolute.Size - Current.Size;
            Delta.TotalFiles = Absolute.TotalFiles - Current.TotalFiles;
            Delta.TotalSubfolders = Absolute.TotalSubfolders - Current.TotalSubfolders;

            Current.Size = Absolute.Size;
            Current.TotalFiles = Absolute.TotalFiles;
            Current.TotalSubfolders = Absolute.TotalSubfolders;
            Current.Oldest = Oldest;
            Current.Newest = Newest;
            Current.LastScanUtc = DateTime.UtcNow;
            Completed = true;

            // The choice of when to CullDetails is tricky.  If we do it while updating a folder (i.e. the recursion loop above) then we run the risk of modifying
            // the content of Subfolders only to have to re-add the information after an UpdateInterval comes back around.  That could lead to a cycle where we
            // spend all our time culling, committing, and re-examining.  Doing it here fails to cull things out of higher level folders, but that's desirable because
            // we are only talking about culling the in-memory representation and we still need those higher levels (they haven't completed yet).  The XML serialization
            // will automatically cull any small directories before storing to the ScanResultFile, so those higher levels will be culled out as far as disk storage
            // is concerned.
            Current.CullDetails();

            // Committing the ScanResultFile is both infrequent and time consuming.  Our results in-memory are valid, that's the best state to quit in.  Not committing
            // to disk is a bit painful, but if they're closing, let's be responsive.  The previous commit should be there anyway.
            if (Closing) { return Delta; }

            int Elapsed = Environment.TickCount - LastCommit;
            if (Elapsed > CommitFrequencyInMilliseconds) {
                Activities WasDoing;
                lock (this) { WasDoing = CurrentActivity; CurrentActivity = Activities.CommittingPartialResults; }
                ScanResultFile.Save();
                lock (this) { CurrentActivity = WasDoing; }
                LastCommit = Environment.TickCount; 
            }

            lock (this) { FilesScanned += AddnFilesScanned; AddnFilesScanned = 0; }
            return Delta;
        }
    }
}
