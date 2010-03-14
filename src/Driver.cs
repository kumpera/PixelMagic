//
// Driver.cs
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
using Mono.Options;
using PixelMagic.Cairo;

namespace PixelMagic {

	public class Driver {
		public static int Main (string[] args) {
			ShaderData sd = new ShaderData ();

			bool dump = false, trace = false, interpreter = false, help = false;
			OptionSet opts = new OptionSet () {
				{ "d|dump", "Decompile the shader to stdout.", v => dump = true },
				{ "t|trace", "Enable tracing of execution (best used with --interpreter).", v => trace = true },
				{ "i|interpreter", "Use the interpreter instead of the JIT.", v => interpreter = true },
				{ "h|help", "Show this message and exit.", v => help = true },
				{ "c:", "Set the scalar value of a constant register", (k, v) =>  sd.SetConstant (int.Parse (k), float.Parse (v)) },
				{ "p:", "Set the pointer value of a constant register", (k, v) => {
					var coords = v.Split (new char[] {','});
					sd.SetConstant (int.Parse (k), float.Parse (coords [0]), float.Parse (coords [1]));
					}
				}
			};

			List<string> extra;
			try {
				extra = opts.Parse (args);
			} catch (OptionException e) {
				Console.WriteLine ("{0}\nTry 'shader --help' for more information.", e.Message);
				return 1;
			}

			if ((extra.Count != 3 && !dump) || (extra.Count != 1 && dump))
				help = true;

			if (help) {
				Console.WriteLine ("Usage: shader [options] shader input-image output-image");
				Console.WriteLine ("Apply the given shader to input-image and save it to output-image");
				opts.WriteOptionDescriptions (Console.Out);
				return 2;
			}

			if (trace)
				TracingConfig.Enable ();

			Parser parser = new Parser (extra [0]);
			var insList = parser.Parse ();
			if (dump) {
				foreach (var i in insList)
					Console.WriteLine (i);
				return 0;
			}

			var srcImg = new CairoImageSurface (extra [1]);
			var dstImg = srcImg.CreateSimilar ();

			Texture intex = new CairoTexture (srcImg);
			Texture outtex =  new CairoTexture (dstImg);

			sd.SetSampler (0, new Sampler (intex));
			sd.SetOutputTexture (0, outtex);

			if (interpreter) {
				Interpreter interp = new Interpreter (insList);
				interp.Run (sd);
			} else {
				CodeGenContext ctx = new CodeGenContext (insList);
				CompiledShader shader = ctx.Compile ();
				shader (sd);
			}
			dstImg.SaveToPng (extra [2]);
			return 0;
		}
	}
}
