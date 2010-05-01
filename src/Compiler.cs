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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Mono.Simd;

namespace PixelMagic {
	public delegate void CompiledShader (ShaderData data);

	internal class HeaderWriterVisitor : InstructionVisitor {
		CodeGenContext ctx;

		internal HeaderWriterVisitor (CodeGenContext ctx) {
			this.ctx = ctx;
		}

		public void Visit (SetConst ins) {
			ctx.DefineConst (ins.Number, ins.Value);
		}

		public void Visit (DefVar ins) {
			ctx.DefineVar (ins.Dest, ins.Kind);
		}

		public void Visit (TexLoad ins) {
		}

		public void Visit (BinaryOp ins) {
		}

		public void Visit (UnaryOp ins) {
		}

		public void Visit (Mov ins) {
		}

		public void Visit (TernaryOp ins) {
		}

		public void Visit (Nop nop) {
		}
	}

	internal class ShaderRequisitesVisitor : InstructionVisitor {
		/*if true constant is external*/
		Dictionary <int, bool> constantMap = new Dictionary <int, bool> ();
		HashSet <int> writeMasks = new HashSet <int> ();
		CodeGenContext ctx;

		internal ShaderRequisitesVisitor (CodeGenContext ctx) {
			this.ctx = ctx;
		}

		internal void LoadExternalVars () {
			foreach (var cons in constantMap) {
				//Console.WriteLine ("const {0} external {1}", cons.Key, cons.Value);
				if (cons.Value)
					ctx.LoadConst (cons.Key);
			}
			foreach (var wm in writeMasks) {
				//Console.WriteLine ("mask {0:X}", wm);
				ctx.GetMask (wm);
			}
		}

		void VisitReg (Register reg) {
			if (reg.Kind == RegKind.Constant) {
				if (!constantMap.ContainsKey (reg.Number))
					constantMap [reg.Number] = true;
			}
		}

		void VisitSrcReg (SrcRegister src) {
			VisitReg (src);
		}

		void VisitDestReg (DestRegister dest) {
			VisitReg (dest);
			if (dest.WriteMask != 0xF)
				writeMasks.Add (dest.WriteMask);
		}

		public void Visit (SetConst ins) {
			constantMap [ins.Number] = false;
		}

		public void Visit (DefVar ins) {
			VisitDestReg (ins.Dest);
		}

		public void Visit (TexLoad ins) {
			VisitSrcReg (ins.Sampler);
			VisitSrcReg (ins.Texture);
			VisitDestReg (ins.Dest);
		}

		public void Visit (BinaryOp ins) {
			VisitSrcReg (ins.Source1);
			VisitSrcReg (ins.Source2);
			VisitDestReg (ins.Dest);
		}

		public void Visit (UnaryOp ins) {
			VisitSrcReg (ins.Source);
			VisitDestReg (ins.Dest);			
		}

		public void Visit (Mov ins) {
			VisitSrcReg (ins.Source);
			VisitDestReg (ins.Dest);			
		}

		public void Visit (TernaryOp ins) {
			VisitSrcReg (ins.Source1);
			VisitSrcReg (ins.Source2);
			VisitSrcReg (ins.Source3);
			VisitDestReg (ins.Dest);
		}

		public void Visit (Nop nop) {
		}
	}

	internal class CodeGenVisitor : InstructionVisitor {
		CodeGenContext ctx;

		internal CodeGenVisitor (CodeGenContext ctx) {
			this.ctx = ctx;
		}

		public void Visit (SetConst ins) {
		}

		public void Visit (DefVar ins) {
		}

		public void Visit (TexLoad ins) {
			if (ins.Sampler.Kind != RegKind.SamplerState)
				throw new Exception ("bad sampler input reg " + ins.Sampler.Kind);
			ctx.SampleTexture (ins.Sampler.Number, ins.Texture);
			ctx.StoreValue (ins.Dest);
		}

		public void Visit (BinaryOp ins) {
			ctx.LoadValue (ins.Source1);
			ctx.LoadValue (ins.Source2);
			ctx.EmitBinary (ins.Operation);
			ctx.StoreValue (ins.Dest);
		}

		public void Visit (UnaryOp ins) {
			ctx.EmitUnary (ins);
			ctx.StoreValue (ins.Dest);
		}

