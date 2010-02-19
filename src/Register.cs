//
// Compiler.cs
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

namespace PixelMagic {

	public enum RegKind {
		Temp,
		Input,
		Constant,
		Texture,
		ConstantInt,
		ColorOut,
		DepthOut,
		SamplerState,
		ConstantBool,
		LoopCounter,
		HalfTemp,
		Misc,
		Label,
		Predicate
	}

	public enum SrcModifier {
		None,
		Negate,
		Bias,
		BiasAndNegate,
		Sign,
		SignAndNegate,
		Complement,
		Double,
		DoubleAndNegate,
		DivideByZ,
		DivideByW,
		Abs,
		AbsAndNegate,
		Not
	}

	public abstract class Register {
		public int Number { get; protected set; }
		public RegKind Kind { get; protected set; }

		protected Register (int val) {
			Number = val & 0x3FF;
			ParseKind (val);
		}

		void ParseKind (int val) {
			int kind = ((val >> 28) & 0x7) | (((val >> 11) & 0x3) << 3);
			switch (kind) {
			case 0:
				Kind = RegKind.Temp;
				break;
			case 1:
				Kind = RegKind.Input;
				break;
			case 2:
				Kind = RegKind.Constant;
				break;
			case 3:
				Kind = RegKind.Texture;
				break;
			case 4:
			case 5:
			case 6:
				throw new Exception ("reserved reg type "+kind);
			case 7:
				Kind = RegKind.ConstantInt;
				break;
			case 8:
				Kind = RegKind.ColorOut;
				break;
			case 9:
				Kind = RegKind.DepthOut;
				break;
			case 10:
				Kind = RegKind.SamplerState;
				break;
			case 11:
				Number += 2048;
				Kind = RegKind.Constant;
				break;
			case 12:
				Number += 4096;
				Kind = RegKind.Constant;
				break;
			case 13:
				Kind = RegKind.Constant;
				Number += 6144;
				break;
			case 14:
				Kind = RegKind.ConstantBool;
				break;
			case 15:
				Kind = RegKind.LoopCounter;
				break;
			case 16:
				Kind = RegKind.HalfTemp;
				break;
			case 17:
				Kind = RegKind.Misc;
				break;
			case 18:
				Kind = RegKind.Label;
				break;
			case 19:
				Kind = RegKind.Predicate;
				break;
			default:
				throw new Exception ("invalid reg type "+kind);
			}	
		}
	}

	public class SrcRegister : Register {
		public SrcRegister (int val) : base (val) {
			Swizzle = (val >> 16) & 0xFF;
			int mod = (val >> 24) & 0x0F;
			if (mod > 0xd)
				throw new Exception ("bad src reg modifier");
			Modifier = (SrcModifier)mod;
		}

		public int Swizzle { get; private set; }
		public SrcModifier Modifier { get; private set; }

		public static int MakeSwizzle (int r, int g, int b, int a) {
			return (r) | (g << 2) | (b << 4) | (a << 6);
		}

		string getMask (int comp) {
			string[] val = new string[] { "r", "g", "b", "a" };
			int x = (Swizzle >> comp * 2) & 0x3;
			return val [x];
		}

		public override string ToString () {
			string str = String.Format ("{0}_{1}", Kind, Number);
			if (Swizzle != MakeSwizzle (0, 1, 2, 3))
				str += "_" + getMask (0) + getMask (1) + getMask (2) + getMask (3);
			if (Modifier != SrcModifier.None)
				str += "_" + Modifier;
			return str;
		}

	}

	public class DestRegister : Register {
		public DestRegister (int val) : base (val) {
			WriteMask = (val >> 16) & 0xF;
			int stuff = (val >> 20) & 0x7;
			if ((stuff & 0x2) != 0)
				PartialPrecision = true;
			if ((stuff & 0x4) != 0)
				Centroid = true;
		}

		public static int MakeMask (bool r, bool g, bool b, bool a) {
			int res = 0;
			if (r)
				res |= 0x1;
			if (g)
				res |= 0x2;
			if (b)
				res |= 0x4;
			if (a)
				res |= 0x8;
			return res;
		}

		public int WriteMask { get; private set; }
		public bool PartialPrecision { get; private set; }
		public bool Centroid { get; private set; }

		public override string ToString () {
			string str = String.Format ("{0}_{1}", Kind, Number);
			if (WriteMask != 0x0F) {
				str += "_";
				if ((WriteMask & 0x1) == 0x1)
					str += "r";
				if ((WriteMask & 0x2) == 0x2)
					str += "g";
				if ((WriteMask & 0x4) == 0x4)
					str += "b";
				if ((WriteMask & 0x8) == 0x8)
					str += "a";
			}
			if (PartialPrecision)
				str += "_pp";
			if (Centroid)
				str += "_ct";
			return str;
		}
	}
}
