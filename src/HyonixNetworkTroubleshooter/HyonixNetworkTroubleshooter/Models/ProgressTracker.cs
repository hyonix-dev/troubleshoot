using System;
using System.Threading;

namespace HyonixNetworkTroubleshooter.Models
{
    public class ProgressTracker
    {
        private int _totalProgress;
        private readonly int _totalWork;
        private readonly IProgress<int> _progress;

        public ProgressTracker(int totalWork, IProgress<int> progress)
        {
            _totalWork = totalWork;
            _progress = progress;
        }

        public void ReportProgress(int incrementalWorkDone)
        {
            Interlocked.Add(ref _totalProgress, incrementalWorkDone);
            _progress.Report(_totalProgress * 100 / _totalWork);
        }
    }


}