		public void Visit (Mov ins) {
			ctx.LoadValue (ins.Source);
			ctx.StoreValue (ins.Dest);
		}

		public void Visit (TernaryOp ins) {
			ctx.EmitTernary (ins);
		}

		public void Visit (Nop nop) {
		}
	}

	public class CodeGenContext {
		const string ASSEMBLY_NAME = "ShaderLib";

		AssemblyBuilder assembly;
		ModuleBuilder module;
		TypeBuilder typeBuilder;
		MethodBuilder method;
		ILGenerator ilgen;
		List<Instruction> insList;
		Dictionary<int, LocalBuilder> constMap = new Dictionary<int, LocalBuilder> ();
		Dictionary<int, LocalBuilder> samplerMap = new Dictionary<int, LocalBuilder> ();
		Dictionary<int, LocalBuilder> tempMap = new Dictionary<int, LocalBuilder> ();
		Dictionary<int, LocalBuilder> maskMap = new Dictionary<int, LocalBuilder> ();

		LocalBuilder colorOut0;
		LocalBuilder out0, height, width, wq, hq;
		LocalBuilder tex0, loop_i, loop_j;
		Label body_i, body_j, cond_i, cond_j;

		int tmpVar;

		public CodeGenContext (List<Instruction> insList) {
			AssemblyName assemblyName = new AssemblyName ();
			assemblyName.Name = ASSEMBLY_NAME;

			assembly = Thread.GetDomain ().DefineDynamicAssembly (assemblyName, AssemblyBuilderAccess.RunAndSave, ".");
			module = assembly.DefineDynamicModule ("module1", "ps.dll");
			typeBuilder = module.DefineType ("Shader", TypeAttributes.Public);
			method = typeBuilder.DefineMethod ("Exec", MethodAttributes.Public | MethodAttributes.Static, typeof (void), new Type [] { typeof (ShaderData) });
			ilgen = method.GetILGenerator ();

			this.insList = insList;
		}

		CompiledShader Finish () {
			ilgen.Emit (OpCodes.Ret);
			Type result = typeBuilder.CreateType ();
			assembly.Save ("ps.dll");
			return (CompiledShader)Delegate.CreateDelegate (typeof (CompiledShader), result.GetMethod ("Exec"));
		}

		public CompiledShader Compile () {
			ShaderRequisitesVisitor reqs = new ShaderRequisitesVisitor (this);
			foreach (var i in insList)
				i.Visit (reqs);

			reqs.LoadExternalVars ();

			HeaderWriterVisitor header = new HeaderWriterVisitor (this);
			//Emit code to load used vars & consts
			foreach (var i in insList)
				i.Visit (header);

			
			EmitLoopVars ();

			EmitLoopStart ();


			CodeGenVisitor codegen = new CodeGenVisitor (this);
			foreach (var i in insList)
				i.Visit (codegen);

			EmitLoopTail ();

			return Finish ();
		}

		void LoadLiteral (LocalBuilder lb, Vector4f val) {
			//TODO optimize for common patterns.
			ilgen.Emit (OpCodes.Ldloca, lb);
			ilgen.Emit (OpCodes.Ldc_R4, val.X);
			ilgen.Emit (OpCodes.Ldc_R4, val.Y);
			ilgen.Emit (OpCodes.Ldc_R4, val.Z);
			ilgen.Emit (OpCodes.Ldc_R4, val.W);
			ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetConstructor (new Type [] {typeof (float), typeof (float), typeof (float), typeof (float) }));
		}

		LocalBuilder DeclareTmp (Type type) {
			return DeclareLocal (type, "_tmp_" + tmpVar++);
		}
	
		LocalBuilder DeclareLocal (Type type, string name) {
			LocalBuilder lb = ilgen.DeclareLocal (type);
			lb.SetLocalSymInfo (name);
			return lb;
		}

		LocalBuilder DeclareAndZeroLocal (Type type, string name) {
			LocalBuilder lb = DeclareLocal (type, name);
			ilgen.Emit (OpCodes.Ldloca, lb);
			ilgen.Emit (OpCodes.Initobj, type);
			return lb;
		}
	
