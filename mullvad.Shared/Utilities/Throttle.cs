using System;
using System.Threading;
using System.Threading.Tasks;

namespace mullvad.Utilities
{
    /// <summary>
    /// Token-bucket rate limiter. Callers await <see cref="WaitAsync"/> before sending;
    /// the bucket refills at <paramref name="ratePerSecond"/> tokens per second up to
    /// <paramref name="burst"/> tokens.
    /// </summary>
    public sealed class Throttle : IDisposable
    {
        private readonly double _rate;   // tokens/ms
        private readonly double _burst;
        private double   _tokens;
        private DateTime _last;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public Throttle(double ratePerSecond, double burst = 0)
        {
            if (ratePerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ratePerSecond));
            _rate   = ratePerSecond / 1000.0;
            _burst  = burst > 0 ? burst : ratePerSecond;
            _tokens = _burst;
            _last   = DateTime.UtcNow;
        }

        public async Task WaitAsync(double cost = 1, CancellationToken ct = default)
        {
            while (true)
            {
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    Refill();
                    if (_tokens >= cost)
                    {
                        _tokens -= cost;
                        return;
                    }
                    double waitMs = (cost - _tokens) / _rate;
                    await Task.Delay(TimeSpan.FromMilliseconds(waitMs), ct)
                              .ConfigureAwait(false);
                }
                finally { _gate.Release(); }
            }
        }

        private void Refill()
        {
            var now    = DateTime.UtcNow;
            double ms  = (now - _last).TotalMilliseconds;
            _tokens    = Math.Min(_burst, _tokens + ms * _rate);
            _last      = now;
        }

        public void Dispose() => _gate.Dispose();
    }
}
