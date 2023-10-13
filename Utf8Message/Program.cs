using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Utf8Message
{
	struct Message : IEquatable<string>
	{
		public byte Noun, Verb, Cond, Seq, Talker;
		public short Offset;
		public byte refNoun, refVerb, refCond, refSeq;
		public string Text;

		public bool Equals(string other)
		{
			return other.Equals(this.Text);
		}
	}

	static class Program
	{
		static string[] Nouns = new string[256];
		static string[] Verbs = new string[256];
		static string[] Talkers = new string[256];
		static string[] Conds = new string[256];

		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Need an SCI Message resource, or a text file with message data, the kind SV would export.");
				return;
			}
			var inFile = args[0];
			var outFile = (args.Length < 2) ? Path.ChangeExtension(inFile, inFile.EndsWith(".msg", StringComparison.InvariantCultureIgnoreCase) ? ".txt" : ".msg") : args[1];
			if (inFile.EndsWith(".msg", StringComparison.InvariantCultureIgnoreCase))
			{
				Msg2Text(inFile, outFile);
			}
			else
			{
				Text2Msg(inFile, outFile);
			}
		}

		static void Text2Msg(string inFile, string outFile)
		{
			GetRelevantSH(Path.GetFileNameWithoutExtension(inFile));
			var lines = File.ReadAllLines(inFile, Encoding.GetEncoding(1252));
			var utf8 = lines[0] == "!utf8";
			if (utf8)
				lines = File.ReadAllLines(inFile, Encoding.UTF8).Skip(1).ToArray();
			var messages = new List<Message>();
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (line.Length < 3)
				{
					continue;
				}
				if (line.StartsWith("//"))
				{
					continue;
				}
				var data = line.Split('\t');
				var noun = ParseWithSH(data[0], Nouns);
				var verb = ParseWithSH(data[1], Verbs);
				var cond = ParseWithSH(data[2], Conds);
				var seq = byte.Parse(data[3]);
				var talker = ParseWithSH(data[4], Talkers);
				var textBuilder = new StringBuilder(data[5]);
				var refNoun = (byte)0;
				var refVerb = (byte)0;
				var refCond = (byte)0;
				if (i < lines.Length - 1)
				{
					var j = i + 1;
					try
					{
						while (lines[j].StartsWith("\t\t\t\t\t"))
						{
							/* In LSL6 0.MSG, there are newlines after the random quit messages.
							 * These are apparently not preserved by SV's export, so you can't roundtrip them:
							 *	\r\n
							 *	(c) 1993 Sierra On-Line, Inc.\r\n
							 *	Thank you for playing Leisure Suit Larry 6: "Shape Up or Slip Out!"\r\n
							 *	\r\n
							 *	Remember: we did it all with 1's and 0's!\r\n <-- this newline is left out in the export!
							 *	\00
							 *	\r\n
							 *	(c) 1993 Sierra On-Line, Inc.
							 *
							 * We replicate this in Msg2Text() for compatibility reasons.
							 */
							textBuilder.Append("\r\n");
							textBuilder.Append(lines[j].Substring(5));
							i++;
							j++;
						}
					}
					catch (IndexOutOfRangeException)
					{
						//lol
					}
				}
				var text = textBuilder.ToString();
				if (text.StartsWith("[REF "))
				{
					var reference = text.Substring(5);
					var refData = reference.Substring(0, reference.IndexOf(']')).Split(' ');
					refNoun = ParseWithSH(refData[0], Nouns);
					refVerb = ParseWithSH(refData[1], Verbs);
					refCond = ParseWithSH(refData[2], Conds);
					text = string.Empty;
				}
				if (utf8)
				{
					//Normalize where needed.
					text = text.Normalize();
					//TODO: eat leftover combining characters
					text = string.Join("",
						text.ToCharArray().Where(c =>
							char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark
						).ToArray()
					);
				}
				messages.Add(new Message
				{
					Noun = noun, Verb = verb, Cond = cond, Seq = seq, Talker = talker, Text = text,
					refNoun = refNoun, refVerb = refVerb, refCond = refCond, refSeq = 0 //refSeq is *always* zero don't be silly.
				});
			}
			var msg = new BinaryWriter(File.Open(outFile, FileMode.Create));
			msg.Write((short)0x8F); //resource
			msg.Write(4210); //version
			msg.Write((short)0); //file length (filled in later)
			msg.Write((short)42); //last message num
			msg.Write((short)messages.Count);
			msg.Seek(0x0B * messages.Count, SeekOrigin.Current);
			var offsets = new List<short>();
			foreach (var message in messages)
			{
				offsets.Add((short)(msg.BaseStream.Position - 2));
				msg.Write(message.Text.ToCharArray());
				msg.Write((byte)0);
			}
			if (utf8)
			{
				msg.Write("UTF8".ToCharArray());
			}
			msg.Seek(0x0C, SeekOrigin.Begin);
			foreach (var message in messages)
			{
				msg.Write(message.Noun);
				msg.Write(message.Verb);
				msg.Write(message.Cond);
				msg.Write(message.Seq);
				msg.Write(message.Talker);
				msg.Write(offsets[0]);
				msg.Write(message.refNoun);
				msg.Write(message.refVerb);
				msg.Write(message.refCond);
				msg.Write(message.refSeq);
				//msg.Write((int)0x01000000); //ref
				offsets.RemoveAt(0);
			}
			msg.Close();
		}

		static void Msg2Text(string inFile, string outFile)
		{
			GetRelevantSH(Path.GetFileNameWithoutExtension(inFile));
			var msg = new BinaryReader(File.Open(inFile, FileMode.Open));
			msg.BaseStream.Seek(-4, SeekOrigin.End);
			var utf8 = msg.ReadInt32() == 0x38465455;
			var enc = utf8 ? Encoding.UTF8 : Encoding.Default;
			var output = new StreamWriter(outFile, false, enc);
			if (utf8)
			{
				output.WriteLine("!utf8");
			}
			output.WriteLine("//noun\tverb\tcond\tseq\ttalker\tline");
			msg.BaseStream.Seek(0xA, SeekOrigin.Begin);
			var num = msg.ReadInt16();
			var records = new Message[num];
			for (var i = 0; i < num; i++)
			{
				records[i] = new Message
				{
					Noun = msg.ReadByte(), Verb = msg.ReadByte(), Cond = msg.ReadByte(), Seq = msg.ReadByte(),
					Talker = msg.ReadByte(), Offset = msg.ReadInt16(),
					refNoun = msg.ReadByte(), refVerb = msg.ReadByte(), refCond = msg.ReadByte(), refSeq = msg.ReadByte(),
				};
			}
			for (var i = 0; i < num; i++)
			{
				var rec = records[i];
				var origRec = rec;
				msg.BaseStream.Seek(2 + rec.Offset, SeekOrigin.Begin);
				var text = ReadCString(msg, enc);
				
				while (rec.refNoun + rec.refVerb + rec.refCond > 0)
				{
					//resolve reference
					var resolved = false;
					for (var j = 0; j < num; j++)
					{
						if (records[j].Noun == rec.refNoun && records[j].Verb == rec.refVerb && records[j].Cond == rec.refCond)
						{
							rec = records[j];
							resolved = true;
							break;
						}
					}
					if (!resolved)
					{
						text = "*** INVALID REFERENCE ***";
						break;
					}
					msg.BaseStream.Seek(2 + rec.Offset, SeekOrigin.Begin);
					text = string.Format("[REF {0} {1} {2}] {3}", Nouns[rec.Noun], Verbs[rec.Verb], Conds[rec.Cond], ReadCString(msg, enc));
				}
				rec = origRec;

				text = text.Replace("\n", "\n\t\t\t\t\t");

				output.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
					Nouns[rec.Noun], Verbs[rec.Verb], Conds[rec.Cond], rec.Seq, Talkers[rec.Talker], text);
			}
			output.Flush();
			output.Close();
		}

		static string ReadCString(BinaryReader r, Encoding enc)
		{
			var bytes = new List<byte>();
			while (true)
			{
				var i = r.ReadByte();
				if (i == 0)
				{
					break;
				}
				bytes.Add(i);
			}
			return enc.GetString(bytes.ToArray());
		}

		static byte ParseWithSH(string keyword, string[] list)
		{
			for (var i = 0; i < 256; i++)
			{
				if (list[i] == keyword)
				{
					return (byte)i;
				}
			}
			return 0;
		}

		static void ParseRelevantSH(string file, string lineStart, string[] list)
		{
			foreach (var line in File.ReadLines(file))
			{
				if (line.StartsWith(lineStart))
				{
					var a = line;
					var b = a.Substring(a.IndexOf(' ') + 1);
					var c = b.Substring(b.IndexOf(' ') + 1);
					b = b.Substring(0, b.IndexOf(' '));
					c = c.Substring(0, c.Length - 1);
					list[int.Parse(c)] = b;
				}
			}
		}

		static void GetRelevantSH(string basename)
		{
			for (var i = 0; i < 256; i++)
			{
				Nouns[i] = i.ToString();
				Verbs[i] = i.ToString();
				Talkers[i] = i.ToString();
				Conds[i] = i.ToString();
			}
			if (File.Exists("verbs.sh"))
			{
				ParseRelevantSH("verbs.sh", "(define V_", Verbs);
			}
			if (File.Exists("talkers.sh"))
			{
				ParseRelevantSH("talkers.sh", "(define ", Talkers);
			}
			if (File.Exists(basename + ".shm"))
			{
				ParseRelevantSH(basename + ".shm", "(define C_", Conds);
				ParseRelevantSH(basename + ".shm", "(define N_", Nouns);
			}
		}
	}
}
