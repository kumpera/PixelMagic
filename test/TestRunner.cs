using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class Driver {
	static List<string> errorList = new List<string> ();
	static int ok = 0;
	static int bad_shader = 0;
	static int bad_result = 0;

	static bool ApplyShader (string test_name, string shader, string input, string result, string extra) {
		Process applyShader = new Process ();
		applyShader.StartInfo.FileName = "mono";
		applyShader.StartInfo.UseShellExecute = false;
		applyShader.StartInfo.Arguments = string.Format ("../src/shader.exe {0} {1} {2} {3}", extra, shader, input, result);
		applyShader.StartInfo.RedirectStandardError = true;
		applyShader.StartInfo.RedirectStandardOutput = true;

		var stdout = new StringBuilder ();
		var stderr = new StringBuilder ();
		applyShader.OutputDataReceived += (s, e) => { if (e.Data.Trim ().Length > 0) stdout.Append("\t").Append (e.Data).Append ("\n"); };
		applyShader.ErrorDataReceived += (s, e) => { if (e.Data.Trim ().Length > 0) stderr.Append("\t").Append (e.Data).Append ("\n"); };

		applyShader.Start ();
		applyShader.BeginOutputReadLine ();
		applyShader.BeginErrorReadLine ();

		applyShader.WaitForExit ();
		var exitCode = applyShader.ExitCode;
		applyShader.Dispose ();
		if (exitCode != 0)
			errorList.Add (String.Format ("Test {0} failed with:\n{1}{2}", test_name, stdout, stderr));
		return exitCode == 0;
	}


	static double CheckResult (string reference, string result, string diff, double tolerance) {
		Process compareImg = new Process ();
		compareImg.StartInfo.FileName = "compare";
		compareImg.StartInfo.UseShellExecute = false;
		compareImg.StartInfo.Arguments = string.Format ("-fuzz {0}% -metric PAE {1} {2} {3}", tolerance, reference, result, diff);
		compareImg.StartInfo.RedirectStandardError = true;
		compareImg.StartInfo.RedirectStandardOutput = true;

		var stdout = new StringBuilder ();
		var stderr = new StringBuilder ();
		compareImg.OutputDataReceived += (s, e) => { if (e.Data.Trim ().Length > 0) stdout.Append (e.Data); };
		compareImg.ErrorDataReceived += (s, e) => { if (e.Data.Trim ().Length > 0) stderr.Append (e.Data); };

		compareImg.Start ();
		compareImg.BeginOutputReadLine ();
		compareImg.BeginErrorReadLine ();

		compareImg.WaitForExit ();
		var exitCode = compareImg.ExitCode;
		compareImg.Dispose ();
		if (exitCode != 0)
			return 100;

		string[] res = new Regex (".*\\((.*)\\)").Split (stderr.ToString ());
		return double.Parse (res [1]) * 100;
	}

	static void RunTest (string test_name, string shader, string input, string reference, string result, string diff, double tolerance, string extra) {
		if (!ApplyShader (test_name, shader, input, result, extra)) {
			Console.WriteLine ("[{0}] BAD SHADER", test_name);
			++bad_shader;
			return;
		}

		double res = CheckResult (reference, result, diff, tolerance);
		if (res > tolerance) {
			Console.WriteLine ("[{0}] BAD RESULT {1} - {2} expected", test_name, res, tolerance);
			++bad_result;
			return;
		}
		Console.WriteLine ("[{0}] OK {1:F3}%", test_name, res);
		++ok;
	}

	public static void Main () {
		StreamReader sr = new StreamReader ("tests.in");
		string line;

		while ((line = sr.ReadLine ()) != null) {
			line = line.Trim ();
			if (line == "")
				continue;

			string[] args = line.Split (new char [] {' '});
			var test_name = args [0];
			var shader = args [1];
			var input = args [2];
			var reference = args [3];
			var tolerance = double.Parse (args [4]);
			var extra = "";
			for (int i = 5; i < args.Length; ++i)
				extra += args [i] + " ";

			string result = string.Format ("results/{0}.png", test_name);
			string diff = string.Format ("results/{0}-diff.png", test_name);
			RunTest (test_name, shader, input, reference, result, diff, tolerance, extra);

			test_name = test_name + "-interp";
			result = string.Format ("results/{0}.png", test_name);
			diff = string.Format ("results/{0}-diff.png", test_name);
			extra = extra += " -i";
			RunTest (test_name, shader, input, reference, result, diff, tolerance, extra);
		}

		Console.WriteLine ("Results: OK {0} BAD SHADER {1} BAD RESULT {2}", ok, bad_shader, bad_result);
		foreach (var s in errorList)
			Console.WriteLine (s);
	}
}