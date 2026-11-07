using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GameShelf
{
    [DataContract]
    public sealed class Game
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string ExecutablePath { get; set; }

        [DataMember] public string CoverFile { get; set; }
        [DataMember] public string LastLaunchedUtc { get; set; }

        public Game Copy()
        {
            return (Game)MemberwiseClone();
        }

        public DateTime? LaunchTime
        {
            get
            {
                DateTime value;
                if (DateTime.TryParseExact(LastLaunchedUtc, "o", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out value))
                {
                    return value.ToUniversalTime();
                }
                return null;
            }
        }
    }

    [DataContract]
    public sealed class LibraryData
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public List<Game> Games { get; set; }
    }

    public sealed class LibraryStore : IDisposable
    {
        private readonly FileStream sessionLock;
        public readonly string Root;
        public string Notice { get; private set; }
        public List<Game> Games { get; private set; }
        public string LibraryPath { get { return Path.Combine(Root, "library.json"); } }
        public string CoversPath { get { return Path.Combine(Root, "covers"); } }

        public LibraryStore(string root)
        {
            Root = Path.GetFullPath(root);
            Directory.CreateDirectory(Root);
            try
            {
                sessionLock = new FileStream(Path.Combine(Root, "session.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException("无法独占打开游戏库。它可能已在另一个窗口中打开，或目录不可写。", ex);
            }

            try
            {
                Directory.CreateDirectory(CoversPath);
                Games = Load();
            }
            catch
            {
                sessionLock.Dispose();
                throw;
            }
        }

        private List<Game> Read(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(LibraryData));
                var data = serializer.ReadObject(stream) as LibraryData;
                if (data == null || data.Version != 1 || data.Games == null)
                {
                    throw new InvalidDataException("游戏库格式或版本不受支持。");
                }
                Validate(data.Games);
                return data.Games;
            }
        }

        private List<Game> Load()
        {
            string backup = LibraryPath + ".bak";
            if (!File.Exists(LibraryPath) && !File.Exists(backup))
            {
                return new List<Game>();
            }

            try
            {
                return Read(LibraryPath);
            }
            catch (Exception primary)
            {
                // Unknown schema must not be replaced with an older backup.
                if (primary is InvalidDataException && primary.Message.Contains("版本"))
                {
                    throw;
                }
                try
                {
                    List<Game> restored = Read(backup);
                    string damaged = LibraryPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
                    if (File.Exists(LibraryPath))
                    {
                        File.Move(LibraryPath, damaged);
                    }
                    File.Copy(backup, LibraryPath);
                    Notice = "游戏库读取异常，已从上一次备份恢复。损坏文件已保留在数据目录；最近一次修改可能需要重新操作。";
                    return restored;
                }
                catch (Exception recovery)
                {
                    throw new InvalidDataException("游戏库无法读取，且没有可用备份。原文件未被覆盖。请保留数据目录并检查 library.json 与 .bak 文件。\n" +
                        primary.Message + "\n" + recovery.Message, primary);
                }
            }
        }

        public static string NormalizeExecutable(string path, bool mustExist)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path.Trim()))
            {
                throw new ArgumentException("请选择游戏启动文件的完整路径。");
            }
            string full = Path.GetFullPath(path.Trim());
            if (!String.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("请选择 .exe 启动文件。第一版不支持快捷方式或脚本。");
            }
            if (mustExist && !File.Exists(full))
            {
                throw new FileNotFoundException("启动文件不存在，请重新选择游戏的 .exe 文件。", full);
            }
            return full;
        }

        private static void Validate(List<Game> games)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Game game in games)
            {
                Guid id;
                if (game == null || !Guid.TryParseExact(game.Id, "N", out id) || !ids.Add(game.Id))
                {
                    throw new InvalidDataException("游戏标识无效或重复。");
                }
                if (String.IsNullOrWhiteSpace(game.Name) || game.Name.Length > 100)
                {
                    throw new ArgumentException("游戏名称需要为 1–100 个字符。");
                }
                string path = NormalizeExecutable(game.ExecutablePath, false);
                if (!paths.Add(path))
                {
                    throw new ArgumentException("这个启动文件已经在游戏库中。");
                }
                Guid coverId;
                if (!String.IsNullOrEmpty(game.CoverFile) &&
                    (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(game.CoverFile), "N", out coverId) ||
                    game.CoverFile != Path.GetFileName(game.CoverFile) || Path.GetExtension(game.CoverFile) != ".png"))
                {
                    throw new InvalidDataException("封面文件名无效。");
                }
                if (!String.IsNullOrEmpty(game.LastLaunchedUtc) && !game.LaunchTime.HasValue)
                {
                    throw new InvalidDataException("最近启动时间格式无效。");
                }
            }
        }

        public void Save(List<Game> next)
        {
            Validate(next);
            string temporary = LibraryPath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(LibraryData)).WriteObject(stream,
                        new LibraryData { Version = 1, Games = next });
                    stream.Flush(true);
                }
                if (File.Exists(LibraryPath))
                {
                    File.Replace(temporary, LibraryPath, LibraryPath + ".bak");
                }
                else
                {
                    File.Move(temporary, LibraryPath);
                }
                Games = next.Select(g => g.Copy()).ToList();
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        public List<Game> Snapshot()
        {
            return Games.Select(g => g.Copy()).ToList();
        }

        public void Upsert(Game game)
        {
            game.ExecutablePath = NormalizeExecutable(game.ExecutablePath, true);
            game.Name = (game.Name ?? "").Trim();
            var next = Snapshot();
            int index = next.FindIndex(g => g.Id == game.Id);
            if (index >= 0)
            {
                next[index] = game.Copy();
            }
            else
            {
                next.Add(game.Copy());
            }
            Save(next);
        }

        public void Remove(string id)
        {
            Save(Snapshot().Where(g => g.Id != id).ToList());
        }

        public void MarkLaunched(string id, DateTime utc)
        {
            var next = Snapshot();
            Game game = next.First(g => g.Id == id);
            game.LastLaunchedUtc = utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            Save(next);
        }

        public List<Game> Query(bool recent, string search)
        {
            var comparer = Comparer<string>.Create((a, b) => CultureInfo.GetCultureInfo("zh-CN")
                .CompareInfo.Compare(a, b, CompareOptions.IgnoreCase));
            string query = (search ?? "").Trim();
            IEnumerable<Game> result = Games.Where(g => g.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            if (recent)
            {
                return result.Where(g => g.LaunchTime.HasValue).OrderByDescending(g => g.LaunchTime.Value)
                    .ThenBy(g => g.Name, comparer).ThenBy(g => g.Id).ToList();
            }
            return result.OrderBy(g => g.Name, comparer).ThenBy(g => g.Id).ToList();
        }

        public void Dispose()
        {
            sessionLock.Dispose();
        }
    }

    public static class GameLauncher
    {
        public static void Start(Game game)
        {
            string path = LibraryStore.NormalizeExecutable(game.ExecutablePath, true);
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = true
            }))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Windows 未返回游戏进程，未记录启动时间。");
                }
            }
        }

        public static void OpenFolder(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("目录不存在。游戏可能已经移动，请编辑并重新选择启动文件。");
            }
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
    }
}
