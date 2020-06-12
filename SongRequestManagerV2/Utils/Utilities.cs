using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Utils
{
    public static class Utilities
    {
        public static void EmptyDirectory(string directory, bool delete = true)
        {
            if (Directory.Exists(directory)) {
                var directoryInfo = new DirectoryInfo(directory);
                foreach (FileInfo file in directoryInfo.GetFiles()) file.Delete();
                foreach (DirectoryInfo subDirectory in directoryInfo.GetDirectories()) subDirectory.Delete(true);

                if (delete) Directory.Delete(directory);
            }
        }
    }
}
