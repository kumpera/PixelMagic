using System;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Windows.Shapes;

namespace ShaderTestGen
{
	public class Shader : ShaderEffect
	{
		public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty ("Input", typeof(Shader), 0);

		PixelShader shader;

		public Shader (string uri)
		{
			shader = new PixelShader ();
			shader.UriSource = new Uri (Driver.MakePath (uri));
			PixelShader = shader;

			UpdateShaderValue (InputProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}
	}

	public class ScalarShader : Shader
	{
		static readonly DependencyProperty[] ScalarProperties = new DependencyProperty [32];

		static ScalarShader () {
			for (int i = 0; i < 32; ++i)
			ScalarProperties [i] = DependencyProperty.Register ("C" + i, typeof (float), typeof (ScalarShader), new UIPropertyMetadata (0f, PixelShaderConstantCallback (i)));
		}

		public ScalarShader (string uri) : base (uri)
		{
			for (int i = 0; i < 32; ++i)
				UpdateShaderValue (ScalarProperties [i]);
		}

		public float C0
		{
			get { return (float)GetValue (ScalarProperties [0]); }
			set { SetValue (ScalarProperties [0], value); }
		}

		public float C1
		{
			get { return (float)GetValue (ScalarProperties [1]); }
			set { SetValue (ScalarProperties [1], value); }
		}

		public float C2
		{
			get { return (float)GetValue (ScalarProperties [2]); }
			set { SetValue (ScalarProperties [2], value); }
		}

		public float C3
		{
			get { return (float)GetValue (ScalarProperties [3]); }
			set { SetValue (ScalarProperties [3], value); }
		}

		public float C4
		{
			get { return (float)GetValue (ScalarProperties [4]); }
			set { SetValue (ScalarProperties [4], value); }
		}

		public float C5
		{
			get { return (float)GetValue (ScalarProperties [5]); }
			set { SetValue (ScalarProperties [5], value); }
		}
	}
}
