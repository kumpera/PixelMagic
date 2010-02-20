//
// ShaderData.cs
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
	public class ShaderData {
		Sampler[] samplers = new Sampler [32];
		Texture[] outTex = new Texture [1];
		Dictionary <int,Vector4f> constants = new Dictionary <int,Vector4f> ();

		public void SetSampler (int idx, Sampler sampler) {
			samplers [idx] = sampler;
		}

		public void SetOutputTexture (int idx, Texture tex) {
			outTex [idx] = tex;
		}

		public void SetConstant (int idx, float value) {
			constants [idx] = new Vector4f (value);
		}

		public Sampler GetSampler (int idx) {
			return samplers [idx];
		}

		public Texture GetOutputTexture (int idx) {
			return outTex [idx];
		}

		public Vector4f GetConstant (int idx) {
			return constants [idx];
		}

		public Vector4f GetConstantOrZero (int idx) {
			if (!constants.ContainsKey (idx))
				return new Vector4f ();
			return constants [idx];
		}

		internal Dictionary <int,Vector4f> GetConstants () {
			return constants;
		}

	}
}
