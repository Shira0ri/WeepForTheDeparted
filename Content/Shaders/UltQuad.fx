float4x4 transformMatrix;
float4 uSilhouetteColor;
float uSilhouetteStrength;

texture uQuadTex;
sampler quadLinearSamp = sampler_state
{
	texture = <uQuadTex>;
	magfilter = LINEAR;
	minfilter = LINEAR;
	mipfilter = NONE;
	AddressU = clamp;
	AddressV = clamp;
};

struct VSInput
{
	float4 Position : POSITION0;
	float2 TexCoords : TEXCOORD0;
	float4 Color : COLOR0;
};

struct PSInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TexCoords : TEXCOORD0;
};

PSInput MainVS(VSInput input)
{
	PSInput output;
	output.Position = mul(input.Position, transformMatrix);
	output.Color = input.Color;
	output.TexCoords = input.TexCoords;
	return output;
}

float4 QuadLinearPS(PSInput input) : COLOR0
{
	float4 tex = tex2D(quadLinearSamp, input.TexCoords);
	float4 sil = float4(uSilhouetteColor.rgb * tex.a, tex.a);
	float4 mixed = lerp(tex, sil, saturate(uSilhouetteStrength));
	return mixed * input.Color;
}

float4 RipplePS(PSInput input) : COLOR0
{
	float profile = sin(saturate(input.TexCoords.y) * 3.14159265);
	profile = pow(saturate(profile), 0.72);
	return input.Color * profile;
}

float4 VignettePS(PSInput input) : COLOR0
{
	float2 centered = (input.TexCoords - float2(0.5, 0.5)) * 2.0;
	float dist = length(centered);
	float vig = smoothstep(0.58, 1.38, dist);
	return input.Color * vig;
}

technique QuadLinearTech
{
	pass P0
	{
		VertexShader = compile vs_3_0 MainVS();
		PixelShader = compile ps_3_0 QuadLinearPS();
	}
}

technique RippleTech
{
	pass P0
	{
		VertexShader = compile vs_3_0 MainVS();
		PixelShader = compile ps_3_0 RipplePS();
	}
}

technique VignetteTech
{
	pass P0
	{
		VertexShader = compile vs_3_0 MainVS();
		PixelShader = compile ps_3_0 VignettePS();
	}
}