		void EmitLoopVars () {
			out0 = DeclareLocal (typeof (Texture), "out0");
			width = DeclareLocal (typeof (int), "width");
			height = DeclareLocal (typeof (int), "height");
			wq = DeclareLocal (typeof (float), "wq");
			hq = DeclareLocal (typeof (float), "hq");

			//Texture out0 = ctx.GetOutputTexture (0);
			ilgen.Emit (OpCodes.Ldarg_0);
			ilgen.Emit (OpCodes.Ldc_I4, 0);
				ilgen.Emit (OpCodes.Call, typeof (ShaderData).GetMethod ("GetOutputTexture"));
			ilgen.Emit (OpCodes.Stloc, out0);

			//int width = out0.Width;
			ilgen.Emit (OpCodes.Ldloc, out0);
			ilgen.Emit (OpCodes.Callvirt, typeof (Texture).GetMethod ("get_Width"));
			ilgen.Emit (OpCodes.Stloc, width);

			//int height = out0.Heigh;
			ilgen.Emit (OpCodes.Ldloc, out0);
			ilgen.Emit (OpCodes.Callvirt, typeof (Texture).GetMethod ("get_Height"));
			ilgen.Emit (OpCodes.Stloc, height);

			//float wq = 1f / (float)output.width;
			ilgen.Emit (OpCodes.Ldc_R4, 1.0f);
			ilgen.Emit (OpCodes.Ldloc, width);
			ilgen.Emit (OpCodes.Conv_R4);
			ilgen.Emit (OpCodes.Div);
			ilgen.Emit (OpCodes.Stloc, wq);

			//float hq = 1f / (float)output.height;
			ilgen.Emit (OpCodes.Ldc_R4, 1.0f);
			ilgen.Emit (OpCodes.Ldloc, height);
			ilgen.Emit (OpCodes.Conv_R4);
			ilgen.Emit (OpCodes.Div);
			ilgen.Emit (OpCodes.Stloc, hq);
		}

		void EmitLoopStart () {
			colorOut0 = DeclareLocal (typeof (Vector4f), "colorOut0");

			//tex0.x = wq / 2;
			if (tex0 != null) {
				ilgen.Emit (OpCodes.Ldloca, tex0);
				ilgen.Emit (OpCodes.Ldloc, wq);
				ilgen.Emit (OpCodes.Ldc_R4, 2f);
				ilgen.Emit (OpCodes.Div);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("set_X"));
			}

			loop_i = DeclareLocal (typeof (int), "i");
			loop_j = DeclareLocal (typeof (int), "j");

			body_i = ilgen.DefineLabel ();
			body_j = ilgen.DefineLabel ();
			cond_i = ilgen.DefineLabel ();
			cond_j = ilgen.DefineLabel ();

			//i = 0;
			ilgen.Emit (OpCodes.Ldc_I4_0);
			ilgen.Emit (OpCodes.Stloc, loop_i);

			//goto loop step
			ilgen.Emit (OpCodes.Br, cond_i);
			ilgen.MarkLabel (body_i);

