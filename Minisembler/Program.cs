using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Minisembler
{
	static class Program
	{
		private static BinaryWriter outFile;
		private static Dictionary<string, int> equ = new Dictionary<string, int>();

		static void Process(string inputFilename)
		{
			foreach (var rawLine in File.ReadAllLines(inputFilename))
			{
				var line = rawLine;
				if (line.StartsWith(";"))
				{
					continue;
				}
				if (line.Contains(';'))
				{
					line = line.Remove(line.IndexOf(';'));
				}
				line = line.Trim();
				if (line.Length == 0)
				{
					continue;
				}

				var data = SplitQ(line, new[] { ' ', '\t', ',' }, true);
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
						if (data[1].StartsWith("\""))
						{
							data[1] = data[1].Substring(1, data[1].Length - 2);
						}
						Process(data[1]);
						break;
					case "incbin":
						if (data[1].StartsWith("\""))
						{
							data[1] = data[1].Substring(1, data[1].Length - 2);
						}
						outFile.Write(File.ReadAllBytes(data[1]));
						break;
					case "org":
						outFile.Seek(Parse(data[1]), SeekOrigin.Begin);
						break;
					case "db":
					case "byte":
						for (var i = 1; i < data.Length; i++)
						{
							if (data[i].StartsWith("\""))
							{
								outFile.Write(Encoding.GetEncoding(437).GetBytes(data[i].Substring(1, data[i].Length - 2)));
							}
							else
							{
								outFile.Write((byte)Parse(data[i]));
							}
						}
						break;
					case "dw":
					case "word":
						for (var i = 1; i < data.Length; i++)
						{
							outFile.Write((short)Parse(data[i]));
						}
						break;
					case "dd":
					case "dword":
						for (var i = 1; i < data.Length; i++)
						{
							outFile.Write(Parse(data[i]));
						}
						break;
					default:
						Console.WriteLine("Don't know what to do with \"{0}\".", firstWord);
						return;
					//TODO: String support?
				}
			}
		}

		static int Parse(string word)
		{
			if (equ.ContainsKey(word))
			{
				return equ[word];
			}
			if (word.EndsWith("h"))
			{
				return int.Parse(word.Substring(0, word.Length - 1), System.Globalization.NumberStyles.HexNumber);
			}
			return int.Parse(word);
		}

		//"a b \"c d\" e".Split() //returns { "a", "b", "c d", "e" }</example>
		static string[] SplitQ(string input, char[] separator, bool withQuotes)
		{
			var ret = new List<string>();
			var item = new StringBuilder();
			for (var i = 0; i < input.Length; i++)
			{
				if (input[i] == '\"')
				{
					if (withQuotes)
					{
						item.Append('\"');
					}
					i++;
					for (int j = i; j < input.Length; i++, j++)
					{
						if (input[j] == '\"')
						{
							if (withQuotes)
								item.Append('\"');
							break;
						}
						item.Append(input[j]);
					}
				}
				else if (separator.Contains(input[i]))
				{
					if (item.Length > 0)
					{
						ret.Add(item.ToString());
					}
					item.Clear();
				}
				else
				{
					item.Append(input[i]);
				}
			}

			if (item.Length > 0)
			{
				ret.Add(item.ToString());
			}

			return ret.ToArray();
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
				{
					outputFilename = args[1];
				}
			}

			outFile = new BinaryWriter(File.OpenWrite(outputFilename));
			Process(inputFilename);
			outFile.Flush();
			outFile.Close();
		}
	}
}
