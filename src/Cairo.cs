//
// Cairo.cs
//
// Authors:
//  Rodrigo Kumpera (kumpera@gmail.com)
//
// Copyright (C) 2010 Rodrigo Kumpera.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Runtime.InteropServices;

/* Minimal cairo binding*/
namespace PixelMagic.Cairo
{

	public enum CairoFormat {
		ARGB32,
		RGB24,
		A8,
		A1
	}

	public class CairoImageSurface: IDisposable {

		[DllImport ("cairo")]
		static extern IntPtr cairo_surface_destroy (IntPtr surface);

		[DllImport ("cairo")]
		static extern int cairo_surface_status (IntPtr surface);

		[DllImport ("cairo")]
		static extern int cairo_surface_flush (IntPtr surface);

		[DllImport ("cairo")]
		static extern IntPtr cairo_image_surface_create (int format, int width, int heigth);

		[DllImport ("cairo")]
		static extern IntPtr cairo_image_surface_get_data (IntPtr surface);

		[DllImport ("cairo")]
		static extern int cairo_image_surface_get_format (IntPtr surface);

		[DllImport ("cairo")]
		static extern int cairo_image_surface_get_width (IntPtr surface);

		[DllImport ("cairo")]
		static extern int cairo_image_surface_get_height (IntPtr surface);

		[DllImport ("cairo")]
		static extern int cairo_image_surface_get_stride (IntPtr surface);

		[DllImport ("cairo")]
		static extern IntPtr cairo_image_surface_create_from_png (string name);

		[DllImport ("cairo")]
		static extern IntPtr cairo_surface_write_to_png (IntPtr surface, string name);

		IntPtr surface;
		bool disposed;

		public CairoImageSurface (int width, int heigth) {
			surface = cairo_image_surface_create ((int)CairoFormat.RGB24, width, heigth);
			if (surface == IntPtr.Zero)
				throw new ArgumentException ("could not create surface");

			int status = cairo_surface_status (surface);
			if (status != 0) {
				Dispose ();
				throw new ArgumentException ("could not create surface");
			}
		}

		public CairoImageSurface (string file) {
			surface = cairo_image_surface_create_from_png (file);
			if (surface == IntPtr.Zero)
				throw new ArgumentException ("could not load png file "+file);

			int status = cairo_surface_status (surface);
			if (status != 0) {
				Dispose ();
				throw new ArgumentException ("could not load png file "+file);
			}
		}

		~CairoImageSurface () {
			Dispose (false);
		}

		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		void Dispose (bool disposing) {
			if (disposed)
				return;
			cairo_surface_destroy (surface);
			disposed = true;
		}


		public CairoImageSurface CreateSimilar () {
			return new CairoImageSurface (Width, Height);
		}

		public void SaveToPng (string file) {
			cairo_surface_flush (surface);
			cairo_surface_write_to_png (surface, file);
		}

		public int Width {
			get { return cairo_image_surface_get_width (surface); }
		}

		public int Height {
			get { return cairo_image_surface_get_height (surface); }
		}

		public int Stride {
			get { return cairo_image_surface_get_stride (surface); }
		}

		public IntPtr Data {
			get { return cairo_image_surface_get_data (surface); }
		}

		public CairoFormat Format {
			get { return (CairoFormat) cairo_image_surface_get_format (surface); }
		}
	}

	public class CairoTexture : Texture {
		CairoImageSurface surface;

		public CairoTexture (CairoImageSurface surface) {
			this.surface = surface;
			if (Tracing.Enabled) Console.WriteLine ("x {0} y {1} stride {2}", surface.Width, surface.Height, surface.Stride);
		}

		public override int Width {
			get { return surface.Width; }
		}

		public override int Height {
			get { return surface.Height; }
		}

		/*RGBA*/
		public override uint Read (int x, int y) {
			unsafe {
				byte *data = (byte*)surface.Data;
				int offset = x * 4 + y * surface.Stride;
				return *(uint*)(data + offset);
			}
		}

		public override void Write (int x, int y, uint rgba) {
			unsafe {
				byte *data = (byte*)surface.Data;
				int offset = x * 4 + y * surface.Stride;
				*(uint*)(data + offset) = rgba;
			}
		}
	}
}
