using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VPNTool
{
    public static class UITheme
    {
        // SVG Geometries
        public const string SvgTrash = "M 6 19 C 6 20.1 6.9 21 8 21 L 16 21 C 17.1 21 18 20.1 18 19 L 18 7 L 6 7 Z M 19 4 L 15.5 4 L 14.5 3 L 9.5 3 L 8.5 4 L 5 4 L 5 6 L 19 6 Z";
        public const string SvgLock = "M 18 8 H 17 V 6 C 17 3.24 14.76 1 12 1 C 9.24 1 7 3.24 7 6 V 8 H 6 C 4.9 8 4 8.9 4 10 V 20 C 4 21.1 4.9 22 6 22 H 18 C 19.1 22 20 21.1 20 20 V 10 C 20 8.9 19.1 8 18 8 Z M 9 6 C 9 4.34 10.34 3 12 3 C 13.66 3 15 4.34 15 6 V 8 H 9 V 6 Z M 12 17 C 10.9 17 10 16.1 10 15 C 10 13.9 10.9 13 12 13 C 13.1 13 14 13.9 14 15 C 14 16.1 13.1 17 12 17 Z";
        public const string SvgDownload = "M 19 9 H 15 V 3 H 9 V 9 H 5 L 12 16 L 19 9 Z M 5 18 V 20 H 19 V 18 H 5 Z";
        public const string SvgShieldCheck = "M 12 1 L 3 5 V 11 C 3 16.55 6.84 21.74 12 23 C 17.16 21.74 21 16.55 21 11 V 5 L 12 1 Z M 10 17 L 6 13 L 7.41 11.59 L 10 14.17 L 16.59 7.58 L 18 9 L 10 17 Z";
        public const string SvgCheck = "M 9 16.17 L 4.83 12 L 3.41 13.41 L 9 19 L 21 7 L 19.59 5.59 L 9 16.17 Z";
        public const string SvgAlert = "M 12 2 L 1 21 H 23 L 12 2 Z M 12 6 L 19.53 19 H 4.47 L 12 6 Z M 11 10 V 14 H 13 V 10 H 11 Z M 11 16 V 18 H 13 V 16 H 11 Z";
        public const string SvgClose = "M 19 6.41 L 17.59 5 L 12 10.59 L 6.41 5 L 5 6.41 L 10.59 12 L 5 17.59 L 6.41 19 L 12 13.41 L 17.59 19 L 19 17.59 L 13.41 12 Z";
        public const string SvgChevronLeft = "M 15.41 7.41 L 14 6 L 8 12 L 14 18 L 15.41 16.59 L 10.83 12 Z";
        public const string SvgChevronRight = "M 10 6 L 8.59 7.41 L 13.17 12 L 8.59 16.59 L 10 18 L 16 12 Z";
        public const string SvgDots = "M 9 5 C 9 6.1 8.1 7 7 7 C 5.9 7 5 6.1 5 5 C 5 3.9 5.9 3 7 3 C 8.1 3 9 3.9 9 5 Z M 9 12 C 9 13.1 8.1 14 7 14 C 5.9 14 5 13.1 5 12 C 5 10.9 5.9 10 7 10 C 8.1 10 9 10.9 9 12 Z M 9 19 C 9 20.1 8.1 21 7 21 C 5.9 21 5 20.1 5 19 C 5 17.9 5.9 17 7 17 C 8.1 17 9 17.9 9 19 Z M 19 5 C 19 6.1 18.1 7 17 7 C 15.9 7 15 6.1 15 5 C 15 3.9 15.9 3 17 3 C 18.1 3 19 3.9 19 5 Z M 19 12 C 19 13.1 18.1 14 17 14 C 15.9 14 15 13.1 15 12 C 15 10.9 15.9 10 17 10 C 18.1 10 19 10.9 19 12 Z M 19 19 C 19 20.1 18.1 21 17 21 C 15.9 21 15 20.1 15 19 C 15 17.9 15.9 17 17 17 C 18.1 17 19 17.9 19 19 Z";

        public static Path CreateVectorIcon(string svgData, Brush fillBrush, double size)
        {
            return new Path
            {
                Data = Geometry.Parse(svgData),
                Fill = fillBrush,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static Brush CreateSpaceBackground()
        {
            RadialGradientBrush brush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                GradientOrigin = new Point(0.5, 0.2),
                RadiusX = 1.1,
                RadiusY = 1.1
            };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(15, 23, 42), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(8, 14, 28), 0.5));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(4, 7, 17), 1.0));
            return brush;
        }
    }
}

