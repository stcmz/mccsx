using System.Drawing;
using System.Drawing.Drawing2D;

namespace mccsx.Extensions
{
    public static class GraphicsExtensions
    {
        public static void DrawStringRotated(this Graphics g, string s, float angle, Font font, Brush brush, RectangleF layoutRectangle, StringFormat format)
        {
            g.RotateTransform(angle);
            g.DrawString(s, font, brush, layoutRectangle, format);
            g.ResetTransform();
        }

        public static void DrawStringRotated(this Graphics g, string s, float angle, Font font, Brush brush, RectangleF layoutRectangle)
        {
            g.RotateTransform(angle);
            g.DrawString(s, font, brush, layoutRectangle);
            g.ResetTransform();
        }

        public static void DrawStringRotated(this Graphics g, string s, float angle, Font font, Brush brush, PointF point, StringFormat format)
        {
            g.RotateTransform(angle);
            g.TranslateTransform(point.X, point.Y, MatrixOrder.Append);
            g.DrawString(s, font, brush, 0, 0, format);
            g.ResetTransform();
        }

        public static void DrawStringRotated(this Graphics g, string s, float angle, Font font, Brush brush, PointF point)
        {
            g.RotateTransform(angle);
            g.TranslateTransform(point.X, point.Y, MatrixOrder.Append);
            g.DrawString(s, font, brush, 0, 0);
            g.ResetTransform();
        }

        public static void DrawStringRotated(this Graphics g, string s, float angle, Font font, Brush brush, float x, float y, StringFormat format)
        {
            g.RotateTransform(angle);
            g.TranslateTransform(x, y, MatrixOrder.Append);
            g.DrawString(s, font, brush, 0, 0, format);
            g.ResetTransform();
        }

        public static void DrawStringRotated(this Graphics g, string s, float angle, Font font, Brush brush, float x, float y)
        {
            g.RotateTransform(angle);
            g.TranslateTransform(x, y, MatrixOrder.Append);
            g.DrawString(s, font, brush, 0, 0);
            g.ResetTransform();
        }
    }
}
