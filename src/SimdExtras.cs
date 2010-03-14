using System;
using Mono.Simd;

namespace PixelMagic {
	public static class SimdExtras {
		static float Frc (float a) {
			return a - (float)(a > 0 ? Math.Floor (a) : Math.Ceiling (a));
		}

		public static Vector4f FractionalPart (this Vector4f v) {
			//FIXME this is super slow
			return new Vector4f (Frc (v.X), Frc (v.Y), Frc (v.Z), Frc (v.W));
		}

		public static Vector4f SquareRootReciprocal (this Vector4f v) {
			return new Vector4f ((float)(1 / Math.Sqrt (Math.Abs (v.X))));
		}

		public static Vector4f Absolute (this Vector4f v) {
			//FIXME Use the trick of unsetting the negative bit
			return new Vector4f (Math.Abs (v.X), Math.Abs (v.Y), Math.Abs (v.Z), Math.Abs (v.W));
		}

		public static Vector4f Dp2Add (Vector4f a, Vector4f b, Vector4f c) {
		 	Vector4f res = a * b;
			//XX we could use HorizontalAdd here
			res = res + res.Shuffle (ShuffleSel.XFromY) + c;
			return res.Shuffle (ShuffleSel.ExpandX);
		}


	}
}