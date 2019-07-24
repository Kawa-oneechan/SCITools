using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Kawa.Tools;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace SeqPlay
{
	public class Player : Form, IDisposable
	{
		private readonly BinaryReader fileStream;
		private readonly byte[] screen;
		private readonly bool exam;

		private readonly int frameCount;
		private int curFrame;
		private string outputFile;

		private readonly Bitmap bitmap;
		private readonly Timer timer;

		public Player(string[] args)
		{
			//InitializeComponent();

			fileStream = new BinaryReader(File.Open(args[0], FileMode.Open));

			screen = new byte[320 * 200];
			bitmap = new Bitmap(320, 200, PixelFormat.Format8bppIndexed);
			this.BackgroundImage = bitmap;
			this.BackgroundImageLayout = ImageLayout.Stretch;
			this.ClientSize = new Size(320, 200);
			this.DoubleBuffered = true;

			exam = (args.Contains("-x"));
			if (args.Contains("-a"))
			{
				this.ClientSize = new Size(320, 240);
			}
			if (args.Contains("-d"))
			{
				this.ClientSize = new Size(this.ClientSize.Width * 2, this.ClientSize.Height * 2);
			}
			if (args.Contains("-e"))
			{
				outputFile = Path.Combine(Path.GetDirectoryName(args[0]), Path.GetFileNameWithoutExtension(args[0]) + "-0001.png");
				for (var i = 0; i < args.Length; i++)
				{
					if (args[i] == "-e")
					{
						if (i + 1 < args.Length)
						{
							outputFile = args[i + 1];
						}
						break;
					}
				}
			}

			if (exam && !string.IsNullOrWhiteSpace(outputFile))
			{
				var result = MessageBox.Show(this, "You have specified both export and examination mode. The resulting files will be unusable for recompilation.\n\nWould you like to disable export mode?", Application.ProductName, MessageBoxButtons.YesNoCancel);
				if (result == DialogResult.Cancel)
				{
					Application.Exit();
					Close();
					return;
				}
				if (result == DialogResult.Yes)
				{
					outputFile = null;
				}
			}

			frameCount = fileStream.ReadUInt16();
			ReadPaletteChunk(fileStream.ReadInt32());
			DecodeNextFrame();

			timer = new Timer();
			timer.Interval = 100;
			timer.Tick += (s, e) =>
			{
				DecodeNextFrame();
				if (curFrame >= frameCount)
				{
					timer.Stop();
				}
			};

			FormClosing += (s, e) =>
			{
				timer.Stop();
			};
			KeyPress += (s, e) =>
			{
				if (e.KeyChar == 27)
				{
					Close();
				}
			};

			timer.Start();
		}

		private void DrawScreen()
		{
			this.Text = string.Format("{0}/{1}", curFrame, frameCount);
			var bitmapData = bitmap.LockBits(new Rectangle(0, 0, 320, 200), ImageLockMode.ReadOnly, bitmap.PixelFormat);
			Marshal.Copy(screen, 0, bitmapData.Scan0, 320 * 200);
			bitmap.UnlockBits(bitmapData);
			this.Invalidate();

			//skip saving first frame, it'd be black.
			if (curFrame == 0)
			{
				return;
			}

			if (!string.IsNullOrEmpty(outputFile))
			{
				bitmap.SaveEx(outputFile);
				outputFile = outputFile.IncreaseSequence();
			}
		}

		private void ReadPaletteChunk(int chunkSize)
		{
			var paletteData = new byte[chunkSize];
			fileStream.Read(paletteData, 0, chunkSize);
			
			//SCI1.1 palette
			var palFormat = paletteData[32];
			var palColorStart = ((paletteData[25]) | (paletteData[26] << 8));
			var palColorCount = ((paletteData[29]) | (paletteData[30] << 8));

			var palOffset = 37;
			var palette = bitmap.Palette;

			for (var colorNo = palColorStart; colorNo < palColorStart + palColorCount; colorNo++)
			{
				if (palFormat == 0) //kSeqPalVariable
				{
					palOffset++;
				}
				palette.Entries[colorNo] = Color.FromArgb(paletteData[palOffset++], paletteData[palOffset++], paletteData[palOffset++]);
			}
			bitmap.Palette = palette;

			DrawScreen();
		}

		private void DecodeNextFrame()
		{
			var frameWidth = fileStream.ReadUInt16();
			var frameHeight = fileStream.ReadUInt16();
			var frameLeft = fileStream.ReadUInt16();
			var frameTop = fileStream.ReadUInt16();
			fileStream.BaseStream.Seek(1, SeekOrigin.Current);
			var frameType = fileStream.ReadByte();
			fileStream.BaseStream.Seek(2, SeekOrigin.Current);
			var frameSize = fileStream.ReadUInt16();
			fileStream.BaseStream.Seek(2, SeekOrigin.Current);
			var rleSize = fileStream.ReadUInt16();
			fileStream.BaseStream.Seek(6, SeekOrigin.Current);
			var offset = fileStream.ReadUInt32();
			fileStream.BaseStream.Seek((long)offset, SeekOrigin.Begin); //is this right?

			if (exam)
			{
				Array.Clear(screen, 0, screen.Length);
				if (frameLeft > 0)
				{
					for (var i = frameTop; i < frameTop + frameHeight; i += 2)
					{
						screen[(i * 320) + frameLeft - 1] = 255;
					}
				}
				if (frameLeft + frameWidth < 320)
				{
					for (var i = frameTop; i < frameTop + frameHeight; i += 2)
					{
						screen[(i * 320) + (frameLeft + frameWidth)] = 255;
					}
				}
				if (frameTop > 0)
				{
					for (var i = frameLeft; i < frameLeft + frameWidth; i += 2)
					{
						screen[((frameTop - 1) * 320) + i] = 255;
					}
				}
				if (frameTop + frameHeight < 200)
				{
					for (var i = frameLeft; i < frameLeft + frameWidth; i += 2)
					{
						screen[((frameTop + frameHeight) * 320) + i] = 255;
					}
				}
			}

			if (frameType == 0) //kSeqFrameFull
			{
				var lineBuf = new byte[frameWidth];
				do
				{
					fileStream.Read(lineBuf, 0, frameWidth);
					Array.Copy(lineBuf, 0, screen, (frameTop * 320) + frameLeft, frameWidth);
					frameTop++;
				} while (--frameHeight > 0);
			}
			else
			{
				var buf = new byte[frameSize];
				fileStream.Read(buf, 0, frameSize);
				DecodeFrame(buf, rleSize, frameSize - rleSize, frameTop, frameLeft, frameWidth, frameHeight);
			}

			DrawScreen();
			curFrame++;
		}

		private void DecodeFrame(byte[] rleData, int rleSize, int litSize, int top, int left, int width, int height)
		{
			var writeRow = top;
			int writeCol = left;
			int litPos = rleSize;
			int rlePos = 0;

			while (rlePos < rleSize)
			{
				var op = rleData[rlePos++];
				if ((op & 0xC0) == 0xC0)
				{
					op &= 0x3F;
					if (op == 0)
					{
						//Go to next line in target buffer
						writeRow++;
						writeCol = left;
					}
					else
					{
						//Skip bytes on the current line
						writeCol += op;
					}
				}
				else if ((op & 0x80) == 0x80)
				{
					op &= 0x3F;
					if (op == 0)
					{
						//Copy remainder of current line
						var rem = width - (writeCol - left);
						Array.Copy(rleData, litPos, screen, (writeRow * 320) + writeCol, rem);
						writeRow++;
						writeCol = left;
						litPos += rem;
					}
					else
					{
						//Copy bytes
						Array.Copy(rleData, litPos, screen, (writeRow * 320) + writeCol, op);
						writeCol += op;
						litPos += op;
					}
				}
				else
				{
					var count = ((op & 7) << 8) | rleData[rlePos++];
					switch (op >> 3)
					{
						case 2: //Skip bytes
							writeCol += count;
							break;
						case 3: //Copy bytes
							Array.Copy(rleData, litPos, screen, (writeRow * 320) + writeCol, count);
							writeCol += count;
							litPos += count;
							break;
						case 6: //Copy rows
							if (count == 0)
								count = height - writeRow;

							for (var i = 0; i < count; i++)
							{
								Array.Copy(rleData, litPos, screen, (writeRow * 320) + writeCol, width);
								litPos += width;
								writeRow++;
							}
							break;
						case 7: //Skip rows
							if (count == 0)
							{
								count = height - writeRow;
							}
							writeRow += count;
							break;
						default:
							Console.WriteLine("Unsupported operation {0} encountered.", op >> 3);
							return;
					}
				}
			}
			return;
		}

		void IDisposable.Dispose()
		{
			fileStream.Dispose();
		}
	}
}
