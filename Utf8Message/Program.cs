using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

//TODO: recognize and use VERBS.SH and TALKERS.SH.

namespace Utf8Message
{
	struct Message
	{
		public byte Noun, Verb, Cond, Seq, Talker;
		public short Offset;
		public string Text;
	}

	class Program
	{
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
				Msg2Text(inFile, outFile);
			else
				Text2Msg(inFile, outFile);
		}

		static void Text2Msg(string inFile, string outFile)
		{
			var lines = File.ReadAllLines(inFile, Encoding.GetEncoding(1252));
			var utf8 = lines[0] == "!utf8";
			if (utf8)
				lines = File.ReadAllLines(inFile, Encoding.UTF8).Skip(1).ToArray();
			var messages = new List<Message>();
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (line.Length < 3)
					continue;
				if (line.StartsWith("//"))
					continue;
				var data = line.Split('\t');
				var noun = byte.Parse(data[0]);
				var verb = byte.Parse(data[1]);
				var cond = byte.Parse(data[2]);
				var seq = byte.Parse(data[3]);
				var talker = byte.Parse(data[4]);
				var text = data[5];
				if (i < lines.Length - 1)
				{
					var j = i + 1;
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
						text = text + "\r\n" + lines[j].Substring(5);
						i++;
						j++;
					}
				}
				messages.Add(new Message() { Noun = noun, Verb = verb, Cond = cond, Seq = seq, Talker = talker, Text = text });
			}
			var msg = new BinaryWriter(File.Open(outFile, FileMode.Create));
			msg.Write((short)0x8F); //resource
			msg.Write((int)4210); //version
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
				msg.Write("UTF8".ToCharArray());
			msg.Seek(0x0C, SeekOrigin.Begin);
			foreach (var message in messages)
			{
				msg.Write(message.Noun);
				msg.Write(message.Verb);
				msg.Write(message.Cond);
				msg.Write(message.Seq);
				msg.Write(message.Talker);
				msg.Write(offsets[0]);
				msg.Write((int)0x01000000); //ref
				offsets.RemoveAt(0);
			}
			msg.Close();
		}

		static void Msg2Text(string inFile, string outFile)
		{
			var msg = new BinaryReader(File.Open(inFile, FileMode.Open));
			msg.BaseStream.Seek(-4, SeekOrigin.End);
			var utf8 = msg.ReadInt32() == 0x38465455;
			var enc = utf8 ? Encoding.UTF8 : Encoding.Default;
			var output = new StreamWriter(outFile, false, enc);
			if (utf8)
				output.WriteLine("!utf8");
			msg.BaseStream.Seek(0xA, SeekOrigin.Begin);
			var num = msg.ReadInt16();
			var records = new Message[num];
			for (var i = 0; i < num; i++)
			{
				records[i] = new Message() { Noun = msg.ReadByte(), Verb = msg.ReadByte(), Cond = msg.ReadByte(), Seq = msg.ReadByte(), Talker = msg.ReadByte(), Offset = msg.ReadInt16() };
				msg.ReadInt32();
			}
			for (var i = 0; i < num; i++)
			{
				msg.BaseStream.Seek(2 + records[i].Offset, SeekOrigin.Begin);
				var text = ReadCString(msg, enc);

				text = text.Replace("\n", "\n\t\t\t\t\t");

				output.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
					records[i].Noun, records[i].Verb, records[i].Cond, records[i].Seq, records[i].Talker, text);
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
				if (i == 0) break;
				bytes.Add(i);
			}
			return enc.GetString(bytes.ToArray());
		}
	}
}
