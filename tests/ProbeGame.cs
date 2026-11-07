using System;
using System.IO;

internal static class ProbeGame
{
    private static void Main()
    {
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launch-proof.txt"),
            Environment.CurrentDirectory);
    }
}
