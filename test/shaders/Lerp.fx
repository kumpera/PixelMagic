float Saturation : register (C0);

sampler2D implicitInputSampler : register(S0);

float4 ApplySat (float4 color, float a, float b) {
	float f = 1 - a;
	float g = (1  	- b) * Saturation;
	return lerp (f, color, g);
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 high = 1 - Saturation;
	float4 sample = tex2D ( implicitInputSampler, uv );

	float4 color = ApplySat (sample, uv.x, uv.y);
	color.a = 1;

	return color;
}
