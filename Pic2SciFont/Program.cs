using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Pic2SciFont
{
	class Cell
	{
		public int Index;
		public int Left, Top, Width, Height;
		public byte[] Data;
		public short Offset;
		public bool Optimized;
		public string Hex;
	}

	class Program
	{
		static void Main(string[] args)
		{
			var inFile = string.Empty;
			var outFile = string.Empty;

			if (args.Length == 0)
			{
				Console.WriteLine("Need an input file, preferably PNG, and maybe an output file.");
				return;
			}
			if (args.Length >= 1)
			{
				inFile = args[0];
				outFile = Path.ChangeExtension(inFile, ".fon");
			}
			if (args.Length >= 2)
			{
				outFile = args[1];
			}

			var sheet = new Bitmap(inFile);

			var lineHeight = 0;
			var charCount = -1;
			var maxHeight = 0;

			var cells = new List<Cell>();

			for (var top = 0; top < sheet.Height; top += maxHeight)
			{
				maxHeight = 1;
				for (var left = 0; left < sheet.Width; left++)
				{
					//scan for a cell's top-left
					var pixel = sheet.GetPixel(left, top).GetBrightness();
					if (!(pixel == 0 || pixel == 1))
						continue;

					//at this point we're at a cell's top-left corner
					var thisCellTop = top;
					var thisCellLeft = left;
					var thisCellWidth = 0;
					var thisCellHeight = 0;
					while (pixel == 0 || pixel == 1)
					{
						left++;
						thisCellWidth++;
						pixel = sheet.GetPixel(left, top).GetBrightness();
					}
					left -= thisCellWidth;
					pixel = sheet.GetPixel(left, top).GetBrightness();
					while (pixel == 0 || pixel == 1)
					{
						top++;
						thisCellHeight++;
						pixel = sheet.GetPixel(left, top).GetBrightness();
					}
					top -= thisCellHeight;
					left += thisCellWidth + 1;

					//we now have the position and size of the cell.
					if (thisCellHeight > maxHeight)
						maxHeight = thisCellHeight;

					if (charCount == -1)
					{
						lineHeight = thisCellHeight;
						charCount++;
						continue;
					}

					var cellData = new Cell() { Index = charCount, Top = thisCellTop, Left = thisCellLeft, Width = thisCellWidth, Height = thisCellHeight, Offset = -1, Optimized = false };
					var bytes = new List<byte>();

					bytes.Add((byte)thisCellWidth);
					bytes.Add((byte)thisCellHeight);

					//var bytesPerLine = (thisCellWidth + 3) / 4 * 4;
					var bytesToWrite = (thisCellWidth + 7) / 8;

					for (var line = 0; line < thisCellHeight; line++)
					{
						for (var i = 0; i < bytesToWrite; i++)
						{
							var bOut = (byte)0;
							var jEnd = 8 * (i + 1);
							var iShift = 7;
							for (var j = 8 * i; (j < jEnd) && (j < thisCellWidth); j++, iShift--)
							{
								if (sheet.GetPixel(j + thisCellLeft, line + thisCellTop).GetBrightness() == 0)
									bOut |= (byte)(1 << iShift);
							}
							bytes.Add(bOut);
						}
					}

					cellData.Data = bytes.ToArray();
					cellData.Hex = string.Join(string.Empty, bytes.Select(b => b.ToString("X02")));
					cells.Add(cellData);
					charCount++;
				}
			}

			var offset = 6 + (charCount * 2);
			for (var i = 0; i < charCount; i++)
			{
				if (i > 0)
				{
					for (var j = 0; j < i; j++)
					{
						if (cells[i].Hex == cells[j].Hex)
						{
							cells[i].Optimized = true;
							cells[i].Offset = cells[j].Offset;
							break;
						}
					}
				}
				if (!cells[i].Optimized)
				{
					cells[i].Offset = (short)offset;
					offset += cells[i].Data.Length;
				}
			}

			var fontFile = new BinaryWriter(File.Open(outFile, System.IO.FileMode.Create));
			fontFile.Write((short)0x87);
			fontFile.Write((short)0x00);
			fontFile.Write((short)charCount);
			fontFile.Write((short)lineHeight);
			for (var i = 0; i < charCount; i++)
				fontFile.Write((short)cells[i].Offset);
			for (var i = 0; i < charCount; i++)
			{
				if (cells[i].Optimized)
					continue;
				fontFile.Write(cells[i].Data);
			}
			fontFile.Flush();
			fontFile.Close();
		}
	}
}
