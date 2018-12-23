using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Kawa.Tools;

namespace SeqMaker
{
	class Program
	{
		static int Main(string[] args)
		{
			Console.WriteLine("{0} {1} -- (c) 2018 Kafuka Productions", Application.ProductName, Application.ProductVersion);

			if (args.Length == 0)
			{
				var myExe = Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLowerInvariant();
				Console.WriteLine();
				Console.WriteLine("To create: {0} inFile [-o outFile]", myExe);
				Console.WriteLine();
				Console.WriteLine("inFile is a 320x200-pixel 256-color bitmap from a numbered sequence.");
				Console.WriteLine("If outFile is missing, inFile with the extension changed to '.seq'");
				Console.WriteLine("and the sequence removed is assumed.");
				Console.WriteLine();
				Console.WriteLine("Example: {0} scene1-1.png ==> scene1.seq", myExe);
				Console.WriteLine("         {0} scene1-1.png -o scene01.seq ==> scene01.seq", myExe);
				Console.WriteLine();
				Console.WriteLine("To play: {0} inFile [-a] [-d] [-e outFile]", myExe);
				Console.WriteLine();
				Console.WriteLine("inFile is a Sierra On-Line SEQ video file.");
				Console.WriteLine("Specify -a to play with aspect correction, -d to double the window size.");
				Console.WriteLine("Specify -e to extract frames to individual bitmaps. If no outFile is");
				Console.WriteLine("given, 'inFile-0001.png' is assumed.");
				Console.WriteLine();
				Console.WriteLine("Example: {0} scene1.seq", myExe);
				Console.WriteLine();
				return 4;
			}

			var wantToCount = true;
			var wasAnimatedGif = false;
			var filename = string.Empty;
			var outFile = string.Empty;
			for (var i = 0; i < args.Length; i++)
			{
				if (i == 0)
					filename = args[i];
				if (args[i].Equals("-o") && i < args.Length - 1)
					outFile = args[i + 1];
			}

			if (!File.Exists(filename))
			{
				Console.WriteLine("Input file {0} does not exist.", filename);
				return 1;
			}

			var frameList = new List<string>();

			if (Path.GetExtension(filename).Equals(".seq", StringComparison.InvariantCultureIgnoreCase))
			{
				try
				{
					Application.Run(new SeqPlay.Player(args));
				}
				catch (ObjectDisposedException)
				{ }
				return 0;
			}
			
			if (Path.GetExtension(filename).Equals(".txt", StringComparison.InvariantCultureIgnoreCase))
			{
				wantToCount = false;
				if (string.IsNullOrEmpty(outFile))
					outFile = Path.ChangeExtension(filename.RemoveSequence(), ".seq");
				frameList = File.ReadAllLines(filename).ToList();
				foreach (var file in frameList)
				{
					if (!File.Exists(file))
					{
						Console.WriteLine("Listed file {0} does not exist.", file);
						return 1;
					}
				}
				Console.WriteLine("Found {0} frames, from {1} up to {2}.", frameList.Count, Path.GetFileName(frameList[0]), Path.GetFileName(frameList.Last()));
			}
#if ALLOWGIFS
			else if (Path.GetExtension(filename).Equals(".gif", StringComparison.InvariantCultureIgnoreCase))
			{
				using (var maybeAnim = new System.Drawing.Bitmap(filename))
				{
					if (maybeAnim.Width != 320 || maybeAnim.Height != 200)
					{
						Console.WriteLine("Input images can only be 320 by 200 pixels in size.");
						return 3;
					}
					var dimension = new System.Drawing.Imaging.FrameDimension(maybeAnim.FrameDimensionsList[0]);
					var frameCount = maybeAnim.GetFrameCount(dimension);
					if (string.IsNullOrEmpty(outFile))
						outFile = Path.ChangeExtension(filename.RemoveSequence(), ".seq");
					if (frameCount == 1)
						wantToCount = true;
					else
					{
						Console.WriteLine("Exploding animated gif...");
						wantToCount = false;
						wasAnimatedGif = true;
							var tempFile = Path.Combine(Path.GetTempPath(), "seqmaker-0001.gif");
						for (var i = 0; i < frameCount; i++)
						{
							maybeAnim.SelectActiveFrame(dimension, i);
							var newFrame = ((System.Drawing.Bitmap)maybeAnim.Clone());
							newFrame.SaveEx(tempFile);
							frameList.Add(tempFile);
							tempFile = tempFile.IncreaseSequence();
						}
					}
				}
			}
#endif
			if (wantToCount && filename.RemoveSequence().Equals(filename))
			{
				Console.WriteLine("Input filename has no sequence.");
				return 2;
			}
			
			if (wantToCount)
			{
				if (string.IsNullOrEmpty(outFile))
					outFile = Path.ChangeExtension(filename.RemoveSequence(), ".seq");
				//Count frames
				var firstFile = filename;
				var lastFile = filename;
				while (File.Exists(filename))
				{
					frameList.Add(filename);
					lastFile = filename;
					filename = filename.IncreaseSequence();
				}
				Console.WriteLine("Found {0} frames, from {1} up to {2}.", frameList.Count, Path.GetFileName(firstFile), Path.GetFileName(lastFile));
			}

			var bitmap = new byte[64000];
			var previous = new byte[64000];
			var palette = new byte[768];

			var fileStream = new BinaryWriter(File.Open(outFile, FileMode.Create));
			fileStream.Write((UInt16)frameList.Count);

			var palChunk = new byte[1024];
			palChunk[10] = 1;
			palChunk[31] = 1;
			palChunk[32] = 1;
			palChunk[29] = 255;
			fileStream.Write(palChunk.Length);

			try
			{
				filename = frameList[0];
				BitmapData.GetPixels(filename, ref palette, ref bitmap);

				Array.Copy(palette, 0, palChunk, 37, 768);

				fileStream.Write(palChunk);

				foreach (var frame in frameList)
				{
					filename = frame;

					var frameLeft = 0;
					var frameTop = 0;
					var frameRight = 319;
					var frameBottom = 199;

					BitmapData.GetPixels(filename, ref palette, ref bitmap);

					var found = false;
					for (frameTop = 0; frameTop < 200; frameTop++)
					{
						for (var c = 0; c < 320; c++)
						{
							if (bitmap[(frameTop * 320) + c] != previous[(frameTop * 320) + c])
							{
								found = true;
								break;
							}
						}
						if (found)
							break;
					}
					found = false;
					for (frameBottom = 199; frameBottom > frameTop; frameBottom--)
					{
						for (var c = 0; c < 320; c++)
						{
							if (bitmap[(frameBottom * 320) + c] != previous[(frameBottom * 320) + c])
							{
								found = true;
								break;
							}
						}
						if (found)
							break;
					}
					frameBottom++;
					found = false;
					for (frameLeft = 0; frameLeft < 320; frameLeft++)
					{
						for (var c = 0; c < 200; c++)
						{
							if (bitmap[(c * 320) + frameLeft] != previous[(c * 320) + frameLeft])
							{
								found = true;
								break;
							}
						}
						if (found)
							break;
					}
					found = false;
					for (frameRight = 319; frameRight > frameLeft; frameRight--)
					{
						for (var c = 0; c < 200; c++)
						{
							if (bitmap[(c * 320) + frameRight] != previous[(c * 320) + frameRight])
							{
								found = true;
								break;
							}
						}
						if (found)
							break;
					}
					frameRight++;

					var frameWidth = frameRight - frameLeft;
					var frameHeight = frameBottom - frameTop;

					if (frameHeight + frameWidth == 0)
					{
						frameWidth = frameHeight = frameRight = frameBottom = 1;
						frameTop = frameLeft = 0;
					}

					var actualF = new byte[frameHeight * frameWidth];
					for (var l = 0; l < frameHeight; l++)
					{
						Array.Copy(bitmap, ((frameTop + l) * 320) + frameLeft, actualF, (l * frameWidth), frameWidth);
					}
					
					//TODO: COMPRESS THAT SHIT

					var colorKey = 255;
					var frameType = 0; //full frame
					var frameSize = frameWidth * frameHeight;
					var rleSize = 0;
					fileStream.Write((UInt16)frameWidth);
					fileStream.Write((UInt16)frameHeight);
					fileStream.Write((UInt16)frameLeft);
					fileStream.Write((UInt16)frameTop);
					fileStream.Write((byte)colorKey);
					fileStream.Write((byte)frameType);
					fileStream.Write((UInt16)0);
					fileStream.Write((UInt16)frameSize);
					fileStream.Write((UInt16)0);
					fileStream.Write((UInt16)rleSize);
					fileStream.Write((UInt32)0);
					fileStream.Write((UInt16)0);
					var offset = fileStream.BaseStream.Position + 4;
					fileStream.Write((UInt32)offset);
					fileStream.Write(actualF);

					Array.Copy(bitmap, 0, previous, 0, 64000);
				}
			}
			catch (FormatException fex)
			{
				Console.WriteLine("Error working on frame {0}:", filename);
				Console.WriteLine(fex.Message);
				return 3;
			}

#if ALLOWGIFS
			if (wasAnimatedGif)
			{
				Console.WriteLine("Cleaning up animated gif frames...");
				foreach (var file in frameList)
					File.Delete(file);
			}
#endif

			Console.WriteLine("Done!");

			fileStream.Flush();
			fileStream.Close();
			return 0;
		}
	}
}
