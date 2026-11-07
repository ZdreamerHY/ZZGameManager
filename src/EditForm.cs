using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GameShelf
{
    internal sealed class EditForm : Form
    {
        private readonly LibraryStore store;
        private readonly Game draft;
        private readonly TextBox nameBox;
        private readonly TextBox pathBox;
        private readonly CoverView preview;
        private readonly Label coverHint;
        private string newCoverPath;
        private bool clearCover;

        public EditForm(LibraryStore library, Game game)
        {
            store = library;
            draft = game == null ? new Game { Id = Guid.NewGuid().ToString("N") } : game.Copy();
            Text = game == null ? "添加游戏 · ZZ游戏管理器" : "编辑游戏 · ZZ游戏管理器";
            Font = Theme.Font(9);
            BackColor = Theme.Paper;
            ClientSize = new Size(680, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;

            Label heading = Theme.Label(game == null ? "把一段冒险，放进游戏库。" : "整理这段冒险", 17, Theme.Ink, FontStyle.Bold);
            heading.SetBounds(28, 20, 620, 42);
            Controls.Add(heading);
            Label description = Theme.Label("选择 .exe 启动文件，封面可以稍后再补。", 9, Theme.Muted);
            description.SetBounds(28, 66, 620, 26);
            Controls.Add(description);

            AddLabel("启动文件", 28, 110);
            pathBox = new TextBox { Text = draft.ExecutablePath ?? "", Font = Theme.Font(9), AccessibleName = "启动文件路径" };
            pathBox.SetBounds(28, 140, 510, 29);
            Controls.Add(pathBox);
            Button browse = Theme.Button("选择文件");
            browse.SetBounds(549, 136, 104, 36);
            browse.Click += (s, e) => BrowseExecutable();
            Controls.Add(browse);

            AddLabel("游戏名称", 28, 188);
            nameBox = new TextBox { Text = draft.Name ?? "", Font = Theme.Font(10), MaxLength = 100, AccessibleName = "游戏名称" };
            nameBox.SetBounds(28, 220, 368, 30);
            Controls.Add(nameBox);
            Label help = Theme.Label("可自定义名称；相同启动文件只添加一次。", 8.5f, Theme.Muted);
            help.SetBounds(28, 255, 370, 28);
            Controls.Add(help);

            Button chooseCover = Theme.Button("选择封面…");
            chooseCover.SetBounds(28, 307, 126, 36);
            chooseCover.Click += (s, e) => ChooseCover();
            Controls.Add(chooseCover);
            Button reset = Theme.Button("使用默认封面");
            reset.SetBounds(166, 307, 138, 36);
            reset.Click += (s, e) =>
            {
                newCoverPath = null;
                clearCover = true;
                preview.SetImage(null);
                coverHint.Text = "将使用默认山景封面";
            };
            Controls.Add(reset);
            coverHint = Theme.Label("JPG / PNG / BMP，最大 20 MB", 8, Theme.Muted);
            coverHint.SetBounds(28, 350, 375, 30);
            Controls.Add(coverHint);
            preview = new CoverView();
            preview.SetBounds(426, 205, 226, 158);
            preview.AccessibleName = "封面预览";
            if (!String.IsNullOrEmpty(draft.CoverFile))
            {
                preview.SetImage(Theme.LoadCover(Path.Combine(store.CoversPath, draft.CoverFile)));
            }
            Controls.Add(preview);

            Button cancel = Theme.Button("取消");
            cancel.SetBounds(426, 409, 104, 38);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            Button save = Theme.Button("保存游戏", true);
            save.SetBounds(542, 409, 110, 38);
            save.Click += (s, e) => SaveGame();
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
            ActiveControl = pathBox;
        }

        private void AddLabel(string text, int x, int y)
        {
            Label label = Theme.Label(text, 9, Theme.Ink, FontStyle.Bold);
            label.SetBounds(x, y, 360, 26);
            Controls.Add(label);
        }

        private void BrowseExecutable()
        {
            using (var dialog = new OpenFileDialog { Title = "选择游戏的启动文件", Filter = "游戏启动文件 (*.exe)|*.exe", CheckFileExists = true })
            {
                if (File.Exists(pathBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(pathBox.Text);
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                pathBox.Text = dialog.FileName;
                if (String.IsNullOrWhiteSpace(nameBox.Text))
                {
                    nameBox.Text = new DirectoryInfo(Path.GetDirectoryName(dialog.FileName)).Name;
                }
            }
        }

        private void ChooseCover()
        {
            using (var dialog = new OpenFileDialog { Title = "选择封面图片", Filter = "图片 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    // Validate by importing only on Save; preview reads a detached, bounded thumbnail.
                    var file = new FileInfo(dialog.FileName);
                    if (file.Length > 20 * 1024 * 1024)
                    {
                        throw new ArgumentException("封面不能超过 20 MB。");
                    }
                    using (Image image = Image.FromFile(dialog.FileName))
                    {
                        if ((long)image.Width * image.Height > 40000000 || image.Width > 12000 || image.Height > 12000)
                        {
                            throw new ArgumentException("封面尺寸过大，请先缩小图片。");
                        }
                        double ratio = Math.Min(1, Math.Min(1024.0 / image.Width, 1536.0 / image.Height));
                        preview.SetImage(new Bitmap(image, Math.Max(1, (int)(image.Width * ratio)), Math.Max(1, (int)(image.Height * ratio))));
                    }
                    newCoverPath = dialog.FileName;
                    clearCover = false;
                    coverHint.Text = Path.GetFileName(newCoverPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "无法读取封面：" + ex.Message, "封面未更改", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void SaveGame()
        {
            string imported = null;
            try
            {
                draft.ExecutablePath = LibraryStore.NormalizeExecutable(pathBox.Text, true);
                draft.Name = nameBox.Text.Trim();
                if (draft.Name.Length == 0)
                {
                    throw new ArgumentException("请填写游戏名称。");
                }
                if (store.Games.Any(g => g.Id != draft.Id && String.Equals(g.ExecutablePath,
                    draft.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("这个启动文件已经在游戏库中，请编辑已有记录。");
                }
                if (newCoverPath != null)
                {
                    imported = Theme.ImportCover(newCoverPath, store.CoversPath);
                    draft.CoverFile = imported;
                }
                else if (clearCover)
                {
                    draft.CoverFile = null;
                }
                store.Upsert(draft);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                if (imported != null)
                {
                    try { File.Delete(Path.Combine(store.CoversPath, imported)); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                MessageBox.Show(this, "未保存：\n" + ex.Message, "检查游戏信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
