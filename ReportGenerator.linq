<Query Kind="Program">
  <NuGetReference>CsvHelper</NuGetReference>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Collections.Immutable</Namespace>
  <Namespace>System.ComponentModel</Namespace>
  <Namespace>System.Reflection.Emit</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>CsvHelper.Configuration</Namespace>
  <Namespace>CsvHelper</Namespace>
</Query>

void Main()
{
	var path = Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), @"BPlusTree.Benchmarks\bin\Release\net6.0\BenchmarkDotNet.Artifacts\results");
	var csvs = Directory.GetFiles(path, "*report.csv");
	
	var allRecords = csvs.SelectMany(ParseCsv).ToArray();
	var sb = new StringBuilder("| Benchmark |");
	foreach (var type in Types)
	{
		var typeDisplayName = type.StartsWith("ArrayBased") ? "Prototype"
			: type.Replace("ImmutableList", string.Empty);
		sb.Append($" Time Ratio ({typeDisplayName}) | Bytes Allocated Ratio ({typeDisplayName}) |");
	}
	sb.AppendLine();
	sb.Append("| ---- |")
		.Append(string.Join("|", Types.Select(_ => " ---- | ---- ")))
		.AppendLine("|");
	foreach (var benchmark in new[] { "Index", "Add", "Insert", "CreateRange", "AddRange" })
	{
		foreach (var elementType in new[] { "Int32", "String" })
		{
			var sizes = benchmark == "AddRange"
				? new[] { 50, 10_000 }
				: new[] { 5, 512, 10_000 };
			foreach (var size in sizes)
			{
				var sizeString = size > 1000 ? $"{size / 1000}K" : size.ToString();		
				var displayName = $"{benchmark}\\<{elementType}\\> ({sizeString})";
				
				sb.AppendLine(Render($"{benchmark}_{elementType}", size, displayName, allRecords));
			}
		}
	}
	sb.ToString().Dump();
}

string[] Types = new[] { "ArrayBasedImmutableList", "TunnelVisionImmutableList" };

string Render(string name, int size, string displayName, IReadOnlyList<Record> records)
{
	var matches = records.Where(r => r.Benchmark == name && r.Size == size && r.Array != false && (r.AddedSize is null || r.AddedSize == size)).ToArray();
	var baseline = matches.Single(r => r.Method == "ImmutableList");
	var result = new StringBuilder($"| {displayName} |");
	foreach (var type in Types)
	{
		var typeRecord = matches.Single(r => r.Method == type);
		result.Append($" {RenderRatio(typeRecord.Ratio)} |");
		result.Append($" {RenderRatio(typeRecord.AllocatedBytes / (double)baseline.AllocatedBytes)} |");
		
		string RenderRatio(double ratio)
		{
			string style = double.IsNaN(ratio) ? string.Empty
				: (ratio > 0.98 && ratio < 1.02) ? string.Empty
				: ratio < 1.0 ? "**"
				: "*";
			return $"{style}{ratio:0.00}{style}";
		}
	}
	return result.ToString();
}

List<Record> ParseCsv(string file)
{
	using var reader = new CsvHelper.CsvReader(new StreamReader(file), new CsvConfiguration(CultureInfo.InvariantCulture) { HeaderValidated = null, MissingFieldFound = null });
	var result = reader.GetRecords<Record>().ToList();
	result.ForEach(r => r.Path = file);
	return result;
}

class Record
{
	public string Benchmark => System.IO.Path.GetFileNameWithoutExtension(this.Path)
		.Replace("BPlusTree.Benchmarks.ImmutableList", string.Empty)
		.Replace("_-report", string.Empty)
		.Replace("Benchmark", string.Empty);
	public string Method { get; set; }
	public int Size { get; set; }
	public int? AddedSize { get; set; }
	public bool? Array { get; set; }
	public double Ratio { get; set; }
	public string Allocated { get; set; }
	
	public long AllocatedBytes
	{
		get 
		{
			var multiplier = this.Allocated.EndsWith(" B") ? 1
				: this.Allocated.EndsWith(" KB") ? 1000
				: throw new FormatException(this.Allocated);
			return long.Parse(this.Allocated.Substring(0, this.Allocated.IndexOf(' ')).Replace(",", string.Empty)) * multiplier;
		}
	}

	public string Path { get; set; }
}