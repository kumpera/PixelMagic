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

	public class SingleScalarShader : Shader
	{

		public static readonly DependencyProperty ScalarProperty = DependencyProperty.Register ("Scalar", typeof(double), typeof (SingleScalarShader), new UIPropertyMetadata (0.25, PixelShaderConstantCallback (0)));

		public SingleScalarShader (string uri) : base (uri)
		{
			UpdateShaderValue (ScalarProperty);
		}

        public double Scalar
        {
            get { return (double)GetValue (ScalarProperty); }
            set { SetValue (ScalarProperty, value); }
        }
	}

}
