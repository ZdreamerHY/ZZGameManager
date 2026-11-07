using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using GameShelf;

internal static class Tests
{
    private static int passed;
    private static string root;
    private static string executable;

    private static void Check(bool value, string name)
    {
        if (!value)
        {
            throw new Exception("FAIL: " + name);
        }
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception) { rejected = true; }
        Check(rejected, name);
    }

    private static Game Create(string name, string path)
    {
        return new Game { Id = Guid.NewGuid().ToString("N"), Name = name, ExecutablePath = path };
    }

    private static T Field<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }

    private static void Invoke(object owner, string name)
    {
        owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            root = Path.GetFullPath(args[0]);
            string fixtures = Path.Combine(root, "中文 游戏 & 空格");
            Directory.CreateDirectory(fixtures);
            executable = Path.Combine(fixtures, "Game.exe");
            File.Copy(args[1], executable, true);
            string secondPath = Path.Combine(fixtures, "Other.exe");
            File.Copy(args[1], secondPath, true);
            string thirdPath = Path.Combine(fixtures, "Third.exe");
            File.Copy(args[1], thirdPath, true);
            string data = Path.Combine(root, "library");

            Game first = Create("月光旅人", executable);
            Game second = Create("森林物语", secondPath);
            Game third = Create("白昼之梦", thirdPath);
            using (var store = new LibraryStore(data))
            {
                Check(store.Games.Count == 0, "first run starts empty");
                Reject(() => { using (var other = new LibraryStore(data)) { } }, "exclusive library lock");
                store.Upsert(first);
                store.Upsert(second);
                store.Upsert(third);
                Check(store.Games.Count == 3, "add three games with Chinese paths");
                Check(File.Exists(store.LibraryPath + ".bak"), "previous snapshot backup");
                Check(String.Join(",", store.Query(false, "").Select(g => g.Name)) == "白昼之梦,森林物语,月光旅人", "Chinese pinyin ordering");
                Check(store.Query(true, "").Count == 0, "unplayed excluded from recent");
                Check(store.Query(false, " 森林 ").Single().Id == second.Id, "trimmed name search");
                Check(store.Query(false, "不存在").Count == 0, "search empty result");

                Reject(() => store.Upsert(Create("重复", executable.ToUpperInvariant())), "case insensitive duplicate prevention");
                Check(store.Games.Count == 3, "duplicate failure preserves memory");
                Reject(() => store.Upsert(Create(" ", executable + ".exe")), "missing executable rejected");
                Reject(() => store.Upsert(Create(" ", executable)), "blank name rejected");
                Reject(() => store.Upsert(Create("too long".PadRight(101, 'x'), executable)), "long name rejected");
                Reject(() => LibraryStore.NormalizeExecutable("Game.exe", false), "relative executable rejected");
                Reject(() => LibraryStore.NormalizeExecutable(Path.Combine(fixtures, "unsafe.cmd"), false), "scripts rejected");

                var badCover = first.Copy();
                badCover.CoverFile = "../outside.png";
                Reject(() => store.Upsert(badCover), "cover path traversal rejected");
                var badDate = first.Copy();
                badDate.LastLaunchedUtc = "invalid";
                Reject(() => store.Upsert(badDate), "invalid timestamp rejected");

                first.Name = "月光旅人 第二章";
                store.Upsert(first);
                Check(store.Games.Single(g => g.Id == first.Id).Name.EndsWith("第二章"), "edit existing record");
                DateTime early = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
                store.MarkLaunched(first.Id, early);
                store.MarkLaunched(second.Id, early.AddHours(2));
                Check(store.Query(true, "").First().Id == second.Id, "recent newest first");
                store.MarkLaunched(first.Id, early.AddHours(3));
                Check(store.Query(true, "").First().Id == first.Id, "relaunch updates order");
                Check(store.Query(true, "森林").Single().Id == second.Id, "search within recent");
                Check(store.Games.First(g => g.Id == first.Id).LastLaunchedUtc.EndsWith("Z"), "UTC timestamp stored");

                string imagePath = Path.Combine(fixtures, "封面.png");
                using (var bitmap = new Bitmap(1400, 1800))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap)) { graphics.Clear(Color.DarkSeaGreen); }
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
                }
                string imported = Theme.ImportCover(imagePath, store.CoversPath);
                using (Image image = Image.FromFile(Path.Combine(store.CoversPath, imported)))
                {
                    Check(image.Width <= 1024 && image.Height <= 1536, "cover resized within bounds");
                }
                first = store.Games.First(g => g.Id == first.Id).Copy();
                first.CoverFile = imported;
                store.Upsert(first);
                File.Delete(imagePath);
                using (Image image = Theme.LoadCover(Path.Combine(store.CoversPath, imported)))
                {
                    Check(image != null, "imported cover survives source deletion");
                }
                string invalidImage = Path.Combine(fixtures, "broken.png");
                File.WriteAllText(invalidImage, "not an image");
                Reject(() => Theme.ImportCover(invalidImage, store.CoversPath), "invalid image rejected");
                Check(Theme.LoadCover(invalidImage) == null, "broken cover falls back");
                Check(Theme.LoadCover(Path.Combine(fixtures, "missing.png")) == null, "missing cover falls back");

                // A directory occupying the temporary file path forces a real write failure.
                Directory.CreateDirectory(store.LibraryPath + ".tmp");
                var fail = first.Copy();
                fail.Name = "must not be committed";
                Reject(() => store.Upsert(fail), "storage failure reported");
                Check(store.Games.First(g => g.Id == first.Id).Name == first.Name, "failed save preserves committed state");
                Directory.Delete(store.LibraryPath + ".tmp");

                GameLauncher.Start(first);
                string proof = Path.Combine(fixtures, "launch-proof.txt");
                for (int i = 0; i < 100 && !File.Exists(proof); i++)
                {
                    Thread.Sleep(50);
                }
                Check(File.Exists(proof), "real test process created");
                Check(File.ReadAllText(proof).Equals(fixtures, StringComparison.OrdinalIgnoreCase), "game working directory is executable directory");
                File.Delete(executable);
                string oldTime = first.LastLaunchedUtc;
                Reject(() => GameLauncher.Start(first), "missing launch fails");
                Check(store.Games.First(g => g.Id == first.Id).LastLaunchedUtc == oldTime, "failed launch does not modify recent");
                Check(store.Query(false, "").Count == 3, "missing executable remains editable in library");
                File.Copy(args[1], executable);

                store.Remove(third.Id);
                Check(store.Games.Count == 2 && File.Exists(thirdPath), "remove leaves game files untouched");
            }

            using (var store = new LibraryStore(data))
            {
                Check(store.Games.Count == 2 && store.Query(true, "").Count == 2, "reopen preserves library and recent");
                first = store.Games.First(g => g.Id == first.Id).Copy();
                first.Name = "change to recover";
                store.Upsert(first);
            }
            string libraryFile = Path.Combine(data, "library.json");
            File.WriteAllText(libraryFile, "{truncated");
            using (var store = new LibraryStore(data))
            {
                Check(store.Notice != null && store.Games.Count == 2, "corrupt primary restores previous backup");
                Check(Directory.GetFiles(data, "*.corrupt-*").Length == 1, "corrupt original preserved");
                Check(store.Games.All(g => g.Name != "change to recover"), "recovery contains previous committed snapshot");
            }
            string fatal = Path.Combine(root, "unrecoverable");
            Directory.CreateDirectory(fatal);
            File.WriteAllText(Path.Combine(fatal, "library.json"), "broken");
            Reject(() => { using (var store = new LibraryStore(fatal)) { } }, "corruption without backup fails closed");
            Check(File.ReadAllText(Path.Combine(fatal, "library.json")) == "broken", "unrecoverable original untouched");
            string future = Path.Combine(root, "future-version");
            Directory.CreateDirectory(future);
            File.WriteAllText(Path.Combine(future, "library.json"), "{\"Version\":2,\"Games\":[]}");
            Reject(() => { using (var store = new LibraryStore(future)) { } }, "unknown schema rejected");
            Check(File.ReadAllText(Path.Combine(future, "library.json")).Contains(":2"), "unknown schema preserved");

            Application.EnableVisualStyles();
            using (var formStore = new LibraryStore(Path.Combine(root, "form-integration")))
            {
                using (var editor = new EditForm(formStore, null))
                {
                    Field<TextBox>(editor, "pathBox").Text = executable;
                    Field<TextBox>(editor, "nameBox").Text = " 表单添加游戏 ";
                    Invoke(editor, "SaveGame");
                    Check(editor.DialogResult == DialogResult.OK && formStore.Games.Single().Name == "表单添加游戏", "real editor save adds trimmed record");
                    Check(String.IsNullOrEmpty(formStore.Games.Single().CoverFile), "editor defaults to generated cover");
                }
                Game editable = formStore.Games.Single().Copy();
                using (var editor = new EditForm(formStore, editable))
                {
                    Field<TextBox>(editor, "nameBox").Text = "cancelled draft";
                }
                Check(formStore.Games.Single().Name == "表单添加游戏", "closing unsaved editor preserves record");
                string formCover = Path.Combine(fixtures, "form-cover.png");
                using (var bitmap = new Bitmap(100, 160))
                {
                    bitmap.Save(formCover, System.Drawing.Imaging.ImageFormat.Png);
                }
                using (var editor = new EditForm(formStore, editable))
                {
                    Field<TextBox>(editor, "nameBox").Text = "表单修复游戏";
                    Field<TextBox>(editor, "pathBox").Text = secondPath;
                    editor.GetType().GetField("newCoverPath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(editor, formCover);
                    Invoke(editor, "SaveGame");
                    Game updated = formStore.Games.Single();
                    Check(updated.Id == editable.Id && updated.ExecutablePath == secondPath && updated.Name == "表单修复游戏", "real editor repairs path preserving identity");
                    Check(File.Exists(Path.Combine(formStore.CoversPath, updated.CoverFile)), "real editor commits imported cover");
                }
                using (var editor = new EditForm(formStore, formStore.Games.Single()))
                {
                    editor.GetType().GetField("clearCover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(editor, true);
                    Invoke(editor, "SaveGame");
                    Check(formStore.Games.Single().CoverFile == null, "real editor restores default cover");
                }
                using (var main = new MainForm(formStore))
                {
                    Invoke(main, "RefreshLibrary");
                    Check(Field<FlowLayoutPanel>(main, "grid").Controls.Count == 1, "main builds game card");
                    Control card = Field<FlowLayoutPanel>(main, "grid").Controls[0];
                    Check(card.ContextMenuStrip.Items.Cast<ToolStripItem>().Any(i => i.Text == "打开所在文件夹"), "card exposes folder action");
                    Field<TextBox>(main, "search").Text = "absent";
                    Invoke(main, "RefreshLibrary");
                    Check(Field<Label>(main, "count").Text.StartsWith("0 个游戏"), "main search refresh shows empty state");
                }
            }

            passed += PaletteTests.Run(root, executable);

            // Reproducible UI fixture, never mixed into the shipped empty library.
            using (var ui = new LibraryStore(Path.Combine(root, "ui-library")))
            {
                var a = Create("白昼之梦", thirdPath);
                var b = Create("森林物语", secondPath);
                var c = Create("月光旅人", executable);
                ui.Upsert(c);
                ui.Upsert(a);
                ui.Upsert(b);
                ui.MarkLaunched(c.Id, DateTime.UtcNow.AddDays(-1));
                ui.MarkLaunched(b.Id, DateTime.UtcNow.AddMinutes(-25));
            }
            Console.WriteLine("ALL PASSED: " + passed);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}
