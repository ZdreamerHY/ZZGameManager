using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GameShelf
{
    internal sealed class MainForm : Form
    {
        private readonly LibraryStore store;
        private readonly FlowLayoutPanel grid;
        private readonly Label pageTitle;
        private readonly Label pageHint;
        private readonly Label count;
        private readonly Label status;
        private readonly Button allButton;
        private readonly Button recentButton;
        private readonly TextBox search;
        private readonly Timer searchTimer;
        private readonly Timer statusTimer;
        private readonly Dictionary<string, DateTime> launchCooldown = new Dictionary<string, DateTime>();
        private bool recent;
        private PaletteForm paletteWindow;

        public MainForm(LibraryStore library)
        {
            store = library;
            string paletteNotice;
            Theme.ApplyPalette(PaletteSettings.Load(store.Root, out paletteNotice));
            Text = "ZZ游戏管理器";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = Theme.Font(9);
            BackColor = Theme.LibraryBackground;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(990, 640);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            var side = new Panel { Dock = DockStyle.Left, Width = 204, BackColor = Theme.Night };
            var brand = Theme.Label("ZZ游戏管理器", 17, Color.White, FontStyle.Bold);
            brand.SetBounds(27, 30, 160, 48);
            side.Controls.Add(brand);
            var brandEnglish = Theme.Label("G A M E  M A N A G E R", 7, Color.FromArgb(165, 165, 165));
            brandEnglish.SetBounds(29, 80, 165, 24);
            side.Controls.Add(brandEnglish);
            var section = Theme.Label("我的收藏室", 8, Color.FromArgb(150, 150, 150));
            section.SetBounds(29, 151, 155, 24);
            side.Controls.Add(section);

            allButton = NavButton("全部游戏", 190);
            recentButton = NavButton("最近启动", 246);
            allButton.Click += (s, e) => { recent = false; RefreshLibrary(); };
            recentButton.Click += (s, e) => { recent = true; RefreshLibrary(); };
            side.Controls.Add(allButton);
            side.Controls.Add(recentButton);

            var bottomSide = new Panel { Dock = DockStyle.Bottom, Height = 172, Padding = new Padding(26, 0, 20, 12) };
            Button data = Theme.Button("打开数据目录");
            data.Dock = DockStyle.Top;
            data.Click += (s, e) => SafeAction(() => GameLauncher.OpenFolder(store.Root));
            bottomSide.Controls.Add(data);
            Button manual = Theme.Button("使用手册");
            manual.SetBounds(26, 47, 156, 34);
            manual.Click += (s, e) => OpenManual();
            bottomSide.Controls.Add(manual);
            var footer = Theme.Label("本地收藏 · 随时出发\nv1.2  /  离线可用", 8, Color.FromArgb(165, 165, 165));
            footer.SetBounds(27, 99, 160, 55);
            bottomSide.Controls.Add(footer);
            side.Controls.Add(bottomSide);

            var main = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32, 0, 24, 0), BackColor = Theme.LibraryBackground, Tag = "library-background" };
            var header = new Panel { Dock = DockStyle.Top, Height = 182 };
            var eyebrow = LibraryLabel("YOUR PERSONAL GAME LIBRARY", 8, false, FontStyle.Bold);
            eyebrow.SetBounds(0, 28, 400, 22);
            header.Controls.Add(eyebrow);
            pageTitle = LibraryLabel("全部游戏", 25, false, FontStyle.Bold);
            pageTitle.SetBounds(0, 55, 450, 53);
            header.Controls.Add(pageTitle);
            pageHint = LibraryLabel("让好游戏，各归其位。", 9);
            pageHint.SetBounds(2, 111, 570, 25);
            header.Controls.Add(pageHint);
            Button add = Theme.LibraryButton("＋ 添加游戏", true);
            add.Size = new Size(134, 42);
            add.Location = new Point(754, 64);
            add.Click += (s, e) => EditGame(null);
            header.Controls.Add(add);
            Button colors = Theme.LibraryButton("界面调色");
            colors.Size = new Size(122, 42);
            colors.Location = new Point(620, 64);
            colors.Click += (s, e) => OpenPalette();
            header.Controls.Add(colors);

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 60 };
            search = new TextBox { Font = Theme.Font(10), BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.LibraryField, ForeColor = Theme.LibraryFieldText, AccessibleName = "搜索游戏名称", Tag = "library-field" };
            search.SetBounds(0, 4, 286, 29);
            toolbar.Controls.Add(search);
            var searchLabel = LibraryLabel("搜索名称  /  Ctrl+F", 8);
            searchLabel.SetBounds(300, 3, 165, 31);
            toolbar.Controls.Add(searchLabel);
            count = LibraryLabel("0 个游戏", 9);
            count.SetBounds(640, 2, 248, 32);
            count.TextAlign = ContentAlignment.MiddleRight;
            toolbar.Controls.Add(count);

            var foot = new Panel { Dock = DockStyle.Bottom, Height = 43 };
            status = LibraryLabel("双击封面启动 · 右键查看更多操作", 8);
            status.Dock = DockStyle.Fill;
            foot.Controls.Add(status);
            grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
                BackColor = Theme.LibraryBackground, Padding = new Padding(0, 0, 0, 16), Tag = "library-background" };
            main.Controls.Add(grid);
            main.Controls.Add(foot);
            main.Controls.Add(toolbar);
            main.Controls.Add(header);
            Controls.Add(main);
            Controls.Add(side);

            // Header layout is based on the actual available width, including after DPI scaling.
            header.Resize += (s, e) =>
            {
                add.Left = header.ClientSize.Width - add.Width;
                colors.Left = add.Left - colors.Width - 12;
            };
            toolbar.Resize += (s, e) => count.Left = toolbar.ClientSize.Width - count.Width;
            searchTimer = new Timer { Interval = 180 };
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); RefreshLibrary(); };
            search.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            statusTimer = new Timer { Interval = 9000 };
            statusTimer.Tick += (s, e) =>
            {
                statusTimer.Stop();
                status.Text = "双击封面启动 · 右键查看更多操作";
                status.ForeColor = Theme.LibraryMuted;
            };
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.F)
                {
                    search.Focus();
                    search.SelectAll();
                    e.SuppressKeyPress = true;
                }
                if (e.Control && e.KeyCode == Keys.N)
                {
                    EditGame(null);
                    e.SuppressKeyPress = true;
                }
                if (e.KeyCode == Keys.F5)
                {
                    RefreshLibrary();
                }
            };
            Shown += (s, e) =>
            {
                add.Left = header.ClientSize.Width - add.Width;
                colors.Left = add.Left - colors.Width - 12;
                count.Left = toolbar.ClientSize.Width - count.Width;
                RefreshLibrary();
                if (!String.IsNullOrEmpty(store.Notice))
                {
                    MessageBox.Show(this, store.Notice, "游戏库已恢复", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                if (!String.IsNullOrEmpty(paletteNotice))
                {
                    ShowStatus(paletteNotice, true);
                }
            };
            FormClosing += (s, e) =>
            {
                if (paletteWindow != null && !paletteWindow.IsDisposed)
                {
                    paletteWindow.Close();
                }
            };
        }

        private Label LibraryLabel(string text, float size, bool card = false, FontStyle style = FontStyle.Regular)
        {
            Label label = Theme.Label(text, size, card ? Theme.LibraryCardText : Theme.LibraryText, style);
            label.Tag = card ? "library-cardtext" : "library-text";
            return label;
        }

        private void OpenPalette()
        {
            if (paletteWindow != null && !paletteWindow.IsDisposed)
            {
                paletteWindow.Activate();
                return;
            }
            paletteWindow = new PaletteForm(store.Root, Theme.Current);
            paletteWindow.PreviewChanged += ApplyLibraryPalette;
            paletteWindow.FormClosed += (s, e) => paletteWindow = null;
            paletteWindow.Show(this);
        }

        internal void ApplyLibraryPalette(PaletteSettings palette)
        {
            Theme.ApplyPalette(palette);
            if (IsDisposed || Disposing)
            {
                return;
            }
            BackColor = Theme.LibraryBackground;
            ApplyControlPalette(this);
            Invalidate(true);
        }

        private void ApplyControlPalette(Control control)
        {
            string role = control.Tag as string;
            if (role == "library-background")
            {
                control.BackColor = Theme.LibraryBackground;
            }
            else if (role == "library-card")
            {
                control.BackColor = Theme.LibraryCard;
            }
            else if (role == "library-text")
            {
                control.ForeColor = Theme.LibraryText;
            }
            else if (role == "library-cardtext")
            {
                control.ForeColor = Theme.LibraryCardText;
            }
            else if (role == "library-field")
            {
                control.BackColor = Theme.LibraryField;
                control.ForeColor = Theme.LibraryFieldText;
            }
            else if (role == "library-launch")
            {
                Theme.StyleLaunchButton((Button)control);
            }
            else if (role == "library-primary" || role == "library-secondary")
            {
                Theme.StyleLibraryButton((Button)control, role == "library-primary");
            }

            var cover = control as CoverView;
            if (cover != null && cover.LibraryPalette)
            {
                cover.Invalidate();
            }
            if (control.ContextMenuStrip != null)
            {
                ContextMenuStrip menu = control.ContextMenuStrip;
                menu.BackColor = Theme.LibraryCard;
                menu.ForeColor = Theme.LibraryCardText;
                foreach (ToolStripItem item in menu.Items)
                {
                    bool launch = item.Text == "启动游戏";
                    item.BackColor = launch ? Theme.LaunchGreen : Theme.LibraryCard;
                    item.ForeColor = launch ? Theme.LaunchText : Theme.LibraryCardText;
                }
            }
            foreach (Control child in control.Controls)
            {
                ApplyControlPalette(child);
            }
        }

        private Button NavButton(string text, int y)
        {
            var button = Theme.Button(text);
            button.SetBounds(16, y, 172, 44);
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 56, 56);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(72, 72, 72);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(15, 0, 0, 0);
            return button;
        }

        private void RefreshLibrary()
        {
            pageTitle.Text = recent ? "最近启动" : "全部游戏";
            pageHint.Text = recent ? "接着上次的冒险，或者再出发一次。" : "让好游戏，各归其位。";
            allButton.BackColor = recent ? Theme.Night : Color.White;
            allButton.ForeColor = recent ? Color.FromArgb(190, 190, 190) : Theme.Ink;
            recentButton.BackColor = recent ? Color.White : Theme.Night;
            recentButton.ForeColor = recent ? Theme.Ink : Color.FromArgb(190, 190, 190);
            allButton.FlatAppearance.MouseOverBackColor = recent ? Color.FromArgb(56, 56, 56) : Color.FromArgb(230, 230, 230);
            allButton.FlatAppearance.MouseDownBackColor = recent ? Color.FromArgb(72, 72, 72) : Color.FromArgb(210, 210, 210);
            recentButton.FlatAppearance.MouseOverBackColor = recent ? Color.FromArgb(230, 230, 230) : Color.FromArgb(56, 56, 56);
            recentButton.FlatAppearance.MouseDownBackColor = recent ? Color.FromArgb(210, 210, 210) : Color.FromArgb(72, 72, 72);

            List<Game> games = store.Query(recent, search.Text);
            count.Text = games.Count + " 个游戏  ·  " + (recent ? "最近启动 ↓" : "名称 A → Z");
            int scrollPosition = grid.VerticalScroll.Value;
            grid.SuspendLayout();
            foreach (Control control in grid.Controls.Cast<Control>().ToArray())
            {
                control.Dispose();
            }
            grid.Controls.Clear();
            if (games.Count == 0)
            {
                AddEmptyState();
            }
            else
            {
                foreach (Game game in games)
                {
                    grid.Controls.Add(CreateCard(game));
                }
            }
            grid.ResumeLayout();
            grid.AutoScrollPosition = new Point(0, scrollPosition);
        }

        private void AddEmptyState()
        {
            bool filtering = !String.IsNullOrWhiteSpace(search.Text);
            var panel = new Panel { Size = new Size(620, 324), Margin = new Padding(0, 14, 0, 0) };
            var art = new CoverView { Size = new Size(185, 114), Location = new Point(0, 8), LibraryPalette = true };
            panel.Controls.Add(art);
            var heading = LibraryLabel(filtering ? "没有找到匹配的游戏" : recent ? "下一场冒险，从游戏库开始" : "这里，留给你的下一场冒险。", 17, false, FontStyle.Bold);
            heading.SetBounds(0, 145, 610, 40);
            panel.Controls.Add(heading);
            var body = LibraryLabel(filtering ? "换个关键词，或清空搜索查看全部游戏。" : recent ?
                "从管理器启动过的游戏，会按时间出现在这里。" : "点击“添加游戏”，选择散落在硬盘里的 .exe 启动文件。\n无需搬动游戏；喜欢的封面也可以一起带来。", 10);
            body.SetBounds(0, 193, 610, 66);
            panel.Controls.Add(body);
            Button action = Theme.LibraryButton(filtering ? "清空搜索" : recent ? "查看全部游戏" : "＋ 添加第一个游戏", true);
            action.SetBounds(0, 275, 178, 40);
            action.Click += (s, e) =>
            {
                if (filtering)
                {
                    search.Clear();
                }
                else if (recent)
                {
                    recent = false;
                    RefreshLibrary();
                }
                else
                {
                    EditGame(null);
                }
            };
            panel.Controls.Add(action);
            ScaleDynamic(panel);
            grid.Controls.Add(panel);
        }

        private Control CreateCard(Game game)
        {
            bool exists = File.Exists(game.ExecutablePath);
            var card = new Panel { Size = new Size(220, 296), BackColor = Theme.LibraryCard, Tag = "library-card",
                Margin = new Padding(0, 0, 18, 20), AccessibleName = game.Name };
            var cover = new CoverView { Dock = DockStyle.Top, Height = 146, Cursor = Cursors.Hand, LibraryPalette = true };
            if (!String.IsNullOrEmpty(game.CoverFile))
            {
                cover.SetImage(Theme.LoadCover(Path.Combine(store.CoversPath, game.CoverFile)));
            }
            cover.DoubleClick += (s, e) => Launch(game);
            card.Controls.Add(cover);

            Label name = LibraryLabel(game.Name, 11, true, FontStyle.Bold);
            name.AutoEllipsis = true;
            name.SetBounds(14, 156, 192, 31);
            name.DoubleClick += (s, e) => Launch(game);
            card.Controls.Add(name);
            Label last = LibraryLabel(game.LaunchTime.HasValue ? "上次  " + game.LaunchTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "还未启动 · 等你出发", 8, true);
            last.SetBounds(14, 190, 192, 24);
            card.Controls.Add(last);
            Label location = LibraryLabel(exists ? "本地游戏" : "文件缺失 · 请重新定位", 8, true);
            location.SetBounds(14, 214, 192, 22);
            card.Controls.Add(location);
            Button launch = exists ? Theme.LaunchButton("▶  启动") : Theme.LibraryButton("重新定位", true);
            launch.SetBounds(14, 249, 129, 33);
            launch.Click += (s, e) =>
            {
                if (File.Exists(game.ExecutablePath))
                {
                    Launch(game);
                }
                else
                {
                    EditGame(game);
                }
            };
            card.Controls.Add(launch);
            Button more = Theme.LibraryButton("···");
            more.AccessibleName = game.Name + " 的更多操作";
            more.SetBounds(154, 249, 52, 33);
            card.Controls.Add(more);

            var menu = new ContextMenuStrip { Font = Theme.Font(9), BackColor = Theme.LibraryCard, ForeColor = Theme.LibraryCardText };
            ToolStripItem launchItem = menu.Items.Add("启动游戏", null, (s, e) => Launch(game));
            launchItem.BackColor = Theme.LaunchGreen;
            launchItem.ForeColor = Theme.LaunchText;
            menu.Items.Add("打开所在文件夹", null, (s, e) => SafeAction(() => GameLauncher.OpenFolder(Path.GetDirectoryName(game.ExecutablePath))));
            menu.Items.Add("编辑游戏 / 重新定位", null, (s, e) => EditGame(game));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("从游戏库移除…", null, (s, e) => RemoveGame(game));
            card.ContextMenuStrip = menu;
            cover.ContextMenuStrip = menu;
            name.ContextMenuStrip = menu;
            more.Click += (s, e) => menu.Show(more, new Point(0, more.Height));
            card.Disposed += (s, e) => menu.Dispose();
            var tips = new ToolTip { AutoPopDelay = 12000 };
            tips.SetToolTip(name, game.Name + "\n" + game.ExecutablePath);
            card.Disposed += (s, e) => tips.Dispose();
            ScaleDynamic(card);
            return card;
        }

        private void ScaleDynamic(Control control)
        {
            float scale;
            using (Graphics graphics = CreateGraphics())
            {
                scale = graphics.DpiX / 96f;
            }
            if (scale != 1)
            {
                control.Scale(new SizeF(scale, scale));
            }
        }

        private void EditGame(Game game)
        {
            using (var form = new EditForm(store, game))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshLibrary();
                    ShowStatus("已保存游戏信息。", false);
                }
            }
        }

        private void Launch(Game game)
        {
            DateTime previous;
            if (launchCooldown.TryGetValue(game.Id, out previous) && (DateTime.UtcNow - previous).TotalSeconds < 2)
            {
                return;
            }
            try
            {
                GameLauncher.Start(game);
                launchCooldown[game.Id] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法启动“" + game.Name + "”：\n" + ex.Message +
                    "\n\n可通过“编辑游戏 / 重新定位”修复路径。", "游戏未启动", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshLibrary();
                return;
            }
            try
            {
                store.MarkLaunched(game.Id, DateTime.UtcNow);
                RefreshLibrary();
                ShowStatus("已启动“" + game.Name + "”。", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "游戏已经启动，但最近启动记录保存失败：\n" + ex.Message,
                    "启动记录未保存", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RemoveGame(Game game)
        {
            if (MessageBox.Show(this, "从游戏库移除“" + game.Name + "”？\n\n游戏文件和存档会保留在原位置。",
                "移除游戏记录", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            SafeAction(() =>
            {
                store.Remove(game.Id);
                RefreshLibrary();
                ShowStatus("已移除记录，游戏文件和存档仍保留。", false);
            });
        }

        private void OpenManual()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用手册.html");
            SafeAction(() =>
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("找不到随附手册，请完整解压交付包。", path);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            });
        }

        private void SafeAction(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "操作未完成：\n" + ex.Message, "ZZ游戏管理器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowStatus(string message, bool warning)
        {
            status.Text = message;
            status.ForeColor = warning ? Theme.LibraryText : Theme.LibraryMuted;
            statusTimer.Stop();
            statusTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                searchTimer.Dispose();
                statusTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
