sampler2D implicitInputSampler : register(S0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float4 up = tex2D ( implicitInputSampler, uv );
	float4 down = tex2D ( implicitInputSampler, 1 - uv );

	up.r = dot (up.rgb, down.rgb);
	return up;
}
