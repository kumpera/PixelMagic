//
// Instruction.cs
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
	public enum TernaryOpKind {
		Mad,
		Cmp,
		SinCos,
		Lrp,
		Dp2Add
	}


	public enum BinOpKind {
		Add,
		Sub,
		Mul,
		Max,
		Dp3,
		Dp4,
		Min
	}

	public enum UnaryOpKind {
		Rcp,
		Frc,
		Rsq,
		Abs,
		Exp,
		Log
	}

	public enum TextureKind {
		Unknown = 0,
		Text2d  = 2,
		Cube    = 4,
		Volume  = 8,
	}

	public interface InstructionVisitor {
		void Visit (SetConst ins);
		void Visit (DefVar ins);
		void Visit (TexLoad ins);
		void Visit (TernaryOp ins);
		void Visit (BinaryOp ins);
		void Visit (UnaryOp ins);
		void Visit (Mov ins);
		void Visit (Nop nop);
	}

	public abstract class Instruction {
		public SrcRegister Predicate { get; set; }

		public abstract void Visit (InstructionVisitor visitor);
	}

	public class Nop : Instruction {
		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}
	}

	public class SetConst : Instruction {
		int reg;
		Vector4f val;

		public SetConst (int reg, Vector4f val) {
			this.reg = reg;
			this.val = val;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public int Number {
			get { return reg; }
		}

		public Vector4f Value {
			get { return val; }
		}

		public override string ToString () {
			return String.Format ("set-const {0} = {1}", reg, val);
		}
	}

	public class DefVar : Instruction {
		TextureKind kind;
		DestRegister reg;

		public DefVar (TextureKind kind, DestRegister reg) {
			this.kind = kind;
			this.reg = reg;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public TextureKind Kind {
			get { return kind; }
		}

		public DestRegister Dest {
			get { return reg; }
		}

		public override string ToString () {
			return String.Format ("def-var {0}/{1}", reg, kind);
		}

	}

	public class TexLoad : Instruction {
		DestRegister dest;
		SrcRegister sampler;
		SrcRegister tex;

		public TexLoad (DestRegister dest, SrcRegister sampler, SrcRegister tex) {
			this.dest = dest;
			this.sampler = sampler;
			this.tex = tex;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public DestRegister Dest {
			get { return dest; }
		}

		public SrcRegister Sampler {
			get { return sampler; }
		}

		public SrcRegister Texture {
			get { return tex; }
		}

		public override string ToString () {
			return String.Format ("texld {0} = {1}[{2}]", dest, sampler, tex);
		}
	}

	public class TernaryOp : Instruction {
		TernaryOpKind op;
		DestRegister dest;
		SrcRegister src1, src2, src3;

		public TernaryOp (TernaryOpKind op, DestRegister dest, SrcRegister src1, SrcRegister src2, SrcRegister src3) {
			this.op = op;
			this.dest = dest;
			this.src1 = src1;
			this.src2 = src2;
			this.src3 = src3;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public TernaryOpKind Operation {
			get { return op; }
		}

		public DestRegister Dest {
			get { return dest; }
		}

		public SrcRegister Source1 {
			get { return src1; }
		}

		public SrcRegister Source2 {
			get { return src2; }
		}

		public SrcRegister Source3 {
			get { return src3; }
		}

		public override string ToString () {
			return String.Format ("{0} = {1} {2} {3}, {4} ", dest, src1, op, src2, src3);
		}
	}

	public class BinaryOp : Instruction {
		BinOpKind op;
		DestRegister dest;
		SrcRegister src1;
		SrcRegister src2;

		public BinaryOp (BinOpKind op, DestRegister dest, SrcRegister src1, SrcRegister src2) {
			this.op = op;
			this.dest = dest;
			this.src1 = src1;
			this.src2 = src2;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public BinOpKind Operation {
			get { return op; }
		}

		public DestRegister Dest {
			get { return dest; }
		}

		public SrcRegister Source1 {
			get { return src1; }
		}

		public SrcRegister Source2 {
			get { return src2; }
		}

		public override string ToString () {
			return String.Format ("{0} = {1} {2} {3}", dest, src1, op, src2);
		}
	}

	public class UnaryOp : Instruction {
		UnaryOpKind op;
		DestRegister dest;
		SrcRegister src;

		public UnaryOp (UnaryOpKind op, DestRegister dest, SrcRegister src) {
			this.op = op;
			this.dest = dest;
			this.src = src;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public UnaryOpKind Operation {
			get { return op; }
		}

		public DestRegister Dest {
			get { return dest; }
		}

		public SrcRegister Source {
			get { return src; }
		}

		public override string ToString () {
			return String.Format ("{0} = {1} {2}", dest, op, src);
		}
	}

	public class Mov : Instruction {
		DestRegister dest;
		SrcRegister src;

		public Mov (DestRegister dest, SrcRegister src) {
			this.dest = dest;
			this.src = src;
		}

		public override void Visit (InstructionVisitor visitor) {
			visitor.Visit (this);
		}

		public DestRegister Dest {
			get { return dest; }
		}

		public SrcRegister Source {
			get { return src; }
		}

		public override string ToString () {
			return String.Format ("mov {0} = {1}", dest, src);
		}
	}
}