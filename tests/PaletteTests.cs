using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using GameShelf;

internal static class PaletteTests
{
    private static int passed;

    private static void Check(bool condition, string name)
    {
        if (!condition)
        {
            throw new Exception("FAIL: " + name);
        }
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static T Field<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }

    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception) { rejected = true; }
        Check(rejected, name);
    }

    public static int Run(string testRoot, string executable)
    {
        passed = 0;
        Color parsed;
        Check(PaletteColors.TryParse("#a6ff00", out parsed) && PaletteColors.Hex(parsed) == "#A6FF00", "hex input accepts lowercase and normalizes");
        Check(!PaletteColors.TryParse("#123", out parsed) && !PaletteColors.TryParse("#ZZ0000", out parsed), "partial and invalid hex rejected");
        Check(PaletteColors.FromHsv(0, 1, 1).ToArgb() == Color.Red.ToArgb() &&
            PaletteColors.FromHsv(120, 1, 1).ToArgb() == Color.Lime.ToArgb() &&
            PaletteColors.FromHsv(240, 1, 1).ToArgb() == Color.Blue.ToArgb(), "hue selects primary colors");
        Check(PaletteColors.Hex(PaletteColors.FromHsv(60, 0, 1)) == "#FFFFFF" &&
            PaletteColors.Hex(PaletteColors.FromHsv(60, 1, 0)) == "#000000", "color surface reaches white and black");
        var random = new Random(73);
        bool roundTrip = true;
        for (int i = 0; i < 200; i++)
        {
            Color color = Color.FromArgb(random.Next(256), random.Next(256), random.Next(256));
            double h, s, v;
            PaletteColors.ToHsv(color, out h, out s, out v);
            Color actual = PaletteColors.FromHsv(h, s, v);
            roundTrip &= Math.Abs(actual.R - color.R) <= 1 && Math.Abs(actual.G - color.G) <= 1 && Math.Abs(actual.B - color.B) <= 1;
        }
        Check(roundTrip, "RGB HSV round trip across 200 colors");
        Check(PaletteColors.TextFor(Color.Black) == Color.White && PaletteColors.TextFor(Color.White) == Color.Black &&
            PaletteColors.TextFor(Color.Lime) == Color.Black && PaletteColors.TextFor(Color.Blue) == Color.White,
            "automatic text contrast on dark light and saturated colors");
        using (var surface = new ColorSurface { Size = new Size(101, 101) })
        {
            int notifications = 0;
            surface.ColorChanged += (s, e) => notifications++;
            surface.SelectPoint(-10, 200);
            Check(surface.Saturation == 0 && surface.Value == 0, "drag outside color square clamps to black edge");
            surface.SelectPoint(200, -10);
            Check(surface.Saturation == 1 && surface.Value == 1 && notifications == 2, "drag selects color and emits preview events");
            surface.HueOnly = true;
            surface.SelectPoint(500, 0);
            Check(surface.Hue >= 359 && surface.Hue < 360, "hue drag clamps at rainbow endpoint");
        }

        string settingsRoot = Path.Combine(testRoot, "palette-settings");
        Directory.CreateDirectory(settingsRoot);
        string notice;
        PaletteSettings settings = PaletteSettings.Load(settingsRoot, out notice);
        Check(settings.Background == "#282828" && notice == null && !File.Exists(Path.Combine(settingsRoot, "theme.json")), "no settings uses deeper default without writing");
        settings.Background = "#1A2B3C";
        settings.Save(settingsRoot);
        Check(PaletteSettings.Load(settingsRoot, out notice).Background == "#1A2B3C", "saved palette reloads from disk");
        settings.Card = "#DDDDDD";
        settings.Save(settingsRoot);
        Check(File.Exists(Path.Combine(settingsRoot, "theme.json.bak")) && PaletteSettings.Load(settingsRoot, out notice).Card == "#DDDDDD", "palette atomic replace retains backup");
        string before = File.ReadAllText(Path.Combine(settingsRoot, "theme.json"));
        Directory.CreateDirectory(Path.Combine(settingsRoot, "theme.json.tmp"));
        settings.Background = "#123456";
        Reject(() => settings.Save(settingsRoot), "palette storage failure reported");
        Check(File.ReadAllText(Path.Combine(settingsRoot, "theme.json")) == before, "failed palette save preserves last saved settings");
        Directory.Delete(Path.Combine(settingsRoot, "theme.json.tmp"));
        settings.Background = "#nope00";
        Reject(() => settings.Save(settingsRoot), "invalid palette cannot overwrite settings");
        File.WriteAllText(Path.Combine(settingsRoot, "theme.json"), "{broken");
        settings = PaletteSettings.Load(settingsRoot, out notice);
        Check(settings.Background == "#282828" && notice != null && File.ReadAllText(Path.Combine(settingsRoot, "theme.json")) == "{broken",
            "corrupt settings fall back without replacing original");

        using (var store = new LibraryStore(Path.Combine(testRoot, "palette-ui")))
        {
            store.Upsert(new Game { Id = Guid.NewGuid().ToString("N"), Name = "调色测试", ExecutablePath = executable });
            string originalLibrary = File.ReadAllText(store.LibraryPath);
            using (var main = new MainForm(store))
            {
                main.GetType().GetMethod("RefreshLibrary", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
                PaletteSettings original = Theme.Current;
                var grid = Field<FlowLayoutPanel>(main, "grid");
                Control originalCard = grid.Controls[0];
                using (var editor = new PaletteForm(store.Root, original))
                {
                    editor.PreviewChanged += main.ApplyLibraryPalette;
                    editor.Show();
                    Application.DoEvents();
                    editor.SetSelectedColor(Color.FromArgb(12, 15, 20));
                    Check(main.BackColor == Color.FromArgb(12, 15, 20) && grid.BackColor == main.BackColor, "picker live preview updates actual main background");
                    Check(Object.ReferenceEquals(originalCard, grid.Controls[0]), "drag preview preserves card controls and images");
                    Check(!File.Exists(Path.Combine(store.Root, "theme.json")), "preview is not persisted before save");
                    Field<ComboBox>(editor, "target").SelectedIndex = 1;
                    editor.SetSelectedColor(Color.White);
                    Check(originalCard.BackColor.ToArgb() == Color.White.ToArgb() && originalCard.Controls.OfType<Label>().All(l => l.ForeColor.ToArgb() == Color.Black.ToArgb()), "card text adapts when card is light");
                    Field<ComboBox>(editor, "target").SelectedIndex = 2;
                    editor.SetSelectedColor(Color.Blue);
                    Button launch = originalCard.Controls.OfType<Button>().First(b => b.Text.Contains("启动"));
                    Check(launch.BackColor.ToArgb() == Color.Blue.ToArgb() && launch.ForeColor == Color.White, "launch color and text update in live preview");
                    editor.Close();
                }
                Check(Theme.Current.Background == original.Background && Theme.Current.Card == original.Card && Theme.Current.Launch == original.Launch,
                    "closing picker cancels all previewed targets");
                Check(main.Controls.OfType<Panel>().Single(p => p.Dock == DockStyle.Left).BackColor == Theme.Night, "picker leaves sidebar color unchanged");
                using (var editor = new PaletteForm(store.Root, Theme.Current))
                {
                    editor.PreviewChanged += main.ApplyLibraryPalette;
                    editor.Show();
                    Application.DoEvents();
                    editor.SetSelectedColor(Color.FromArgb(22, 33, 44));
                    editor.SavePalette();
                    Check(editor.Saved && PaletteSettings.Load(store.Root, out notice).Background == "#16212C" && Theme.Current.Background == "#16212C",
                        "save keeps preview and persists selected color");
                }
                string saved = File.ReadAllText(Path.Combine(store.Root, "theme.json"));
                using (var editor = new PaletteForm(store.Root, Theme.Current))
                {
                    editor.PreviewChanged += main.ApplyLibraryPalette;
                    editor.Show();
                    Application.DoEvents();
                    Field<TextBox>(editor, "hex").Text = "#12";
                    editor.SavePalette();
                    Check(!editor.Saved && !editor.IsDisposed && File.ReadAllText(Path.Combine(store.Root, "theme.json")) == saved, "partial hex blocks save and keeps picker open");
                    editor.Close();
                }
                using (var editor = new PaletteForm(store.Root, Theme.Current))
                {
                    editor.PreviewChanged += main.ApplyLibraryPalette;
                    editor.Show();
                    Application.DoEvents();
                    editor.Controls.OfType<Button>().Single(b => b.Text == "恢复默认配色").PerformClick();
                    Check(Theme.Current.Background == "#282828" && Theme.Current.Launch == "#A6FF00", "restore defaults previews complete default palette");
                    editor.Close();
                    Check(Theme.Current.Background == "#16212C", "cancelling default reset restores previous saved palette");
                }
            }
            using (var reopened = new MainForm(store))
            {
                Check(reopened.BackColor == Color.FromArgb(22, 33, 44), "new main window loads persisted palette");
            }
            Check(File.ReadAllText(store.LibraryPath) == originalLibrary, "palette workflow never modifies game library");
        }
        return passed;
    }
}
