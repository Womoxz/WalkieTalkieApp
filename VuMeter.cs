using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    /// <summary>
    /// Medidor de nivel de micrófono. Sin esto no había forma de saber si el
    /// micrófono estaba capturando algo hasta escuchar la grabación.
    /// </summary>
    public class VuMeter : Control
    {
        private float level;
        private float peak;
        private DateTime peakTime;
        private readonly System.Windows.Forms.Timer decayTimer;

        private const int Segments = 24;

        public VuMeter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Theme.Surface;
            Height = 12;

            // El nivel cae solo aunque dejen de llegar muestras.
            decayTimer = new System.Windows.Forms.Timer { Interval = 50 };
            decayTimer.Tick += (s, e) =>
            {
                bool changed = false;

                if (level > 0.001f)
                {
                    level = Math.Max(0f, level - 0.06f);
                    changed = true;
                }
                if (peak > 0.001f && (DateTime.UtcNow - peakTime).TotalMilliseconds > 800)
                {
                    peak = Math.Max(0f, peak - 0.03f);
                    changed = true;
                }
                if (changed) Invalidate();
            };
            decayTimer.Start();
        }

        public void SetLevel(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            if (value > level) level = value;
            if (value > peak)
            {
                peak = value;
                peakTime = DateTime.UtcNow;
            }
            Invalidate();
        }

        public void Reset()
        {
            level = 0;
            peak = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.Clear(BackColor);

            int gap = 2;
            float segWidth = (Width - (Segments - 1) * gap) / (float)Segments;
            if (segWidth <= 0) return;

            int lit = (int)Math.Round(level * Segments);
            int peakSeg = (int)Math.Round(peak * Segments) - 1;

            for (int i = 0; i < Segments; i++)
            {
                float x = i * (segWidth + gap);
                float ratio = i / (float)Segments;

                Color on = ratio switch
                {
                    < 0.65f => Theme.Online,
                    < 0.85f => Color.FromArgb(230, 190, 60),
                    _ => Theme.Danger
                };

                bool isLit = i < lit;
                bool isPeak = i == peakSeg && peakSeg >= 0;

                using var brush = new SolidBrush(
                    isLit || isPeak ? on : Color.FromArgb(70, 71, 77));
                g.FillRectangle(brush, x, 0, segWidth, Height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) decayTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
