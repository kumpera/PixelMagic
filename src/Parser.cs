//
// Parser.cs
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
using System.IO;
using Mono.Simd;

namespace PixelMagic {
	public class Parser {
		Stream stream;
		List<Instruction> insList = new List<Instruction> ();
		int foff;

		bool Read (ref int val) {
			byte[] data = new byte [4];
			if (stream.Read (data, 0, 4) != 4)
				return false;
			foff += 4;
			val = data [0] | (data [1] << 8) | (data [2] << 16) | (data [3] << 24);
			//Console.WriteLine ("offset {0} got {1:X}", foff, (uint)val);
			return true;
		}

		public Parser (string filename) {
			this.stream = new FileStream (filename, FileMode.OpenOrCreate, FileAccess.Read);
		}

		//-----------
		Instruction ParseMov () {
			DestRegister dst = ParseDestReg ();
			SrcRegister src = ParseSrcReg ();
			return new Mov (dst, src);
		}

		Instruction ParseBinOp (BinOpKind op) {
			DestRegister dst = ParseDestReg ();
			SrcRegister src1 = ParseSrcReg ();
			SrcRegister src2 = ParseSrcReg ();
			return new BinaryOp (op, dst, src1, src2);
		}

		Instruction ParseUnOp (UnaryOpKind op) {
			DestRegister dst = ParseDestReg ();
			SrcRegister src = ParseSrcReg ();
			return new UnaryOp (op, dst, src);
		}

		Instruction ParseTernary (TernaryOpKind op) {
			DestRegister dst = ParseDestReg ();
			SrcRegister src1 = ParseSrcReg ();
			SrcRegister src2 = ParseSrcReg ();
			SrcRegister src3 = ParseSrcReg ();
			return new TernaryOp (op, dst, src1, src2, src3);
		}

		Instruction ParseDcl () {
			int token = 0;
			if (!Read (ref token))
				throw new Exception ("Cannot parse dcl args");
			TextureKind kind = (TextureKind) ((token >> 27) & 0x0F);
			DestRegister reg = ParseDestReg ();
			return new DefVar (kind, reg);
		}

		Instruction ParseTex () {
			DestRegister dst = ParseDestReg ();
			SrcRegister tex = ParseSrcReg ();
			SrcRegister coord = ParseSrcReg ();
			return new TexLoad (dst, coord, tex);
		}

		Instruction ParseDef () {
			DestRegister reg = ParseDestReg ();
			byte[] data = new byte[16];
			if (stream.Read (data, 0, 16) != 16)
				throw new Exception ("Cannot parse def args");
			foff += 16;
			Vector4f val = new Vector4f (
				BitConverter.ToSingle (data, 0),
				BitConverter.ToSingle (data, 4),
				BitConverter.ToSingle (data, 8),
				BitConverter.ToSingle (data, 12));

			if (reg.Kind != RegKind.Constant)
				throw new Exception ("Can't handle def to non constant regs " + reg.Kind);
			if (reg.WriteMask != DestRegister.MakeMask (true, true, true, true))
				throw new Exception ("Can't handle const without full mask " + reg);

			return new SetConst (reg.Number, val);
		}

		DestRegister ParseDestReg () {
			int val = 0;
			if (!Read (ref val))
				throw new Exception ("can't read dest register");
			DestRegister reg = new DestRegister (val);
			if (reg.Number >= 32)
				throw new Exception ("Invalid dest reg number " + reg.Number);
			return reg;
		}

		SrcRegister ParseSrcReg () {
			int val = 0;
			if (!Read (ref val))
				throw new Exception ("can't read src register");
			SrcRegister reg = new SrcRegister (val);
			if (reg.Number >= 32)
				throw new Exception ("Invalid src reg number " + reg.Number);
			return reg;
		}

		void ParseVersion (int v) {
			int minor = v & 0xFF;
			int major = (v >> 8) & 0xFF;
			int type = (v >> 16) & 0xFFFF;

			if (type != 0xFFFF)
				throw new Exception ("only pixel shaders supported");
			if (minor != 0 || major != 2)
			throw new Exception (String.Format ("only 2.0 supported, got {0}.{1}", major, minor));
		}

		bool ParseIns (int v) {
			int kind = v & 0xFFFF;
			if (kind == 0xFFFF) {
	//			Console.WriteLine ("EOF");
				return false;
			}

			if (kind == 0xFFFE) {
				int amount = (v >> 16);
				//Console.WriteLine ("skipping {0}", amount);
				for (int i = 0; i < amount; ++i)
					Read (ref v);
			} else {
				Instruction ins = null;
				switch (kind) {
				case 0x00:
					ins = new Nop ();
					break;
				case 0x01:
					ins = ParseMov ();
					break;
				case 0x02:
					ins = ParseBinOp (BinOpKind.Add);
					break;
				case 0x03:
					ins = ParseBinOp (BinOpKind.Sub);
					break;
				case 0x4:
					ins = ParseTernary (TernaryOpKind.Mad);
					break;
				case 0x05:
					ins = ParseBinOp (BinOpKind.Mul);
					break;
				case 0x06:
					ins = ParseUnOp (UnaryOpKind.Rcp);
					break;
				case 0x07:
					ins = ParseUnOp (UnaryOpKind.Rsq);
					break;
				case 0x08:
					ins = ParseBinOp (BinOpKind.Dp3);
					break;
				case 0x0A:
					ins = ParseBinOp (BinOpKind.Min);
					break;
				case 0x0B:
					ins = ParseBinOp (BinOpKind.Max);
					break;
				case 0x0E:
					ins = ParseUnOp (UnaryOpKind.Exp);
					break;
				case 0x0F:
					ins = ParseUnOp (UnaryOpKind.Log);
					break;
				case 0x12:
					ins = ParseTernary (TernaryOpKind.Lrp);
					break;
				case 0x13:
				ins = ParseUnOp (UnaryOpKind.Frc);
					break;
				case 0x1F:
					ins = ParseDcl ();
					break;
				case 0x23:
					ins = ParseUnOp (UnaryOpKind.Abs);
					break;
				case 0x25:
					ins =  ParseTernary (TernaryOpKind.SinCos);
					break;
				case 0x42:
					ins = ParseTex ();
					break;
				case 0x51:
					ins = ParseDef ();
					break;
				case 0x5A:
					ins = ParseTernary (TernaryOpKind.Dp2Add);
					break;
				case 0x58:
					ins = ParseTernary (TernaryOpKind.Cmp);
					break;
				default:
					throw new Exception ("invalid kind 0x" + kind.ToString ("X"));
				}
				if (((v >> 28) & 0x1) == 0x1)
					ins.Predicate = ParseSrcReg ();
				insList.Add (ins);
			}
			return true;
		}

		public List<Instruction> Parse () {
			int val = 0;

			if (!Read (ref val))
				throw new Exception ("empty file!");
			ParseVersion (val);

			while (Read (ref val)) {
				if (!ParseIns (val))
					break;
			}
			return insList;
		}
	}
}
