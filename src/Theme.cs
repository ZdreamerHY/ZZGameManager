using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace GameShelf
{
    internal static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(22, 22, 22);
        public static readonly Color Muted = Color.FromArgb(112, 112, 112);
        public static readonly Color Paper = Color.FromArgb(245, 245, 245);
        public static readonly Color Accent = Color.FromArgb(22, 22, 22);
        public static readonly Color Night = Color.FromArgb(16, 16, 16);

        private static PaletteSettings current = PaletteSettings.Default();
        public static PaletteSettings Current { get { return current.Copy(); } }

        public static Color LaunchGreen { get; private set; }
        public static Color LaunchText { get; private set; }
        public static Color LibraryBackground { get; private set; }
        public static Color LibraryCard { get; private set; }
        public static Color LibraryField { get; private set; }
        public static Color LibraryText { get; private set; }
        public static Color LibraryMuted { get { return LibraryText; } }
        public static Color LibraryCardText { get; private set; }
        public static Color LibraryFieldText { get; private set; }

        static Theme()
        {
            ApplyPalette(current);
        }

        public static void ApplyPalette(PaletteSettings palette)
        {
            palette.Validate();
            current = palette.Copy();
            LibraryBackground = PaletteColors.Parse(palette.Background);
            LibraryCard = PaletteColors.Parse(palette.Card);
            LibraryText = PaletteColors.TextFor(LibraryBackground);
            LibraryCardText = PaletteColors.TextFor(LibraryCard);
            LibraryField = PaletteColors.Mix(LibraryCard, LibraryCardText, .12);
            LibraryFieldText = PaletteColors.TextFor(LibraryField);
            LaunchGreen = PaletteColors.Parse(palette.Launch);
            LaunchText = PaletteColors.TextFor(LaunchGreen);
        }

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        public static Label Label(string text, float size, Color color, FontStyle style = FontStyle.Regular)
        {
            return new Label { Text = text, Font = Font(size, style), ForeColor = color,
                AutoSize = false, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
        }

        public static Button Button(string text, bool primary = false)
        {
            var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, Font = Font(9),
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink,
                Cursor = Cursors.Hand, Height = 36, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(54, 54, 54) : Color.FromArgb(238, 238, 238);
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(72, 72, 72) : Color.FromArgb(224, 224, 224);
            return button;
        }

        public static Button LaunchButton(string text)
        {
            Button button = Button(text, true);
            button.Tag = "library-launch";
            button.Font = Font(9, FontStyle.Bold);
            StyleLaunchButton(button);
            return button;
        }

        public static void StyleLaunchButton(Button button)
        {
            button.BackColor = LaunchGreen;
            button.ForeColor = LaunchText;
            button.FlatAppearance.MouseOverBackColor = PaletteColors.Mix(LaunchGreen, LaunchText, .08);
            button.FlatAppearance.MouseDownBackColor = PaletteColors.Mix(LaunchGreen, LaunchText, .16);
        }

        public static Button LibraryButton(string text, bool primary = false)
        {
            Button button = Button(text, primary);
            button.Tag = primary ? "library-primary" : "library-secondary";
            StyleLibraryButton(button, primary);
            return button;
        }

        public static void StyleLibraryButton(Button button, bool primary)
        {
            button.BackColor = primary ? LibraryText : LibraryField;
            button.ForeColor = primary ? LibraryBackground : LibraryFieldText;
            button.FlatAppearance.BorderColor = PaletteColors.Mix(LibraryField, LibraryFieldText, .35);
            button.FlatAppearance.MouseOverBackColor = PaletteColors.Mix(button.BackColor, button.ForeColor, .08);
            button.FlatAppearance.MouseDownBackColor = PaletteColors.Mix(button.BackColor, button.ForeColor, .16);
        }

        public static Image LoadCover(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                using (Image original = Image.FromFile(path))
                {
                    return new Bitmap(original);
                }
            }
            catch (Exception ex)
            {
                if (ex is OutOfMemoryException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    return null;
                }
                throw;
            }
        }

        public static string ImportCover(string path, string coversDirectory)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 20 * 1024 * 1024)
            {
                throw new ArgumentException("封面需要为不超过 20 MB 的本地图片。");
            }
            using (Image original = Image.FromFile(path))
            {
                if ((long)original.Width * original.Height > 40000000 || original.Width > 12000 || original.Height > 12000)
                {
                    throw new ArgumentException("图片尺寸过大，请使用边长不超过 12000 像素、总计不超过 4000 万像素的封面。");
                }
                double ratio = Math.Min(1, Math.Min(1024.0 / original.Width, 1536.0 / original.Height));
                using (var bitmap = new Bitmap(Math.Max(1, (int)(original.Width * ratio)), Math.Max(1, (int)(original.Height * ratio))))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(238, 238, 238));
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(original, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    string filename = Guid.NewGuid().ToString("N") + ".png";
                    bitmap.Save(Path.Combine(coversDirectory, filename), ImageFormat.Png);
                    return filename;
                }
            }
        }
    }

    internal sealed class CoverView : Control
    {
        private Image cover;
        public string Caption { get; set; }
        public bool LibraryPalette { get; set; }

        public CoverView()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(220, 220, 220);
            AccessibleName = "游戏封面，双击启动";
        }

        public void SetImage(Image image)
        {
            if (cover != null)
            {
                cover.Dispose();
            }
            cover = image;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (cover != null)
            {
                double scale = Math.Max(Width / (double)cover.Width, Height / (double)cover.Height);
                float width = (float)(cover.Width * scale);
                float height = (float)(cover.Height * scale);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(cover, (Width - width) / 2, (Height - height) / 2, width, height);
                return;
            }

            float w = Width;
            float h = Height;
            using (var brush = new LinearGradientBrush(ClientRectangle,
                LibraryPalette ? Theme.LibraryField : Color.FromArgb(237, 237, 237),
                LibraryPalette ? Theme.LibraryBackground : Color.FromArgb(191, 191, 191), 100))
            {
                g.FillRectangle(brush, ClientRectangle);
            }
            using (var sun = new SolidBrush(Color.White))
            using (var far = new SolidBrush(Color.FromArgb(156, 156, 156)))
            using (var near = new SolidBrush(Color.FromArgb(93, 93, 93)))
            using (var front = new SolidBrush(LibraryPalette ? Theme.LibraryText : Color.FromArgb(43, 43, 43)))
            {
                g.FillEllipse(sun, w * .68f, h * .16f, h * .24f, h * .24f);
                g.FillPolygon(far, new[] { new PointF(0, h * .8f), new PointF(w * .38f, h * .28f),
                    new PointF(w * .76f, h), new PointF(0, h) });
                g.FillPolygon(near, new[] { new PointF(w * .2f, h), new PointF(w * .75f, h * .5f),
                    new PointF(w, h * .78f), new PointF(w, h) });
                g.FillEllipse(front, -w * .25f, h * .83f, w * 1.4f, h * .65f);
            }
            using (Font font = Theme.Font(8, FontStyle.Bold))
            using (var brush = new SolidBrush(LibraryPalette ? Theme.LibraryFieldText : Theme.Ink))
            {
                g.DrawString("ZZ  /  GAME MANAGER", font, brush, 14, 12);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && cover != null)
            {
                cover.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
