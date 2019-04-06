using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Minisembler
{
	class Program
	{
		private static BinaryWriter outFile;
		private static Dictionary<string, int> equ = new Dictionary<string, int>();

		static void Process(string inputFilename)
		{
			foreach (var rawLine in File.ReadAllLines(inputFilename))
			{
				var line = rawLine;
				if (line.StartsWith(";"))
					continue;
				if (line.Contains(';'))
					line = line.Remove(line.IndexOf(';'));
				line = line.Trim();
				if (line.Length == 0)
					continue;

				var data = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
				if (data.Length > 2)
				{
					if (data[1].ToLowerInvariant() == "equ")
					{
						equ.Add(data[0], Parse(data[2]));
						continue;
					}
				}
				var firstWord = data[0].ToLowerInvariant();
				switch (firstWord)
				{
					case "include":
						Process(data[1]);
						break;
					case "org":
						outFile.Seek(Parse(data[1]), SeekOrigin.Begin);
						break;
					case "db":
						for (var i = 1; i < data.Length; i++)
							outFile.Write((byte)Parse(data[i]));
						break;
					case "dw":
						for (var i = 1; i < data.Length; i++)
							outFile.Write((short)Parse(data[i]));
						break;
					case "dd":
						for (var i = 1; i < data.Length; i++)
							outFile.Write((int)Parse(data[i]));
						break;
					//TODO: String support? Probably steal SplitQ from my Noxico project to make that easier...
				}
			}
		}

		static int Parse(string word)
		{
			if (equ.ContainsKey(word))
				return equ[word];
			if (word.EndsWith("h"))
				return int.Parse(word.Substring(0, word.Length - 1), System.Globalization.NumberStyles.HexNumber);
			return int.Parse(word);
		}

		static void Main(string[] args)
		{
			var inputFilename = string.Empty;
			var outputFilename  = string.Empty;

			if (args.Length < 1)
			{
				Console.WriteLine("Expected an input filename.");
				return;
			}
			else
			{
				inputFilename = args[0];
				outputFilename = Path.ChangeExtension(inputFilename, ".bin");
				if (args.Length > 1)
					outputFilename= args[1];
			}

			outFile = new BinaryWriter(File.OpenWrite(outputFilename));
			Process(inputFilename);
			outFile.Flush();
			outFile.Close();
		}
	}
}
