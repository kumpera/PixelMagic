sampler2D implicitInputSampler : register(S0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 tx = tex2D( implicitInputSampler, uv );
	float4 color = 1;
	color.r = tx.g;
	color.g = tx.b;
	color.b = tx.r;
	return color;
}
