using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using NUnit.Framework;

namespace TilesDataTest;

public class TestHelpers
{
    public static string ResolveToOutputDir(string path)
    {
        return Path.Combine(Environment.CurrentDirectory, path);
    }

    public static string ResolveToOutputDir(string path1, string path2)
    {
        return ResolveToOutputDir(Path.Combine(path1, path2));
    }

    public static IEnumerable<TestCaseData> Tilesets()
    {
        var matcher = new Matcher();
        matcher.AddInclude("**/tileset.json");
        var sampleDir = ResolveToOutputDir("3d-tiles-samples", "1.1");
        return
            from path in matcher.GetResultsInFullPath(sampleDir)
            select new TestCaseData(path, File.ReadAllBytes(path));
    }
}
