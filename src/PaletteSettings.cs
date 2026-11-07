using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GameShelf
{
    [DataContract]
    internal sealed class PaletteSettings
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public string Background { get; set; }
        [DataMember] public string Card { get; set; }
        [DataMember] public string Launch { get; set; }

        public static PaletteSettings Default()
        {
            return new PaletteSettings { Version = 1, Background = "#282828", Card = "#363636", Launch = "#A6FF00" };
        }

        public PaletteSettings Copy()
        {
            return (PaletteSettings)MemberwiseClone();
        }

        public void Validate()
        {
            Color ignored;
            if (Version != 1 || !PaletteColors.TryParse(Background, out ignored) ||
                !PaletteColors.TryParse(Card, out ignored) || !PaletteColors.TryParse(Launch, out ignored))
            {
                throw new InvalidDataException("配色文件格式无效。颜色需要为 #RRGGBB 格式。");
            }
        }

        public static PaletteSettings Load(string root, out string notice)
        {
            notice = null;
            string path = Path.Combine(root, "theme.json");
            if (!File.Exists(path))
            {
                return Default();
            }
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    var result = (PaletteSettings)new DataContractJsonSerializer(typeof(PaletteSettings)).ReadObject(stream);
                    if (result == null)
                    {
                        throw new InvalidDataException("配色为空。");
                    }
                    result.Validate();
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is SerializationException || ex is ArgumentException || ex is UnauthorizedAccessException))
                {
                    throw;
                }
                notice = "配色设置读取失败，已暂用默认深灰配色。游戏库不受影响；原 theme.json 已保留。";
                return Default();
            }
        }

        public void Save(string root)
        {
            Validate();
            string path = Path.Combine(root, "theme.json");
            string temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(PaletteSettings)).WriteObject(stream, this);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, path + ".bak");
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }

    internal static class PaletteColors
    {
        public static bool TryParse(string hex, out Color color)
        {
            color = Color.Empty;
            int value;
            if (hex == null || hex.Length != 7 || hex[0] != '#' ||
                !Int32.TryParse(hex.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }
            color = Color.FromArgb((value >> 16) & 255, (value >> 8) & 255, value & 255);
            return true;
        }

        public static Color Parse(string hex)
        {
            Color color;
            if (!TryParse(hex, out color))
            {
                throw new ArgumentException("颜色需要为 #RRGGBB 格式。");
            }
            return color;
        }

        public static string Hex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        public static Color Mix(Color from, Color to, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb((int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        private static double Linear(byte component)
        {
            double value = component / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        public static Color TextFor(Color background)
        {
            double luminance = .2126 * Linear(background.R) + .7152 * Linear(background.G) + .0722 * Linear(background.B);
            double whiteContrast = 1.05 / (luminance + .05);
            double blackContrast = (luminance + .05) / .05;
            return whiteContrast >= blackContrast ? Color.White : Color.Black;
        }

        public static Color FromHsv(double hue, double saturation, double value)
        {
            hue = ((hue % 360) + 360) % 360;
            saturation = Math.Max(0, Math.Min(1, saturation));
            value = Math.Max(0, Math.Min(1, value));
            double chroma = value * saturation;
            double x = chroma * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - chroma;
            double r = 0, g = 0, b = 0;
            if (hue < 60)
            {
                r = chroma; g = x;
            }
            else if (hue < 120)
            {
                r = x; g = chroma;
            }
            else if (hue < 180)
            {
                g = chroma; b = x;
            }
            else if (hue < 240)
            {
                g = x; b = chroma;
            }
            else if (hue < 300)
            {
                r = x; b = chroma;
            }
            else
            {
                r = chroma; b = x;
            }
            return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        public static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double max = Math.Max(color.R, Math.Max(color.G, color.B));
            double min = Math.Min(color.R, Math.Min(color.G, color.B));
            hue = color.GetHue();
            saturation = max == 0 ? 0 : (max - min) / max;
            value = max / 255.0;
        }
    }
}
