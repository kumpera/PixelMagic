//
// Texture.cs
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
using Mono.Simd;

namespace PixelMagic {
	public abstract class Texture {
		public abstract int Width { get; }
		public abstract int Height { get; }

		/*RGBA*/
		public abstract uint Read (int x, int y);
		public abstract void Write (int x, int y, uint rgba);

		public void WriteColor (int x, int y, Vector4f color) {
			uint r = (uint)Math.Round (color.X * 255);
			uint g = (uint)Math.Round (color.Y * 255);
			uint b = (uint)Math.Round (color.Z * 255);
			uint a = (uint)Math.Round (color.W * 255);
			uint p = r | (g << 8) | (b << 16) | (a << 24);

			if (Tracing.Enabled) Console.WriteLine ("store {0:X8} at [{1}, {2}] from {3}", p, x, y, color);
			Write (x, y, p);
		}

		public Vector4f ReadColor (int x, int y) {
			uint p = Read (x, y);
			float r = (p & 0xFF) / 255f;
			float g = ((p >> 8) & 0xFF) / 255f;
			float b = ((p >> 16) & 0xFF) / 255f;
			float a = ((p >> 24) & 0xFF) / 255f;

			Vector4f color = new Vector4f (r, g, b, a);
			if (Tracing.Enabled) Console.WriteLine ("read color [{0}, {1}] = {2:X}  / {3}", x, y, p, color);
			return color;
		}
	}

	public class Sampler {
		Texture tex;

		public Sampler (Texture tex) {
			this.tex = tex;
		}

		static float Clamp (float x) {
			if (x < 0f)
				return 0f;
			if (x > 1f)
				return 1f;
			return x;
		}

		static int Clamp (int x, int max) {
			if (x < 0)
				return 0;
			if (x >= max)
				return max - 1;
			return x;
		}

		public Vector4f Sample (Vector4f coord) {
			int x = Clamp ((int)(coord.X * tex.Width), tex.Width);
			int y = Clamp ((int)(coord.Y * tex.Height), tex.Width);
			Vector4f color = tex.ReadColor (x, y);

			if (Tracing.Enabled) Console.WriteLine ("sampling {0} -> [{1}, {2}] -> {3}", coord, x, y, color);
			return color;
		}
	}
}
