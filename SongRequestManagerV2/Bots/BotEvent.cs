using System;
using System.Timers;

namespace SongRequestManagerV2.Bots
{
    public class BotEvent : IDisposable
    {
        public DateTime time;
        public string command;
        public bool repeat;
        private bool disposedValue;
        private readonly Timer timeq;
        private Action<string, ElapsedEventArgs> _event;
        public void StopTimer()
        {
            this.timeq.Stop();
        }

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
            this._event = timerAction;
            this.timeq.Elapsed += this.Timeq_Elapsed;
            this.timeq.AutoReset = repeat;
            this.timeq.Enabled = true;
        }

        private void Timeq_Elapsed(object sender, ElapsedEventArgs e)
        {
            this._event?.Invoke(this.command, e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue) {
                if (disposing) {
                    this.timeq.Elapsed -= this.Timeq_Elapsed;
                    this._event = null;
                    this.timeq.Stop();
                    this.timeq.Dispose();
                }
                this.disposedValue = true;
            }
        }
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}