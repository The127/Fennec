namespace Fennec.App.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

public class FederationBackground : Control
{
    class Node
    {
        public double X;
        public double Y;
        public double Z;
    }

    class Edge
    {
        public Node A;
        public Node B;
        public double Progress;
        public double Speed;
    }

    readonly List<Node> _nodes = new();
    readonly List<Edge> _edges = new();

    readonly Random _rand = new();
    readonly DispatcherTimer _timer;

    double _rotation;

    const double CameraDistance = 3;

    public FederationBackground()
    {
        for (int i = 0; i < 40; i++)
        {
            _nodes.Add(new Node
            {
                X = (_rand.NextDouble() - 0.5) * 2,
                Y = (_rand.NextDouble() - 0.5) * 2,
                Z = (_rand.NextDouble() - 0.5) * 2
            });
        }

        for (int i = 0; i < 80; i++)
        {
            var a = _nodes[_rand.Next(_nodes.Count)];
            var b = _nodes[_rand.Next(_nodes.Count)];

            if (a == b)
                continue;

            _edges.Add(new Edge
            {
                A = a,
                B = b,
                Progress = _rand.NextDouble(),
                Speed = 0.02 + _rand.NextDouble() * 0.03
            });
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };

        _timer.Tick += (_, _) =>
        {
            _rotation += 0.0005;

            foreach (var e in _edges)
            {
                e.Progress += e.Speed * 0.016;
                if (e.Progress > 1)
                    e.Progress = 0;
            }

            InvalidateVisual();
        };

        ActualThemeVariantChanged += (_, __) => InvalidateVisual();

        _timer.Start();
    }

    public override void Render(DrawingContext ctx)
    {
        var rect = Bounds;

        DrawBackground(ctx, rect);
        DrawEdges(ctx, rect);
        DrawNodes(ctx, rect);
    }

    void DrawBackground(DrawingContext ctx, Rect rect)
    {
        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(
                    dark ? Color.Parse("#1b1f2e") : Color.Parse("#eef1f7"),0),
                new GradientStop(
                    dark ? Color.Parse("#0d0f15") : Color.Parse("#d9e0f0"),1)
            }
        };

        ctx.FillRectangle(gradient, rect);
    }

    void DrawNodes(DrawingContext ctx, Rect rect)
    {
        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

        foreach (var n in _nodes)
        {
            var p = Project(n, rect);

            double size = 3 * p.Scale;

            var alpha = (byte)(255 * p.Scale);

            var color = dark
                ? Color.FromArgb(alpha, 200, 200, 200)
                : Color.FromArgb(alpha, 40, 40, 40);

            var brush = new SolidColorBrush(color);

            // soft halo (fake blur)
            ctx.DrawEllipse(
                new SolidColorBrush(Color.FromArgb((byte)(40 * p.Scale), 255, 255, 255)),
                null,
                p.Point,
                size * 2.5,
                size * 2.5);

            // core node
            ctx.DrawEllipse(
                brush,
                null,
                p.Point,
                size,
                size);
        }
    }

    void DrawEdges(DrawingContext ctx, Rect rect)
    {
        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

        foreach (var e in _edges)
        {
            var a = Project(e.A, rect);
            var b = Project(e.B, rect);

            var depth = Math.Min(a.Scale, b.Scale);

            var alpha = (byte)(120 * depth);

            var lineColor = dark
                ? Color.FromArgb(alpha, 255, 255, 255)
                : Color.FromArgb(alpha, 80, 80, 80);

            var pen = new Pen(
                new SolidColorBrush(lineColor),
                1);

            ctx.DrawLine(pen, a.Point, b.Point);

            var x = a.Point.X + (b.Point.X - a.Point.X) * e.Progress;
            var y = a.Point.Y + (b.Point.Y - a.Point.Y) * e.Progress;

            var packetAlpha = (byte)(200 * depth);

            var packetColor = dark
                ? Color.FromArgb(packetAlpha, 80, 220, 255)
                : Color.FromArgb(packetAlpha, 0, 120, 255);

            var packetBrush = new SolidColorBrush(packetColor);

            ctx.DrawEllipse(
                packetBrush,
                null,
                new Point(x, y),
                2.5,
                2.5);
        }
    }

    (Point Point, double Scale) Project(Node n, Rect rect)
    {
        double cos = Math.Cos(_rotation);
        double sin = Math.Sin(_rotation);

        double x = n.X * cos - n.Z * sin;
        double z = n.X * sin + n.Z * cos;
        double y = n.Y;

        double scale = 1 / (z + CameraDistance);

        double sx = rect.Center.X + x * rect.Width * 2 * scale;
        double sy = rect.Center.Y + y * rect.Height * 2 * scale;

        return (new Point(sx, sy), scale);
    }
}