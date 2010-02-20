//
// Interpreter.cs
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
using System.Collections.Generic;
using Mono.Simd;

namespace PixelMagic {
	public class Interpreter {
		List<Instruction> shader;

		public Interpreter (List<Instruction> shader) {
			this.shader = shader;
		}

		public void Run (ShaderData sd) {
			Texture outtex = sd.GetOutputTexture (0);
			float wq = 1f / (float)outtex.Width;
			float hq = 1f / (float)outtex.Height;
			if (Tracing.Enabled) Console.WriteLine ("wq {0} hq {1}", wq, hq);

			ShaderContext ctx = new ShaderContext (sd);
			EvalVisitor visitor = new EvalVisitor (ctx);

			float acc_w = wq / 2;
			for (int i = 0; i < outtex.Width; ++i) {

				float acc_h = hq / 2;
				for (int j = 0; j < outtex.Height; ++j) {
					if (Tracing.Enabled) Console.WriteLine ("----processing x {0} y {1}", i, j);
					ctx.ResetState ();

					ctx.SetTextureValue (0, acc_w, acc_h);
					acc_h += hq;

					foreach (var ins in shader)
						ins.Visit (visitor);

					ctx.WriteColor (0, i, j, outtex);
				}
				acc_w += wq;
			}
		}
	}

	internal class EvalVisitor : InstructionVisitor {
		ShaderContext ctx;

		internal EvalVisitor (ShaderContext ctx) {
			this.ctx = ctx;
		}

		public void Visit (SetConst ins) {
			ctx.SetConstant (ins.Number, ins.Value);
		}

		public void Visit (DefVar ins) {
			//TODO what should I do here?
		}

		public void Visit (TexLoad ins) {
			if (ins.Sampler.Kind != RegKind.SamplerState)
				throw new Exception ("bad tex input reg " + ins.Texture.Kind);
			if (ins.Texture.Kind != RegKind.Texture)
				throw new Exception ("bad tex coord reg");

			Sampler s = ctx.GetSampler (ins.Sampler.Number);
			Vector4f c = ctx.GetTexture (ins.Texture.Number);
			Vector4f sample = s.Sample (c);

			if (Tracing.Enabled) Console.WriteLine ("tex-load {0}[{1}] => {2}", ins.Sampler, ins.Texture, sample);
			ctx.StoreValue (ins.Dest, sample);
		}

		public void Visit (BinaryOp ins) {
			Vector4f a = ctx.ReadValue (ins.Source1);
			Vector4f b = ctx.ReadValue (ins.Source2);
			Vector4f res = new Vector4f ();
			switch (ins.Operation) {
			case BinOpKind.Add:
				res = a + b;
				break;
			case BinOpKind.Mul:
				res = a * b;
				break;
			}

			if (Tracing.Enabled) Console.WriteLine ("{0} {1} {2} => {3}/{4} == {5}", ins.Source1, ins.Operation, ins.Source2, a, b, res);
			ctx.StoreValue (ins.Dest, res);
		}

		public void Visit (UnaryOp ins) {
			Vector4f a = ctx.ReadValue (ins.Source);
			Vector4f res = new Vector4f ();
			switch (ins.Operation) {
			case UnaryOpKind.Rcp:
				res = a.Reciprocal ();
				break;
			}

			if (Tracing.Enabled) Console.WriteLine ("{0} {1} => {2} == {3}", ins.Source, ins.Operation, a, res);
			ctx.StoreValue (ins.Dest, res);
		}

		public void Visit (Mov ins) {
			Vector4f a = ctx.ReadValue (ins.Source);
			ctx.StoreValue (ins.Dest, a);
		}

		public void Visit (TernaryOp ins) {
			throw new Exception ("can't handle " + ins);
		}
	}

	internal class ShaderContext {
		ShaderData shaderData;
		Vector4f[] textures = new Vector4f [32];
		Vector4f[] temp = new Vector4f [32];
		Vector4f[] constants = new Vector4f [32];
		Vector4f[] colorOut = new Vector4f [32];

		internal ShaderContext (ShaderData shaderData) {
			this.shaderData = shaderData;
		}

		internal void WriteColor (int colorReg, int i, int j, Texture outtex) {
			outtex.WriteColor (i, j, colorOut [colorReg]);
		}

		internal void ResetState () {
			textures = new Vector4f [32];
			temp = new Vector4f [32];
			colorOut = new Vector4f [32];

			foreach (var kv in shaderData.GetConstants ())
				SetConstant (kv.Key, kv.Value);
		}

		internal void SetTextureValue (int idx, float r, float g) {
			textures [idx].X = r;
			textures [idx].Y = g;
			if (Tracing.Enabled) Console.WriteLine ("set tex {0} with {1}", idx, textures [idx]);
		}

		internal void SetConstant (int idx, Vector4f val) {
			constants[idx] = val;
		}

		internal Sampler GetSampler (int idx) { return shaderData.GetSampler (idx); }

		internal Vector4f GetTexture (int idx) { return textures [idx]; }

		Vector4f GetReg (Register reg) {
			switch (reg.Kind) {
			case RegKind.Temp:
				return temp [reg.Number];
			case RegKind.Constant:
				return constants [reg.Number];
			case RegKind.ColorOut:
				return colorOut [reg.Number];
			default:
				throw new Exception ("can't handle reg load of type " + reg.Kind);
			}
		}

		internal Vector4f ReadValue (SrcRegister src) {
			Vector4f val = GetReg (src);
			if (Tracing.Enabled) Console.WriteLine ("\t {0} -> {1}", src, val);

			val = val.Shuffle ((ShuffleSel)src.Swizzle);
			switch (src.Modifier) {
			case SrcModifier.None:
				break;
			case SrcModifier.Negate:
				val = val * Vector4f.MinusOne;
				break;
			default:
				throw new Exception ("can't handle src reg modifier "+src.Modifier);
			}

			return val;
		}

		internal void StoreValue (DestRegister dst, Vector4f value) {
			Vector4f reg = GetReg (dst);

			if (dst.WriteMask != 0xF) {
				if ((dst.WriteMask & 0x1) == 0x1)
					reg.X = value.X;
				if ((dst.WriteMask & 0x2) == 0x2)
					reg.Y = value.Y;
				if ((dst.WriteMask & 0x4) == 0x4)
					reg.Z = value.Z;
				if ((dst.WriteMask & 0x8) == 0x8)
					reg.W = value.W;
			} else
				reg = value;

			switch (dst.Kind) {
			case RegKind.Temp:
				temp [dst.Number] = reg;
				break;
			case RegKind.ColorOut:
				colorOut [dst.Number] = reg;
				break;
			default:
				throw new Exception ("can't handle store to " + dst.Kind);
			}
		}
	}
}
