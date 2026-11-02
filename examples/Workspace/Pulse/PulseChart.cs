using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MySharp.Examples.Pulse;

public sealed class PulseChart(PulseModel model, PulseSeries series, string color, double maximum = 100, PulseSeries? secondary = null) : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var ink = Brush.Parse(color);
        var dim = new SolidColorBrush(Color.Parse(color), .12);
        var second = Brush.Parse("#FF625B");
        var cols = Math.Max(1, (int)(Bounds.Width / 8));
        var rows = Math.Max(1, (int)(Bounds.Height / 8));
        for (var x = 0; x < cols; x++)
        {
            var ago = (int)((cols - 1 - x) * (PulseModel.RangeSeconds[model.Range] - 1d) / Math.Max(1, cols - 1));
            var low = (secondary?.Ago(ago) ?? 0) / maximum;
            var high = low + series.Ago(ago) / maximum;
            for (var y = 0; y < rows; y++)
            {
                var level = (rows - y - .5) / rows;
                context.DrawEllipse(level < low ? second : level < high ? ink : dim, null,
                    new Point(x * 8 + 3, y * 8 + 3), 2, 2);
            }
        }
    }
}

public sealed class PulseMemoryRing : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double[] fractions = [11.3 / 32, 4.48 / 32, 11.12 / 32, 4.03 / 32, 1.07 / 32];
        string[] colors = ["#2DA74D", "#EF9208", "#F24275", "#008AFF", "#DCDCE1"];
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 8;
        var offset = -Math.PI / 2;
        for (var i = 0; i < fractions.Length; i++)
        {
            var end = offset + fractions[i] * Math.Tau;
            var startAngle = offset + .012; var endAngle = end - .012;
            Point At(double angle, double r) => center + new Vector(Math.Cos(angle) * r, Math.Sin(angle) * r);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(At(startAngle, radius + 6), true);
                path.ArcTo(At(endAngle, radius + 6), new Size(radius + 6, radius + 6), 0, endAngle - startAngle > Math.PI, SweepDirection.Clockwise);
                path.LineTo(At(endAngle, radius - 6));
                path.ArcTo(At(startAngle, radius - 6), new Size(radius - 6, radius - 6), 0, endAngle - startAngle > Math.PI, SweepDirection.CounterClockwise);
                path.EndFigure(true);
            }
            context.DrawGeometry(Brush.Parse(colors[i]), null, geometry);
            offset = end;
        }
    }
}