			//tex0.y = hq / 2;
			if (tex0 != null) {
				ilgen.Emit (OpCodes.Ldloca, tex0);
				ilgen.Emit (OpCodes.Ldloc, hq);
				ilgen.Emit (OpCodes.Ldc_R4, 2f);
				ilgen.Emit (OpCodes.Div);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("set_Y"));
			}

			//j = 0;
			ilgen.Emit (OpCodes.Ldc_I4_0);
			ilgen.Emit (OpCodes.Stloc, loop_j);
		
			//goto loop step
			ilgen.Emit (OpCodes.Br, cond_j);
			ilgen.MarkLabel (body_j);

			ilgen.Emit (OpCodes.Ldloca, colorOut0);
			ilgen.Emit (OpCodes.Initobj, typeof (Vector4f));
		}

		void EmitLoopTail () {
			//output.WriteColor (i, j, colorOut [0]);
			ilgen.Emit (OpCodes.Ldloc, out0);
			ilgen.Emit (OpCodes.Ldloc, loop_i);
			ilgen.Emit (OpCodes.Ldloc, loop_j);
			ilgen.Emit (OpCodes.Ldloc, colorOut0);
			ilgen.Emit (OpCodes.Call, typeof (Texture).GetMethod ("WriteColor"));
			

			//tex0.y += hq;
			if (tex0 != null) {
				ilgen.Emit (OpCodes.Ldloca, tex0);
				ilgen.Emit (OpCodes.Ldloca, tex0);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_Y"));
				ilgen.Emit (OpCodes.Ldloc, hq);
				ilgen.Emit (OpCodes.Add);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("set_Y"));
			}

			//++j
			ilgen.Emit (OpCodes.Ldloc, loop_j);
			ilgen.Emit (OpCodes.Ldc_I4_1);
			ilgen.Emit (OpCodes.Add);
			ilgen.Emit (OpCodes.Stloc, loop_j);

			//if (j < height) goto body j
			ilgen.MarkLabel (cond_j);
			ilgen.Emit (OpCodes.Ldloc, loop_j);
			ilgen.Emit (OpCodes.Ldloc, height);
			ilgen.Emit (OpCodes.Blt, body_j);

		
			//tex0.x += wq;
			if (tex0 != null) {
				ilgen.Emit (OpCodes.Ldloca, tex0);
				ilgen.Emit (OpCodes.Ldloca, tex0);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_X"));
				ilgen.Emit (OpCodes.Ldloc, wq);
				ilgen.Emit (OpCodes.Add);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("set_X"));
			}

			//++i
			ilgen.Emit (OpCodes.Ldloc, loop_i);
			ilgen.Emit (OpCodes.Ldc_I4_1);
			ilgen.Emit (OpCodes.Add);
			ilgen.Emit (OpCodes.Stloc, loop_i);

			//if (i < width) goto body i
			ilgen.MarkLabel (cond_i);
			ilgen.Emit (OpCodes.Ldloc, loop_i);
			ilgen.Emit (OpCodes.Ldloc, width);
			ilgen.Emit (OpCodes.Blt, body_i);
		
		}

		internal void DefineConst (int num, Vector4f initialVal) {
			if (constMap.ContainsKey (num))
				throw new Exception (String.Format ("constant {0} already defined", num));

			LocalBuilder lb = DeclareLocal (typeof (Vector4f), "const_" + num);
			constMap [num] = lb;

			LoadLiteral (lb, initialVal);
		}

		internal void LoadConst (int num) {
			if (constMap.ContainsKey (num))
				throw new Exception (String.Format ("constant {0} already defined", num));

			LocalBuilder lb = DeclareLocal (typeof (Vector4f), "const_" + num);
			constMap [num] = lb;

			ilgen.Emit (OpCodes.Ldarg_0);
			ilgen.Emit (OpCodes.Ldc_I4, num);
			ilgen.Emit (OpCodes.Call, typeof (ShaderData).GetMethod ("GetConstantOrZero"));
			ilgen.Emit (OpCodes.Stloc, lb);
		}

		internal void DefineVar (DestRegister reg, TextureKind kind) {
			switch (reg.Kind) {
			case RegKind.Texture:
				if (tex0 != null)
					throw new Exception ("Cannot handle multiple texture registers");
				if (reg.Number != 0)
					throw new Exception ("Only one texture register supported");
				//FIXME since we only support 2d textures, is this the right thing?
				if (reg.WriteMask != DestRegister.MakeMask (true, true, false, false))
					throw new Exception ("Cannot handle a texture register with a write mask different than rg");
				tex0 = DeclareAndZeroLocal (typeof (Vector4f), "tex_" + reg.Number); //FIXME using a pair of floats would be faster
				break;
			case RegKind.SamplerState:
				if (kind != TextureKind.Text2d)
					throw new Exception ("Cannot handle non text2d samplers.");

				if (samplerMap.ContainsKey (reg.Number))
					throw new Exception (String.Format ("sampler {0} already defined", reg.Number));

				LocalBuilder lb = DeclareLocal (typeof (Sampler), "sampler_" + reg.Number);
				samplerMap [reg.Number] = lb;

				ilgen.Emit (OpCodes.Ldarg_0);
				ilgen.Emit (OpCodes.Ldc_I4, reg.Number);
				ilgen.Emit (OpCodes.Call, typeof (ShaderData).GetMethod ("GetSampler"));
				ilgen.Emit (OpCodes.Stloc, lb);
				break;
			}
		}

		internal void SampleTexture (int sampler, SrcRegister texReg) {
			ilgen.Emit (OpCodes.Ldloc, samplerMap [sampler]);
			LoadValue (texReg);
			ilgen.Emit (OpCodes.Call, typeof (Sampler).GetMethod ("Sample"));
		}

		internal LocalBuilder GetReg (RegKind kind, int number) {
			switch (kind) {
			case RegKind.Temp:
				if (!tempMap.ContainsKey (number)) {
					tempMap [number] = DeclareAndZeroLocal (typeof (Vector4f), "temp_" + number);
				}
				return tempMap [number];

			case RegKind.Constant:
				return constMap [number];

			case RegKind.ColorOut:
				if (number != 0)
					throw new Exception ("don't know how to handle colorOut != 0");
				return colorOut0;
			case RegKind.Texture: //XXX do number check when we support multiple texture regs
				return tex0;
			}
			throw new Exception ("Invalid reg kind " + kind);
		}

		internal void EmitTernary (TernaryOp ins) {
			//FIXME it might be an issue if arguments are not of type Vector4f
			switch (ins.Operation) {
			case TernaryOpKind.Cmp: {
				var mask = DeclareTmp (typeof (Vector4f));
				//mask = 0
				ilgen.Emit (OpCodes.Ldloca, mask);
				ilgen.Emit (OpCodes.Initobj, typeof (Vector4f));

				//a < tmp
				LoadValue (ins.Source1);
				ilgen.Emit (OpCodes.Ldloc, mask);
				ilgen.Emit (OpCodes.Call, typeof (VectorOperations).GetMethod ("CompareLessThan", new Type[] { typeof (Vector4f), typeof (Vector4f)}));
				ilgen.Emit (OpCodes.Stloc, mask);

				//mask & c
				ilgen.Emit (OpCodes.Ldloc, mask);
				LoadValue (ins.Source3);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_BitwiseAnd"));

				//mask.AndNot (b);
				ilgen.Emit (OpCodes.Ldloc, mask);
				LoadValue (ins.Source2);
				ilgen.Emit (OpCodes.Call, typeof (VectorOperations).GetMethod ("AndNot", new Type[] { typeof (Vector4f), typeof (Vector4f)}));

				//res = (mask & c) | mask.AndNot (b);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_BitwiseOr"));
				StoreValue (ins.Dest);
				break;
			} 

			case TernaryOpKind.Mad: //a * b + c 
				LoadValue (ins.Source1);
				LoadValue (ins.Source2);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Multiply"));
				LoadValue (ins.Source3);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Addition"));
				StoreValue (ins.Dest);
				break;

			case TernaryOpKind.SinCos: //.x = cos (a.X) .y = cos (a.X)
				//XXX maybe we should use the macro expansion provided in the driver docs for HLSL
				switch (ins.Dest.WriteMask) {
				case 1: //R cos
					ApplyMathScalarUnaryToElement (ins.Dest, ins.Source1, "Cos", "X");
					break;					
				case 2: //G Sin
					ApplyMathScalarUnaryToElement (ins.Dest, ins.Source1, "Sin", "Y");
					break;
				case 3: //RG
					ApplyMathScalarUnaryToElement (ins.Dest, ins.Source1, "Cos", "X");
					ApplyMathScalarUnaryToElement (ins.Dest, ins.Source1, "Sin", "Y");
					break;
				default:
					throw new Exception ("Invalid writeMask for sincos " + ins.Dest.WriteMask.ToString ("X"));
				}
				break;
			case TernaryOpKind.Lrp:  //a * b + (1 - a) * c;
				//a * b
				LoadValue (ins.Source1);
				LoadValue (ins.Source2);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Multiply"));
				//1 - a
				EmitTempVector4f (1);
				LoadValue (ins.Source1);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Subtraction"));
				// (1 - a) * c
				LoadValue (ins.Source3);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Multiply"));

				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Addition"));
				StoreValue (ins.Dest);
				break;
			case TernaryOpKind.Dp2Add: {//res = a.r * b.r + a.g * b.g + c.swizzle
				LoadValue (ins.Source1);
				LoadValue (ins.Source2);
				LoadValue (ins.Source3);
				ilgen.Emit (OpCodes.Call, typeof (SimdExtras).GetMethod ("Dp2Add"));
				StoreValue (ins.Dest);
				break;

				//a * b
				LoadValue (ins.Source1);
				LoadValue (ins.Source2);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Multiply"));
				ilgen.Emit (OpCodes.Dup);

				//FIXME we could use HorizontalAdd here
				EmitShuffle (ShuffleSel.XFromY); //[x,y,_,_] [y,y,_,_]
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Addition")); //[x + y,_,_,_]

				//+c.swizzle
				LoadValue (ins.Source3);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Addition"));
				EmitShuffle (ShuffleSel.ExpandX);
				StoreValue (ins.Dest);
				break;
			}
			default:
				throw new Exception ("can't handle ternop " + ins.Operation);
			}
		}

		void ApplyMathScalarUnaryToElement (DestRegister dest, SrcRegister src, string func, string element) {
			ilgen.Emit (OpCodes.Ldloca, GetReg (dest.Kind, dest.Number));
			LoadScalar (src);
			ilgen.Emit (OpCodes.Call, typeof (Math).GetMethod (func));
			ilgen.Emit (OpCodes.Conv_R4);
			ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("set_" + element));
		}

		void LoadScalar (SrcRegister src) {
			ilgen.Emit (OpCodes.Ldloca, GetReg (src.Kind, src.Number));
			switch (src.Swizzle) {
			case 0: //R 
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_X"));
				break;
			case (1 << 0) | (1 << 2)  | (1 << 4) | (1 << 6): //G  
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_Y"));
				break;
			case (2 << 0) | (2 << 2)  | (2 << 4) | (2 << 6): //B
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_Z"));
				break;
			case (3 << 0) | (3 << 2)  | (3 << 4) | (3 << 6): //A
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_W"));
				break;
			default:
				throw new Exception ("Invalid swizzle for scalar " + src.Swizzle.ToString ("X"));
			}
		}

		void EmitShuffle (ShuffleSel shuffle) {
			ilgen.Emit (OpCodes.Ldc_I4, (int)shuffle);
			ilgen.Emit (OpCodes.Call, typeof (VectorOperations).GetMethod ("Shuffle", new Type[] { typeof (Vector4f), typeof (ShuffleSel)}));
		}

		internal void EmitBinary (BinOpKind op) {
			//FIXME it might be an issue if arguments are not of type Vector4f
			MethodInfo mi = null;
			switch (op) {
			case BinOpKind.Add:
				mi = typeof (Vector4f).GetMethod ("op_Addition");
				break;
			case BinOpKind.Sub:
				mi = typeof (Vector4f).GetMethod ("op_Subtraction");
				break;
			case BinOpKind.Mul:
				mi = typeof (Vector4f).GetMethod ("op_Multiply");
				break;
			case BinOpKind.Max:
				mi = typeof (VectorOperations).GetMethod ("Max", new Type[] { typeof (Vector4f), typeof (Vector4f)});
				break;
			case BinOpKind.Dp3:
				//This is a very fuck'd up code sequence, figure out how to speed it up
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Multiply"));
				ilgen.Emit (OpCodes.Dup);
				ilgen.Emit (OpCodes.Dup);
				//FIXME we could use HorizontalAdd for this step
				EmitShuffle (ShuffleSel.XFromY); //[x,y,z,w] [x,y,z,w] [y,y,z,w]
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Addition")); //[x,y,z,w] [x + y,_,_,_]

				EmitShuffle (ShuffleSel.ZFromY); //[x,y,z,w] [_,_,x + y, _]
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Addition")); //[_,_, x + y + z, _]
				EmitShuffle (ShuffleSel.ExpandZ); //[dp3, dp3, dp3, dp3]
				break;
			case BinOpKind.Min:
				mi = typeof (VectorOperations).GetMethod ("Min", new Type[] { typeof (Vector4f), typeof (Vector4f)});
				break;
			default:
				throw new Exception ("can't handle binop " + op);
			}
			if (mi != null)
				ilgen.Emit (OpCodes.Call, mi);
		}

		void EmitTempVector4f (float arg) {
			ilgen.Emit (OpCodes.Ldc_R4, arg);
			ilgen.Emit (OpCodes.Newobj, typeof (Vector4f).GetConstructor (new Type [] { typeof (float) }));
		}

		internal void EmitUnary (UnaryOp ins) {
			switch (ins.Operation) {
			case UnaryOpKind.Rcp:
				EmitTempVector4f (1);
				LoadValue (ins.Source);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Division"));
				break;
			case UnaryOpKind.Frc:
				LoadValue (ins.Source);
				ilgen.Emit (OpCodes.Call, typeof (SimdExtras).GetMethod ("FractionalPart"));
				break;
			case UnaryOpKind.Rsq:
				LoadValue (ins.Source);
				ilgen.Emit (OpCodes.Call, typeof (SimdExtras).GetMethod ("SquareRootReciprocal"));
				break;
			case UnaryOpKind.Abs:
				LoadValue (ins.Source);
				ilgen.Emit (OpCodes.Call, typeof (SimdExtras).GetMethod ("Absolute"));
				break;
			default:
				throw new Exception ("can't handle unop " + ins.Operation);
			}
		}

		void EmitVectorCast (Type src, Type to) {
			MethodInfo mi = null;
			foreach (var m in src.GetMethods (BindingFlags.Public | BindingFlags.Static)) {
				if (m.Name == "op_Explicit" && m.ReturnType == to) {
					mi = m;
					break;
				}
			}
			ilgen.Emit (OpCodes.Call, mi);
		}

		internal LocalBuilder GetMask (int writeMask) {
			if (!maskMap.ContainsKey (writeMask)) {
				var mask = DeclareLocal (typeof (Vector4f), "mask_" + writeMask);
				maskMap [writeMask] = mask;

				ilgen.Emit (OpCodes.Ldc_I4, (writeMask & 0x1) == 0x1 ? -1 : 0);
				ilgen.Emit (OpCodes.Ldc_I4, (writeMask & 0x2) == 0x2 ? -1 : 0);
				ilgen.Emit (OpCodes.Ldc_I4, (writeMask & 0x4) == 0x4 ? -1 : 0);
				ilgen.Emit (OpCodes.Ldc_I4, (writeMask & 0x8) == 0x8 ? -1 : 0);
				ilgen.Emit (OpCodes.Newobj, typeof (Vector4i).GetConstructor (new Type [] {typeof (int), typeof (int), typeof (int), typeof (int) }));
				EmitVectorCast (typeof (Vector4i), typeof (Vector4f));

				ilgen.Emit (OpCodes.Stloc, mask);

			}
			return maskMap [writeMask];
		}

		internal void StoreValue (DestRegister dst) {
			var dest = GetReg (dst.Kind, dst.Number);
			/*FIXME we must merge the instruction mask with the dest var mask.*/
			if (dst.WriteMask != 0xF) {
				//return 

				var mask = GetMask (dst.WriteMask);

				//src & mask
				ilgen.Emit (OpCodes.Ldloc, mask);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_BitwiseAnd"));

				//mask.AndNot (dest)
				ilgen.Emit (OpCodes.Ldloc, mask);
				ilgen.Emit (OpCodes.Ldloc, dest);
				ilgen.Emit (OpCodes.Call, typeof (VectorOperations).GetMethod ("AndNot", new Type[] { typeof (Vector4f), typeof (Vector4f)}));
			
				//(mask & src) | mask.AndNot (dest);
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_BitwiseOr"));
			}
			ilgen.Emit (OpCodes.Stloc, dest);
		}

		internal void LoadValue (SrcRegister src) {
			ilgen.Emit (OpCodes.Ldloc, GetReg (src.Kind, src.Number));

			if (src.Swizzle != SrcRegister.MakeSwizzle (0, 1, 2, 3))
				EmitShuffle ((ShuffleSel)src.Swizzle);

			switch (src.Modifier) {
			case SrcModifier.None:
				break;
			case SrcModifier.Negate:
				//FIXME We could be using a ^ (1 << 31) to negate (could we?)
				//FIXME Make The JIT intrinsify this 
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("get_MinusOne"));
				ilgen.Emit (OpCodes.Call, typeof (Vector4f).GetMethod ("op_Multiply"));
				break;
			default:
				throw new Exception ("can't handle src reg modifier " + src.Modifier);
			}
		}
	}
}