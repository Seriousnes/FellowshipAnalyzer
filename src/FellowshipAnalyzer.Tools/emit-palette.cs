#:property PublishAot=false
#:property TargetFramework=net10.0
#:project ../FellowshipAnalyzer.Core.Contracts/FellowshipAnalyzer.Core.Contracts.csproj

using FellowshipAnalyzer.Core.Contracts.Design;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run --no-cache emit-palette.cs <path-to-_palette.scss>");
    return 1;
}

var path = Path.GetFullPath(args[0]);
var scss = FaPaletteScss.Render();
var tokenCount = FaTheme.Original.Tokens.Count();

if (File.Exists(path) && File.ReadAllText(path).ReplaceLineEndings(FaPaletteScss.NewLine.ToString()) == scss)
{
    Console.WriteLine($"{path} is up to date ({tokenCount} tokens x {FaTheme.All.Count} themes).");
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, scss);

Console.WriteLine($"Wrote {path} ({tokenCount} tokens x {FaTheme.All.Count} themes, {FaSemantic.All.Count} semantic classes).");
return 0;
