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
	}
}