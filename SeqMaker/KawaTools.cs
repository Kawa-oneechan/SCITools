using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Globalization;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Kawa.Tools
{
	/// <summary>
	/// Adds a few handy extension methods.
	/// </summary>
	static public class Extensions	
	{
		/// <summary>
		/// Given something like a filename with a sequence number in it, returns the next filename.
		/// </summary>
		/// <remarks>
		/// The sequence number is the -last- stretch of consecutive digits in the string.
		/// </remarks>
		/// <example>"4.png" =&gt; "5.png"</example>
		/// <param name="filename">A string with a number in it, such as "frame003.png".</param>
		/// <param name="throwIfMissing">If true, throw an ArgumentException if there is no sequence in the string. If false, silently return the original string.</param>
		/// <returns>The next string in the sequence, such as "frame004.png".</returns>
		/// <exception cref="System.ArgumentException">Thrown when there is no sequence in the string and throwIfMissing is true.</exception>
		static public string IncreaseSequence(this string filename, bool throwIfMissing = false)
		{
			var lastNumberPos = -1;
			for (var i = 0; i < filename.Length; i++)
				if (char.IsDigit(filename[i]))
					lastNumberPos = i;
			if (lastNumberPos == -1)
			{
				if (throwIfMissing)
					throw new ArgumentException(string.Format("String \"{0}\" has no number sequence in it.", filename));
				else
					return filename;
			}
			var firstNumberPos = lastNumberPos;
			for (var i = lastNumberPos - 1; i >= 0; i--)
				if (char.IsDigit(filename[i]))
					firstNumberPos = i;
				else
					break;
			var length = lastNumberPos - firstNumberPos + 1;
			var numPart = filename.Substring(firstNumberPos, length);
			var number = int.Parse(numPart);
			number++;
			filename = filename.Substring(0, firstNumberPos) + number.ToString("D" + length) + filename.Substring(lastNumberPos + 1);
			return filename;
		}

		/// <summary>
		/// Given something like a filename with a sequence number in it, returns that filename without a sequence.
		/// </summary>
		/// <remarks>
		/// The sequence number is the -last- stretch of consecutive digits in the string.
		/// </remarks>
		/// <example>"foo-4.png" =&gt; "foo.png"</example>
		/// <param name="filename">A string with a number in it, such as "frame003.png".</param>
		/// <param name="blank">The string to use if the filename starts with a sequence, so that "0001.png" becomes "out.png" instead of ".png".</param>
		/// <returns>The filename with the sequence part removed.</returns>
		static public string RemoveSequence(this string filename, string blank = "out")
		{
			var lastNumberPos = -1;
			for (var i = 0; i < filename.Length; i++)
				if (char.IsDigit(filename[i]))
					lastNumberPos = i;
			if (lastNumberPos == -1)
				return filename;
			var firstNumberPos = lastNumberPos;
			for (var i = lastNumberPos - 1; i >= 0; i--)
				if (char.IsDigit(filename[i]))
					firstNumberPos = i;
				else
					break;
			while (firstNumberPos > 0 && char.IsPunctuation(filename[firstNumberPos - 1]))
				firstNumberPos--;
			if (firstNumberPos == 0)
				return blank + filename.Substring(lastNumberPos + 1);
			return filename.Substring(0, firstNumberPos) + filename.Substring(lastNumberPos + 1);
		}
		
		/// <summary>
		/// Saves the given Bitmap object to a file, selecting the proper ImageFormat from the filename's extension instead of defaulting to PNG.
		/// </summary>
		/// <param name="bitmap">The bitmap to save.</param>
		/// <param name="filename">The filename to save to.</param>
		/// <exception cref="System.ArgumentException">Thrown when saving to a filename with an unrecognized extension.</exception>
		public static void SaveEx(this Bitmap bitmap, string filename)
		{
			switch (Path.GetExtension(filename.ToLowerInvariant()))
			{
				case ".png": bitmap.Save(filename, ImageFormat.Png); break;
				case ".bmp": bitmap.Save(filename, ImageFormat.Bmp); break;
				case ".gif": bitmap.Save(filename, ImageFormat.Gif); break;
				case ".jpg": bitmap.Save(filename, ImageFormat.Jpeg); break;
				case ".jpeg": bitmap.Save(filename, ImageFormat.Jpeg); break;
				case ".tif": bitmap.Save(filename, ImageFormat.Tiff); break;
				case ".tiff": bitmap.Save(filename, ImageFormat.Tiff); break;
				default: throw new ArgumentException("Unsupported bitmap file format.");
			}
		}
	}

	/// <summary>
	/// Abstracts away the retrieval of an image's raw pixel and palette data, and allows reading ZSoft Paintbrush PCX files into Bitmap objects.
	/// </summary>
	public static class BitmapData
	{
		/// <summary>
		/// Extracts the raw pixel data and optionally palette from a 256-color 320 by 200 pixel Bitmap.
		/// </summary>
		/// <param name="from">The name of a bitmap file in any of the formats supported by Bitmap, or ZSoft Paintbrush PCX.</param>
		/// <param name="palette">An array of 768 bytes to recieve the color palette of the Bitmap, or null.</param>
		/// <param name="data">An array of 64000 bytes to recieve the pixel data of the Bitmap.</param>
		public static void GetPixels(string from, ref byte[] palette, ref byte[] data)
		{
			using (var bmp = new Bitmap(from))
			{
				GetPixels(bmp, ref palette, ref data);
			}
		}

		/// <summary>
		/// Extracts the raw pixel data and optionally palette from a 256-color 320 by 200 pixel Bitmap.
		/// </summary>
		/// <param name="from">A Bitmap object.</param>
		/// <param name="palette">An array of 768 bytes to recieve the color palette of the Bitmap, or null.</param>
		/// <param name="data">An array of 64000 bytes to recieve the pixel data of the Bitmap.</param>
		/// <exception cref="System.Exception">Thrown whenever the programmer fucked up.</exception>
		/// <exception cref="System.FormatException">Thrown whenever the Bitmap is not the right size or color depth.</exception>
		/// <exception cref="System.ArgumentNullException">Thrown when a null data array is given.</exception>
		public static void GetPixels(Bitmap from, ref byte[] palette, ref byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException("Need a target array.");
			if (data.Length != 320 * 200)
				throw new Exception("Target array isn't big enough.");
			if (palette != null && palette.Length != 256 * 3)
				throw new Exception("Palette array isn't big enough.");
			var victim = from;
			if (victim.PixelFormat != PixelFormat.Format8bppIndexed)
				throw new FormatException("Input images can only be in 256 color format.");
			if (victim.Width != 320 || victim.Height != 200)
				throw new FormatException("Input images can only be 320 by 200 pixels in size.");

			if (palette != null)
			{
				for (var i = 0; i < victim.Palette.Entries.Length; i++)
				{
					var color = victim.Palette.Entries[i];
					palette[i * 3 + 0] = color.R;
					palette[i * 3 + 1] = color.G;
					palette[i * 3 + 2] = color.B;
				}
			}

			var bitmapData = victim.LockBits(new Rectangle(0, 0, victim.Width, victim.Height), ImageLockMode.ReadOnly, victim.PixelFormat);
			Marshal.Copy(bitmapData.Scan0, data, 0, 320 * 200);
			victim.UnlockBits(bitmapData);
		}
	}
}
