using IPA.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2
{
    public static class Logger
    {
        private static IPA.Logging.Logger IPALogger => Plugin.Logger;
        public static void Debug(string message, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Debug($"{Path.GetFileName(path)}[{member}({num})] : {message}");
        }
        
        public static void Debug(Exception e, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Debug($"{Path.GetFileName(path)}[{member}({num})] : {e}");
        }
        
        public static void Error(Exception e, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Error($"{Path.GetFileName(path)}[{member}({num})] : {e}");
        }
        
        public static void Error(string message, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Error($"{Path.GetFileName(path)}[{member}({num})] : {message}");
        }
        
        public static void Info(string message, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Info($"{Path.GetFileName(path)}[{member}({num})] : {message}");
        }
        
        public static void Info(Exception e, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Info($"{Path.GetFileName(path)}[{member}({num})] : {e}");
        }
        
        public static void Notice(Exception e, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Notice($"{Path.GetFileName(path)}[{member}({num})] : {e}");
        }
        
        public static void Notice(string message, [CallerFilePath] string path = null, [CallerMemberName] string member = null, [CallerLineNumber] int? num = null)
        {
            IPALogger.Notice($"{Path.GetFileName(path)}[{member}({num})] : {message}");
        }

    }
}
