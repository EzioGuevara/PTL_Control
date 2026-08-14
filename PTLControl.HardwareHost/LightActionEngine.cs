using System;
using System.Collections.Generic;
using System.Threading;

namespace PTLControl.HardwareHost
{
    internal sealed class LightActionEngine : IDisposable
    {
        private readonly SerialHardwareService _serial;
        private readonly object _sync = new object();
        private readonly Dictionary<string, LightState> _states = new Dictionary<string, LightState>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly Thread _thread;
        private MarqueeState _marquee;
        private bool _disposed;

        public LightActionEngine(SerialHardwareService serial)
        {
            _serial = serial;
            _thread = new Thread(Run) { IsBackground = true, Name = "PTL-Light-State" };
            _thread.Start();
        }

        public int ActiveStateCount { get { lock (_sync) return _states.Count; } }
        public bool IsMarqueeActive { get { lock (_sync) return _marquee != null; } }

        public void SetLight(int layer, int index, int r, int g, int b)
        {
            Validate(layer, index, r, g, b);
            lock (_sync)
            {
                _marquee = null;
                _states[Key(layer, index)] = new LightState
                {
                    Layer = layer, Index = index, R = r, G = g, B = b,
                    Blink = false, On = true, Dirty = true
                };
            }
            _signal.Set();
        }

        public void SetBlink(int layer, int index, int r, int g, int b, int intervalMs)
        {
            Validate(layer, index, r, g, b);
            intervalMs = Math.Max(50, Math.Min(intervalMs, 60000));
            lock (_sync)
            {
                _marquee = null;
                _states[Key(layer, index)] = new LightState
                {
                    Layer = layer, Index = index, R = r, G = g, B = b,
                    Blink = true, IntervalMs = intervalMs, On = true, Dirty = true,
                    NextChangeUtc = DateTime.UtcNow.AddMilliseconds(intervalMs)
                };
            }
            _signal.Set();
        }

        public void TurnOff(int layer, int index) => SetLight(layer, index, 0, 0, 0);

        public void AllOff()
        {
            lock (_sync) { _states.Clear(); _marquee = null; }
            _serial.SendPriority("<OFF>");
        }

        public void StartMarquee(int r, int g, int b, int intervalMs, IList<StripDefinition> strips)
        {
            var points = new List<LightPoint>();
            if (strips != null)
                foreach (var strip in strips)
                    for (var index = 0; index < Math.Max(0, strip.Count); index++)
                        points.Add(new LightPoint { Layer = strip.Layer, Index = index });
            if (points.Count == 0) throw new InvalidOperationException("跑马灯没有可用灯位。");
            lock (_sync)
            {
                _states.Clear();
                _marquee = new MarqueeState
                {
                    R = Clamp(r), G = Clamp(g), B = Clamp(b),
                    IntervalMs = Math.Max(50, Math.Min(intervalMs, 60000)),
                    Points = points, Position = 0, NextChangeUtc = DateTime.UtcNow
                };
            }
            _serial.SendPriority("<OFF>");
            _signal.Set();
        }

        public void ReplayAll()
        {
            lock (_sync)
            {
                foreach (var state in _states.Values) state.Dirty = true;
                if (_marquee != null) _marquee.NextChangeUtc = DateTime.UtcNow;
            }
            _signal.Set();
        }

        private void Run()
        {
            while (!_disposed)
            {
                var commands = new List<string>();
                var now = DateTime.UtcNow;
                lock (_sync)
                {
                    if (_marquee != null && now >= _marquee.NextChangeUtc)
                    {
                        var point = _marquee.Points[_marquee.Position];
                        commands.Add("<OFF>");
                        commands.Add(Format(point.Layer, point.Index, _marquee.R, _marquee.G, _marquee.B));
                        _marquee.Position = (_marquee.Position + 1) % _marquee.Points.Count;
                        _marquee.NextChangeUtc = now.AddMilliseconds(_marquee.IntervalMs);
                    }
                    else if (_marquee == null)
                    {
                        foreach (var state in _states.Values)
                        {
                            if (state.Blink && now >= state.NextChangeUtc)
                            {
                                state.On = !state.On;
                                state.Dirty = true;
                                state.NextChangeUtc = now.AddMilliseconds(state.IntervalMs);
                            }
                            if (!state.Dirty) continue;
                            commands.Add(Format(state.Layer, state.Index,
                                state.On ? state.R : 0, state.On ? state.G : 0, state.On ? state.B : 0));
                            state.Dirty = false;
                        }
                    }
                }
                foreach (var command in commands)
                {
                    try { _serial.Send(command); } catch { }
                }
                _signal.WaitOne(20);
            }
        }

        private static void Validate(int layer, int index, int r, int g, int b)
        {
            if (layer < 1 || layer > 255) throw new ArgumentOutOfRangeException(nameof(layer));
            if (index < 0 || index > 65535) throw new ArgumentOutOfRangeException(nameof(index));
            if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
                throw new ArgumentOutOfRangeException("RGB", "RGB 必须介于 0-255。");
        }

        private static string Key(int layer, int index) => layer + "_" + index;
        private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));
        private static string Format(int layer, int index, int r, int g, int b)
            => string.Format("<{0},{1},{2},{3},{4}>", layer, index, r, g, b);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _signal.Set();
            try { _thread.Join(1000); } catch { }
            _signal.Dispose();
        }

        private sealed class LightState
        {
            public int Layer, Index, R, G, B, IntervalMs;
            public bool Blink, On, Dirty;
            public DateTime NextChangeUtc;
        }
        private sealed class MarqueeState
        {
            public int R, G, B, IntervalMs, Position;
            public List<LightPoint> Points;
            public DateTime NextChangeUtc;
        }
        private sealed class LightPoint { public int Layer, Index; }
    }

    internal sealed class StripDefinition { public int Layer { get; set; } public int Count { get; set; } }
}
