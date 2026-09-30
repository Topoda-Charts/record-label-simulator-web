using System.IO;
using UnityEngine;

namespace Topoda.RLS.Observer
{
    // Capture sessions never write recovery saves into the human player's snapshot directory.
    public static class ObserverSnapshotPaths
    {
        public static string Current
        {
            get
            {
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-rlsSnapshotRoot" && Path.IsPathRooted(args[i + 1]))
                        return Path.GetFullPath(args[i + 1]);
                string capture = ObserverAutoCaptureBootstrap.TryReadCaptureDirectory();
                return Path.Combine(string.IsNullOrEmpty(capture) ? Application.persistentDataPath : capture, "Snapshots");
            }
        }
    }
}
