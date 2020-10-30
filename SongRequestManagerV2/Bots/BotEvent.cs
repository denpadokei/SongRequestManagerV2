using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SongRequestManagerV2.Bots
{
    public class BotEvent : IDisposable
    {
        public DateTime time;
        public string command;
        public bool repeat;
        Timer timeq;

        public void Dispose()
        {
            ((IDisposable)this.timeq).Dispose();
        }

        public void StopTimer()
        {
            this.timeq.Stop();
        }

        public BotEvent(DateTime time, string command, bool repeat, Action<string, ElapsedEventArgs> timerAction = null)
        {
            this.time = time;
            this.command = command;
            this.repeat = repeat;
            timeq = new Timer(1000);
            timeq.Elapsed += (sender, e) => timerAction?.Invoke(command, e);
            timeq.AutoReset = true;
            timeq.Enabled = true;
        }

        public BotEvent(TimeSpan delta, string command, bool repeat = false, Action<string, ElapsedEventArgs> timerAction = null)
        {
            this.command = command;
            this.repeat = repeat;
            timeq = new Timer(delta.TotalMilliseconds);
            timeq.Elapsed += (sender, e) => timerAction?.Invoke(command, e);
            timeq.AutoReset = repeat;
            timeq.Enabled = true;
        }
    }
}