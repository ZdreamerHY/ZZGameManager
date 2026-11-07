using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameShelf
{
    internal sealed class ColorSurface : Control
    {
        public double Hue { get; set; }
        public double Saturation { get; set; }
        public double Value { get; set; }
        public bool HueOnly { get; set; }
        public event EventHandler ColorChanged;
        private bool dragging;

        public ColorSurface()
        {
            DoubleBuffered = true;
            TabStop = true;
            Cursor = Cursors.Cross;
            SetStyle(ControlStyles.Selectable, true);
            AccessibleRole = AccessibleRole.Slider;
        }

        private void NotifyColor()
        {
            Invalidate();
            if (ColorChanged != null)
            {
                ColorChanged(this, EventArgs.Empty);
            }
        }

        internal void SelectPoint(int x, int y)
        {
            double horizontal = Math.Max(0, Math.Min(1, x / (double)Math.Max(1, Width - 1)));
            if (HueOnly)
            {
                Hue = horizontal * 359.999;
            }
            else
            {
                Saturation = horizontal;
                Value = 1 - Math.Max(0, Math.Min(1, y / (double)Math.Max(1, Height - 1)));
            }
            NotifyColor();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                dragging = true;
                Capture = true;
                SelectPoint(e.X, e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                SelectPoint(e.X, e.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = false;
                Capture = false;
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                dragging = false;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double step = e.Shift ? .1 : .01;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                if (HueOnly)
                {
                    Hue = (Hue + ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) ? -1 : 1) * step * 360 + 360) % 360;
                }
                else
                {
                    if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                    {
                        Saturation = Math.Max(0, Math.Min(1, Saturation + (e.KeyCode == Keys.Left ? -step : step)));
                    }
                    else
                    {
                        Value = Math.Max(0, Math.Min(1, Value + (e.KeyCode == Keys.Down ? -step : step)));
                    }
                }
                e.SuppressKeyPress = true;
                NotifyColor();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2)
            {
                return;
            }
            Graphics g = e.Graphics;
            if (HueOnly)
            {
                for (int x = 0; x < Width; x++)
                {
                    using (var pen = new Pen(PaletteColors.FromHsv(x * 360.0 / Width, 1, 1)))
                    {
                        g.DrawLine(pen, x, 0, x, Height);
                    }
                }
            }
            else
            {
                using (var horizontal = new LinearGradientBrush(ClientRectangle, Color.White,
                    PaletteColors.FromHsv(Hue, 1, 1), LinearGradientMode.Horizontal))
                using (var vertical = new LinearGradientBrush(ClientRectangle, Color.FromArgb(0, 0, 0, 0),
                    Color.Black, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(horizontal, ClientRectangle);
                    g.FillRectangle(vertical, ClientRectangle);
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pointX = (float)(HueOnly ? Hue / 360 : Saturation) * (Width - 1);
            float pointY = HueOnly ? Height / 2f : (float)(1 - Value) * (Height - 1);
            // Keep the complete selector visible even at black/white and hue endpoints.
            pointX = Math.Max(7, Math.Min(Width - 8, pointX));
            pointY = Math.Max(7, Math.Min(Height - 8, pointY));
            using (var outer = new Pen(Color.Black, 5))
            using (var inner = new Pen(Color.White, 3))
            {
                g.DrawEllipse(outer, pointX - 6, pointY - 6, 12, 12);
                g.DrawEllipse(inner, pointX - 6, pointY - 6, 12, 12);
            }
            if (Focused)
            {
                ControlPaint.DrawFocusRectangle(g, new Rectangle(1, 1, Width - 2, Height - 2));
            }
        }
    }

    internal sealed class PaletteForm : Form
    {
        private readonly string root;
        private readonly PaletteSettings original;
        private PaletteSettings draft;
        private readonly ComboBox target;
        private readonly ColorSurface square;
        private readonly ColorSurface hue;
        private readonly TextBox hex;
        private readonly Panel swatch;
        private readonly Label valueLabel;
        private readonly Label error;
        private bool syncing;

        public event Action<PaletteSettings> PreviewChanged;
        public bool Saved { get; private set; }

        public PaletteForm(string dataRoot, PaletteSettings initial)
        {
            root = dataRoot;
            original = initial.Copy();
            draft = initial.Copy();
            Text = "界面调色 · ZZ游戏管理器";
            Font = Theme.Font(9);
            BackColor = Theme.Paper;
            ClientSize = new Size(650, 560);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            Shown += (s, e) =>
            {
                Rectangle area = Screen.FromControl(Owner ?? this).WorkingArea;
                Rectangle parent = Owner == null ? area : Owner.Bounds;
                int x = parent.Left + (parent.Width - Width) / 2;
                int y = parent.Top + (parent.Height - Height) / 2;
                Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, x)),
                    Math.Max(area.Top, Math.Min(area.Bottom - Height, y)));
            };

            Label title = Theme.Label("把界面调成你喜欢的颜色", 17, Theme.Ink, FontStyle.Bold);
            title.SetBounds(26, 18, 600, 40);
            Controls.Add(title);
            Label hint = Theme.Label("拖动圆点实时预览 · 保存后记住配色 · 取消会还原", 9, Theme.Muted);
            hint.SetBounds(28, 65, 595, 28);
            Controls.Add(hint);
            Label targetLabel = Theme.Label("调整位置", 9, Theme.Ink, FontStyle.Bold);
            targetLabel.SetBounds(28, 108, 90, 28);
            Controls.Add(targetLabel);
            target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "选择调色位置" };
            target.Items.AddRange(new object[] { "游戏库背景", "游戏卡片", "启动按钮" });
            target.SetBounds(120, 108, 258, 29);
            Controls.Add(target);

            square = new ColorSurface { AccessibleName = "饱和度和明度色板", AccessibleDescription = "拖动圆点，横向调整饱和度，纵向调整明暗；也可使用方向键" };
            square.SetBounds(28, 153, 350, 240);
            Controls.Add(square);
            hue = new ColorSurface { HueOnly = true, AccessibleName = "色相条", AccessibleDescription = "拖动圆点选择色相；也可使用左右方向键" };
            hue.SetBounds(28, 411, 350, 28);
            Controls.Add(hue);
            Label guide = Theme.Label("向下拖动更深，向左拖动更灰。方向键也能微调。", 8, Theme.Muted);
            guide.SetBounds(28, 447, 355, 35);
            Controls.Add(guide);

            swatch = new Panel();
            swatch.SetBounds(408, 153, 214, 80);
            valueLabel = Theme.Label("", 12, Color.White, FontStyle.Bold);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.TextAlign = ContentAlignment.MiddleCenter;
            swatch.Controls.Add(valueLabel);
            Controls.Add(swatch);
            Label hexLabel = Theme.Label("颜色值  HEX", 8, Theme.Muted);
            hexLabel.SetBounds(408, 246, 210, 25);
            Controls.Add(hexLabel);
            hex = new TextBox { MaxLength = 7, Font = Theme.Font(11), AccessibleName = "十六进制颜色值" };
            hex.SetBounds(408, 277, 214, 30);
            Controls.Add(hex);
            string[] presets = { "#181818", "#282828", "#5A5A5A", "#DADADA" };
            for (int index = 0; index < presets.Length; index++)
            {
                string preset = presets[index];
                Color color = PaletteColors.Parse(preset);
                Button button = Theme.Button("●");
                button.BackColor = color;
                button.ForeColor = PaletteColors.TextFor(color);
                button.AccessibleName = "预设颜色 " + preset;
                button.SetBounds(408 + index * 55, 331, 49, 38);
                button.Click += (s, e) => SetSelectedColor(color);
                Controls.Add(button);
            }
            Label presetsLabel = Theme.Label("近黑 / 深灰 / 灰 / 浅灰", 8, Theme.Muted);
            presetsLabel.SetBounds(408, 378, 214, 28);
            Controls.Add(presetsLabel);
            error = Theme.Label("文字颜色会自动适配背景。", 8, Theme.Muted);
            error.SetBounds(408, 413, 214, 68);
            Controls.Add(error);

            Button reset = Theme.Button("恢复默认配色");
            reset.SetBounds(28, 501, 146, 36);
            reset.Click += (s, e) =>
            {
                draft = PaletteSettings.Default();
                SyncSelectedColor();
                Preview();
            };
            Controls.Add(reset);
            Button cancel = Theme.Button("取消");
            cancel.SetBounds(408, 501, 96, 36);
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);
            Button save = Theme.Button("保存配色", true);
            save.SetBounds(516, 501, 106, 36);
            save.Click += (s, e) => SavePalette();
            Controls.Add(save);
            CancelButton = cancel;
            AcceptButton = save;

            target.SelectedIndexChanged += (s, e) => SyncSelectedColor();
            square.ColorChanged += (s, e) => SetSelectedColor(PaletteColors.FromHsv(square.Hue, square.Saturation, square.Value), false);
            hue.ColorChanged += (s, e) =>
            {
                square.Hue = hue.Hue;
                square.Invalidate();
                SetSelectedColor(PaletteColors.FromHsv(square.Hue, square.Saturation, square.Value), false);
            };
            hex.TextChanged += (s, e) =>
            {
                if (!syncing)
                {
                    Color color;
                    if (PaletteColors.TryParse(hex.Text, out color))
                    {
                        SetSelectedColor(color);
                    }
                    else
                    {
                        error.Text = "请输入完整的 #RRGGBB，例如 #282828。";
                    }
                }
            };
            FormClosed += (s, e) =>
            {
                if (!Saved && PreviewChanged != null)
                {
                    PreviewChanged(original.Copy());
                }
            };
            target.SelectedIndex = 0;
        }

        private string SelectedHex()
        {
            return target.SelectedIndex == 1 ? draft.Card : target.SelectedIndex == 2 ? draft.Launch : draft.Background;
        }

        private void SyncSelectedColor()
        {
            Color color = PaletteColors.Parse(SelectedHex());
            double h, s, v;
            PaletteColors.ToHsv(color, out h, out s, out v);
            square.Hue = h;
            square.Saturation = s;
            square.Value = v;
            hue.Hue = h;
            square.Invalidate();
            hue.Invalidate();
            UpdateValues(color);
        }

        private void UpdateValues(Color color)
        {
            syncing = true;
            hex.Text = PaletteColors.Hex(color);
            syncing = false;
            swatch.BackColor = color;
            valueLabel.ForeColor = PaletteColors.TextFor(color);
            valueLabel.Text = hex.Text;
            error.Text = "文字颜色会自动适配背景。\n只有点击保存才会记住设置。";
        }

        internal void SetSelectedColor(Color color, bool updateNodes = true)
        {
            string value = PaletteColors.Hex(color);
            if (target.SelectedIndex == 1)
            {
                draft.Card = value;
            }
            else if (target.SelectedIndex == 2)
            {
                draft.Launch = value;
            }
            else
            {
                draft.Background = value;
            }
            if (updateNodes)
            {
                SyncSelectedColor();
            }
            else
            {
                UpdateValues(color);
            }
            Preview();
        }

        private void Preview()
        {
            if (PreviewChanged != null)
            {
                PreviewChanged(draft.Copy());
            }
        }

        internal void SavePalette()
        {
            Color color;
            if (!PaletteColors.TryParse(hex.Text, out color))
            {
                error.Text = "颜色值不完整，未保存。请填写 #RRGGBB。";
                hex.Focus();
                return;
            }
            try
            {
                draft.Save(root);
                Saved = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "配色未保存：\n" + ex.Message + "\n可以重试或取消还原。", "保存配色失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
