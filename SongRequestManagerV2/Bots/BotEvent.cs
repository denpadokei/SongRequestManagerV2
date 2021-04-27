using System;
using System.Timers;

namespace SongRequestManagerV2.Bots
{
    public class BotEvent : IDisposable
    {
        public DateTime time;
        public string command;
        public bool repeat;
        private readonly Timer timeq;

        public void Dispose() => ((IDisposable)this.timeq).Dispose();

        public void StopTimer() => this.timeq.Stop();

        public BotEvent(DateTime time, string command, bool repeat, Action<string, ElapsedEventArgs> timerAction = null)
        {
            this.time = time;
            this.command = command;
            this.repeat = repeat;
            this.timeq = new Timer(1000);
            this.timeq.Elapsed += (sender, e) => timerAction?.Invoke(command, e);
            this.timeq.AutoReset = true;
            this.timeq.Enabled = true;
        }

        public BotEvent(TimeSpan delta, string command, bool repeat = false, Action<string, ElapsedEventArgs> timerAction = null)
        {
            this.command = command;
            this.repeat = repeat;
            this.timeq = new Timer(delta.TotalMilliseconds);
            this.timeq.Elapsed += (sender, e) => timerAction?.Invoke(command, e);
            this.timeq.AutoReset = repeat;
            this.timeq.Enabled = true;
        }
    }
}