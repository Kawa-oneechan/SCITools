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
				Console.WriteLine("Need an input file -- an image (preferably PNG), or an SCI FON file -- and maybe an output file.");
				return;
			}
			if (args.Length >= 1)
			{
				inFile = args[0];
				if (inFile.EndsWith(".fon", StringComparison.InvariantCultureIgnoreCase))
					outFile = Path.ChangeExtension(inFile, ".png");
				else
					outFile = Path.ChangeExtension(inFile, ".fon");
			}
			if (args.Length >= 2)
			{
				outFile = args[1];
			}

			if (inFile.EndsWith(".fon", StringComparison.InvariantCultureIgnoreCase))
				Font2Pic(inFile, outFile);
			else
				Pic2Font(inFile, outFile);
		}

		static void Pic2Font(string inFile, string outFile)
		{
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
					left += thisCellWidth;

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

		static void Font2Pic(string inFile, string outFile)
		{
			var fontFile = new BinaryReader(File.OpenRead(inFile));
			var resHeader = fontFile.ReadInt16();
			if (resHeader != 0x87)
			{
				Console.WriteLine("File is not an SCI Font resource.");
				return;
			}
			var lowChar = fontFile.ReadInt16();
			var highChar = fontFile.ReadInt16();
			var lineHeight = fontFile.ReadInt16();
			var numChars = highChar - lowChar;

			//16384 ought to be big enough to cover my needs.
			//Might have to notice we're out of height and try
			//again at 32 characters per line.
			var bigBitmap = new Bitmap(16384, 16384);
			var g = Graphics.FromImage(bigBitmap);
			g.Clear(Color.Silver);
			var extentWidth = 32;
			var extentHeight = 32;
			g.FillRectangle(Brushes.White, 2, 2, 2, lineHeight);
			g.DrawRectangle(Pens.Gray, 1, 1, 3, lineHeight + 1);

			var left = 7;
			var top = 2;
			var maxHeight = 1;

			for (var i = 0; i < numChars; i++)
			{
				fontFile.BaseStream.Seek(8 + (i * 2), SeekOrigin.Begin);
				var offset = fontFile.ReadInt16();
				fontFile.BaseStream.Seek(offset + 2, SeekOrigin.Begin);
				var width = fontFile.ReadByte();
				var height = fontFile.ReadByte();
				if (height > maxHeight)
					maxHeight = height;
				var b = 0;
				for (var line = 0; line < height; line++)
				{
					for (int done = 0; done < width; done++)
					{
						if ((done & 7) == 0)
							b = fontFile.ReadByte();
						bigBitmap.SetPixel(left + done, top + line, ((b & 0x80) == 0x80) ? Color.Black : Color.White);
						b = (byte)(b << 1);
					}
				}
				g.DrawRectangle(Pens.Gray, left - 1, top - 1, width + 1, height + 1);

				left += width + 2;
				if (i % 16 == 15)
				{
					if (left > extentWidth)
						extentWidth = left + 0;
					left = 7;
					top += maxHeight + 2;
					maxHeight = 1;
				}
			}
			extentHeight = top + maxHeight + 2;

			var smallBitmap = new Bitmap(extentWidth, extentHeight);
			g = Graphics.FromImage(smallBitmap);
			g.DrawImage(bigBitmap, 0, 0, new Rectangle(0, 0, extentWidth, extentHeight), GraphicsUnit.Pixel);
			smallBitmap.Save(outFile);
		}
	}
}
