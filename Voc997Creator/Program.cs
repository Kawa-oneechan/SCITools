using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Voc997Creator
{
	static class Program
	{
		static void Main(string[] args)
		{
			var inFile = string.Empty;
			var outFile = string.Empty;
			if (args.Length == 0)
			{
				if (File.Exists("997.voc") && !File.Exists("997.txt"))
				{
					inFile = "997.voc";
					outFile = "997.txt";
					Console.WriteLine("Found a 997.VOC. Converting to 997.TXT...");
				}
				else if (!File.Exists("997.voc") && File.Exists("997.txt"))
				{
					inFile = "997.txt";
					outFile = "997.voc";
					Console.WriteLine("Found a 997.TXT. Converting to 997.VOC...");
				}
				else if (File.Exists("997.voc") && File.Exists("997.txt"))
				{
					var vocFI = new FileInfo("997.voc");
					var txtFI = new FileInfo("997.txt");
					if (vocFI.LastWriteTime > txtFI.LastWriteTime)
					{
						inFile = "997.voc";
						outFile = "997.txt";
						Console.WriteLine("Found a 997.VOC newer than 997.TXT...");
					}
					else
					{
						inFile = "997.txt";
						outFile = "997.voc";
						Console.WriteLine("Found a 997.TXT newer than 997.VOC...");
					}
				}
				else
				{
					Console.WriteLine("No explicit file names given and no 997.VOC or 997.TXT to be found.");
				}
			}
			else if (args.Length == 1)
			{
				inFile = args[0];
				if (inFile.EndsWith("voc", StringComparison.InvariantCultureIgnoreCase))
				{
					outFile = Path.ChangeExtension(inFile, "txt");
				}
				else
				{
					outFile = Path.ChangeExtension(inFile, "voc");
				}
				Console.WriteLine("Converting {0} to {1}...", inFile, outFile);
			}
			else if (args.Length == 2)
			{
				inFile = args[0];
				outFile = args[1];
				Console.WriteLine("Converting {0} to {1}...", inFile, outFile);
			}
			else
			{
				Console.WriteLine("What are you doing?");
			}

			if (inFile.EndsWith(".voc", StringComparison.InvariantCultureIgnoreCase))
			{
				int count;
				string[] names;
				using (var reader = new BinaryReader(File.Open(inFile, FileMode.Open)))
				{
					var resType = reader.ReadInt16();
					if (resType != 0x86)
					{
						Console.WriteLine("Input file is not a vocab resource.");
						return;
					}
					count = reader.ReadInt16() + 1;
					var offsets = new Int16[count];
					names = new string[count];
					for (var i = 0; i < count; i++)
					{
						offsets[i] = reader.ReadInt16();
					}
					for (var i = 0; i < count; i++)
					{
						reader.BaseStream.Seek(offsets[i] + 2, SeekOrigin.Begin);
						var length = reader.ReadInt16();
						var selector = new string(reader.ReadChars(length));
						names[i] = selector;
					}
				}
				using (var writer = new StreamWriter(File.Open(outFile, FileMode.OpenOrCreate)))
				{
					var skip = 1;
					for (var i = 0; i < count; i++)
					{
						if (names[i].Contains(' ') || names[i] == "BAD SELECTOR")
						{
							skip++;
						}
						else
						{
							if (skip > 0)
							{
								skip = 0;
								writer.WriteLine("//{0}", i);
							}
							writer.WriteLine(names[i]);
						}
					}
				}
			}
			else
			{
				var lines = File.ReadAllLines(inFile);
				var count = 0;
				foreach (var line in lines)
				{
					if (line.StartsWith("//"))
					{
						count = int.Parse(line.Substring(2));
						continue;
					}
					count++;
				}
				var names = new string[count];
				var i = 0;
				foreach (var line in lines)
				{
					if (line.StartsWith("//"))
					{
						i = int.Parse(line.Substring(2));
						continue;
					}
					names[i] = line;
					i++;
				}
				using (var writer = new BinaryWriter(File.Open(outFile, FileMode.OpenOrCreate)))
				{
					writer.Write((Int16)0x86);
					writer.Write((Int16)(count - 1));
					var offsetToNames = 4 + (names.Length * 2); //plus another two for BAD SELECTOR, y'dig?
					var badSelector = (Int16)(offsetToNames - 2);
					var offsets = new Int16[count];
					writer.BaseStream.Seek(offsetToNames, SeekOrigin.Begin);
					writer.Write((Int16)0xC);
					writer.Write("BAD SELECTOR".ToCharArray());
					for (var j = 0; j < count; j++)
					{
						var name = names[j];
						if (name == null)
						{
							offsets[j] = badSelector;
							continue;
						}
						offsets[j] = (Int16)(writer.BaseStream.Position - 2);
						writer.Write((Int16)name.Length);
						writer.Write(name.ToCharArray());
					}
					writer.BaseStream.Seek(4, SeekOrigin.Begin);
					for (var j = 0; j < count; j++)
					{
						writer.Write(offsets[j]);
					}
				}
			}
		}
	}
}
