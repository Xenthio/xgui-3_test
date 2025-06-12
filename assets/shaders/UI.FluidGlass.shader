HEADER
{
	DevShader = true;
	Version = 1;
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
MODES
{
	Default();
	Forward();
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
FEATURES
{
	#include "ui/features.hlsl"
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
COMMON
{
	#include "ui/common.hlsl"
}
  
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
VS
{
	#include "ui/vertex.hlsl"  
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
PS
{ 
	#include "ui/pixel.hlsl"

	DynamicCombo( D_TEXTURE_FILTERING, 0..3, Sys( PC ) ); 
	DynamicCombo( D_BORDER_IMAGE, 0..2, Sys( PC ) );
	DynamicCombo( D_BACKGROUND_IMAGE, 0..1, Sys( PC ) );
	DynamicCombo( D_REFRACTION_EFFECT, 0..1, Sys( PC ) );
	DynamicCombo( D_FRESNEL_EFFECT, 0..1, Sys( PC ) );
	DynamicCombo( D_EDGE_REFLECTION, 0..1, Sys( PC ) ); // DynamicCombo( D_REFLECTIVE_BORDER, 0..1, Sys( PC ) );
	DynamicCombo( D_BLUR_EFFECT, 0..1, Sys( PC ) );
	DynamicCombo( D_BLUR_QUALITY, 0..1, Sys( PC ) );

	bool HasBorder <Default( 0 ); Attribute( "HasBorder" );>;
	bool HasBorderImageFill <Default(  0 ); Attribute( "HasBorderImageFill" );>;
	float4 CornerRadius < Attribute( "BorderRadius" ); >;
	float4 BorderWidth < UiGroup( "Border" ); Attribute( "BorderSize" ); >;	
	float4 BorderImageSlice < UiGroup( "Border" ); Attribute( "BorderImageSlice"); >;
	float4 BorderColorL < UiType( Color ); Default4( 0.0, 0.0, 0.0, 1.0 ); UiGroup( "Border,10/Colors,10/1" ); Attribute( "BorderColorL" ); >;
	float4 BorderColorT < UiType( Color ); Default4( 0.0, 0.0, 0.0, 1.0 ); UiGroup( "Border,10/Colors,10/2" ); Attribute( "BorderColorT" ); >;
	float4 BorderColorR < UiType( Color ); Default4( 0.0, 0.0, 0.0, 1.0 ); UiGroup( "Border,10/Colors,10/3" ); Attribute( "BorderColorR" ); >;
	float4 BorderColorB < UiType( Color ); Default4( 0.0, 0.0, 0.0, 1.0 ); UiGroup( "Border,10/Colors,10/4" ); Attribute( "BorderColorB" ); >;

	float BorderReflectAmount < Default( 5.0 ); UiGroup( "Border" ); Attribute( "BorderReflectAmount" ); >;
	float BorderReflectFresnelPower < Default( 4.0 ); UiGroup( "Border" ); Attribute( "BorderReflectFresnelPower" ); >;
	float4 BorderReflectTint < UiType( Color ); Default4( 1.0, 1.0, 1.0, 1.0 ); UiGroup( "Border" ); Attribute( "BorderReflectTint" ); >;

	float ExtremeEdgePower < Default( 16.0 ); UiGroup( "Border" ); Attribute( "ExtremeEdgePower" ); >;
	float ExtremeEdgeSampleDistance < Default( 25.0 ); UiGroup( "Border" ); Attribute( "ExtremeEdgeSampleDistance" ); >; // Increased default for a further reach
	float ExtremeEdgeIntensityScale < Default( 0.75 ); UiGroup( "Border" ); Attribute( "ExtremeEdgeIntensityScale" ); >;

	float ChromaticAberrationAmount < Default( 0.5 ); UiGroup( "Refraction" ); Attribute( "ChromaticAberrationAmount" ); >;

	float4 FresnelColor < UiType( Color ); Default4( 1.0, 1.0, 1.0, 0.5 ); UiGroup( "Fresnel" ); Attribute( "FresnelColor" ); >;
	float FresnelPower < Default( 2.5 ); UiGroup( "Fresnel" ); Attribute( "FresnelPower" ); >;

	float BlurAmount < Default( 4.0 ); UiGroup( "Glass" ); Attribute( "Blur" ); >;

	float4 BgPos < Default4( 0.0, 0.0, 500.0, 100.0 ); Attribute( "BgPos" ); >;
	float4 BgTint < Default4( 1.0, 1.0, 1.0, 1.0 ); Attribute( "BgTint" ); >;

	int BgRepeat <Attribute( "BgRepeat" );>;
	float BgAngle < Default( 0.0 ); Attribute( "BgAngle" ); >;
	
	Texture2D g_tBorderImage < Attribute( "BorderImageTexture" ); Default( 1.0 ); >;
	
	float RefractAmount < Default( 8.0 ); UiGroup( "Refraction" ); Attribute( "Refraction" ); >;
	float BevelWidth < Default( 20.0 ); UiGroup( "Refraction" ); Attribute( "BevelWidth" ); >;
	float BevelCurve < Default( 1.0 ); UiGroup( "Refraction" ); Attribute( "BevelCurve" ); >;
	float BevelSplit < Default( 0.5 ); UiType( Slider ); UiGroup( "Refraction" ); Attribute( "BevelSplit" ); >;
	float4 RefractTint < UiType( Color ); Default4( 1.0, 1.0, 1.0, 0.0 ); UiGroup( "Refraction" ); Attribute( "RefractTint" ); >;

	#if D_TEXTURE_FILTERING == 0
		SamplerState g_sRepeatSampler < Filter( Anisotropic ); AddressU( WRAP ); AddressV( WRAP ); >;
		SamplerState g_sRepeatXSampler < Filter( Anisotropic ); AddressU( WRAP ); AddressV( CLAMP ); >;
		SamplerState g_sRepeatYSampler < Filter( Anisotropic ); AddressU( CLAMP ); AddressV( WRAP ); >;
		SamplerState g_sClampSampler < Filter( Anisotropic ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	#else
		SamplerState g_sRepeatSampler < Filter( Point ); AddressU( WRAP ); AddressV( WRAP ); >;
		SamplerState g_sRepeatXSampler < Filter( Point ); AddressU( WRAP ); AddressV( CLAMP ); >;
		SamplerState g_sRepeatYSampler < Filter( Point ); AddressU( CLAMP ); AddressV( WRAP ); >;
		SamplerState g_sClampSampler < Filter( Point ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	#endif

	#if D_TEXTURE_FILTERING == 2 || D_TEXTURE_FILTERING == 1 // Use trilinear for blur
		SamplerState g_sTrilinearClampSampler < Filter( Trilinear ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	#else
		SamplerState g_sTrilinearClampSampler < Filter( Anisotropic ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	#endif

	BoolAttribute( bWantsFBCopyTexture, true );
	Texture2D g_tFrameBufferCopyTexture < Attribute( "FrameBufferCopyTexture" ); SrgbRead( true ); >;

	Texture2D g_tColor < Attribute( "Texture" ); SrgbRead( false ); >;
	float4 BorderImageTint < Default4( 1.0, 1.0, 1.0, 1.0 ); Attribute( "BorderImageTint" ); >;
	float4 g_vViewport < Source( Viewport ); >;

	RenderState( SrgbWriteEnable0, true );
	RenderState( ColorWriteEnable0, RGBA );
	RenderState( FillMode, SOLID );
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );

	#define SUBPIXEL_AA_MAGIC 0.5

	float GetDistanceFromEdge( float2 pos, float2 size, float4 cornerRadius )
	{
		float minCorner = min(size.x, size.y);
		float4 r = min( cornerRadius * 2.0 , minCorner );
		r.xy = (pos.x>0.0)?r.xy : r.zw;
		r.x  = (pos.y>0.0)?r.x  : r.y;
		float2 q = abs(pos)-(size)+r.x;
		return -0.5 + min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
	}
	
	float2 RotateTexCoord( float2 vTexCoord, float angle, float2 offset = 0.5 )
	{
		float2x2 m = float2x2( cos(angle), -sin(angle), sin(angle), cos(angle) );
		return mul( m, vTexCoord - offset ) + offset;
	}
	
	float2 DistanceNormal( float2 p, float2 c, float4 cornerRadius )
	{
		const float eps = 0.5;
		const float2 h = float2(eps,0);
		return normalize( float3( GetDistanceFromEdge(p-h.xy, c, cornerRadius) - GetDistanceFromEdge(p+h.xy, c, cornerRadius),
								GetDistanceFromEdge(p-h.yx, c, cornerRadius) - GetDistanceFromEdge(p+h.yx, c, cornerRadius),
								2.0*h.x
			) ).xy;
	}

	float4 AlphaBlend( float4 src, float4 dest )
	{
		float4 result;
		result.a = src.a + (1 - src.a) * dest.a;
        if (result.a < 0.0001f) return 0;
		result.rgb = (1 / result.a) * (src.a * src.rgb + (1 - src.a) * dest.a * dest.rgb);
		return result;
	}

	float4 AddBorder( PS_INPUT i, float2 pos, float distanceFromCenter )
	{
		float2 vTransPos = i.vTexCoord.xy * BoxSize;
		float2 fScale = 1.0 / ( 1.0 - ( float2( BorderWidth.z + BorderWidth.x , BorderWidth.y + BorderWidth.w ) / BoxSize) );
		vTransPos = ( vTransPos - ( BoxSize * 0.5 ) ) * ( fScale ) + ( BoxSize * 0.5 );	
		vTransPos += float2( -BorderWidth.x + BorderWidth.z, -BorderWidth.y + BorderWidth.a ) * (fScale * 0.5);
		float2 vOffsetPos = ( BoxSize ) * ( ( vTransPos / BoxSize) * 2.0 - 1.0);
		
		float2 vNormal = DistanceNormal( vOffsetPos, BoxSize, CornerRadius );
		float fDistance = GetDistanceFromEdge( vOffsetPos, BoxSize, CornerRadius );
		fDistance += 1.5;

		float4 vBorderColor;
		float fAntialiasAmount = max( 1.0f / SUBPIXEL_AA_MAGIC, 2.0f / SUBPIXEL_AA_MAGIC * abs( distanceFromCenter / ( min(BoxSize.x, BoxSize.y) ) ) );
		
		float4 vBorderL = BorderColorL;
		float4 vBorderT = BorderColorT;
		float4 vBorderR = BorderColorR;
		float4 vBorderB = BorderColorB;

		vBorderL.a = max( vNormal.x, 0 ) * fDistance / ( BorderWidth.x );
		vBorderT.a = max( vNormal.y, 0 ) * fDistance / ( BorderWidth.y );
		vBorderR.a = max(-vNormal.x, 0 ) * fDistance / ( BorderWidth.z );
		vBorderB.a = max(-vNormal.y, 0 ) * fDistance / ( BorderWidth.w );
		
		float4 c = -100;
		float fBorderAlpha = 0;
		
		if( BorderWidth.x > 0.0f && vBorderL.a > c.a ) { c = vBorderL; fBorderAlpha = BorderColorL.a; }
		if( BorderWidth.y > 0.0f && vBorderT.a > c.a ) { c = vBorderT; fBorderAlpha = BorderColorT.a; }
		if( BorderWidth.z > 0.0f && vBorderR.a > c.a ) { c = vBorderR; fBorderAlpha = BorderColorR.a; }
		if( BorderWidth.a > 0.0f && vBorderB.a > c.a ) { c = vBorderB; fBorderAlpha = BorderColorB.a; }

		vBorderColor = c;
		vBorderColor.a *= fBorderAlpha; // Use the original alpha from the color picker
		
		float finalAlpha = saturate( smoothstep( 0, fAntialiasAmount, fDistance ) );
		vBorderColor.a *= finalAlpha;
		vBorderColor.rgb = SrgbGammaToLinear( vBorderColor.rgb );

		return vBorderColor;
	}

	float4 AddImageBorder( float2 texCoord )
	{
		const float4 BorderImageWidth = BorderWidth; 
		const float2 vBorderImageSize = TextureDimensions2D( g_tBorderImage, 0 );
		const float4 vBorderPixelSize = BorderImageSlice;
		const float4 vBorderPixelRatio = vBorderPixelSize / float4(vBorderImageSize.x,vBorderImageSize.y,vBorderImageSize.x,vBorderImageSize.y);
		const float2 vBoxTexCoord = texCoord * BoxSize;
		
		float2 uv = 0.0;

		if ( !HasBorderImageFill && 
			vBoxTexCoord.x > BorderImageWidth.x && vBoxTexCoord.x < BoxSize.x - BorderWidth.z &&
			vBoxTexCoord.y > BorderImageWidth.y && vBoxTexCoord.y < BoxSize.y - BorderWidth.w )
			return 0;

		if( vBorderPixelSize.x < vBorderImageSize.x * 0.5)
		{
			if ( D_BORDER_IMAGE == 1 )
			{
				float2 vMiddleSize = 1.0 - (vBorderPixelRatio.xy + vBorderPixelRatio.zw);
				float2 vRepeatAmount = floor( ( BoxSize * vMiddleSize ) / BorderImageWidth.xy );
				uv.x = ( vBoxTexCoord.x - BorderImageWidth.x ) / ( BoxSize.x - ( BorderImageWidth.x + BorderImageWidth.z ) ) * vRepeatAmount.x;
				uv.x = fmod( uv.x, vMiddleSize.x ) + vBorderPixelRatio.x;
				uv.y = ( vBoxTexCoord.y - BorderImageWidth.y ) / ( BoxSize.y - ( BorderImageWidth.y + BorderImageWidth.z ) ) * vRepeatAmount.y;
				uv.y = fmod( uv.y, vMiddleSize.y ) + vBorderPixelRatio.y;
			}
			else
			{
				uv.x = ( vBoxTexCoord.x - BorderImageWidth.x ) / ( BoxSize.x - ( BorderImageWidth.x + BorderImageWidth.z ) );
				uv.x = (uv.x * (1.0 - (vBorderPixelRatio.x + vBorderPixelRatio.z))) + vBorderPixelRatio.x;
				uv.y = ( vBoxTexCoord.y - BorderImageWidth.y ) / ( BoxSize.y - ( BorderImageWidth.y + BorderImageWidth.w ) );
				uv.y = (uv.y * (1.0 - (vBorderPixelRatio.y + vBorderPixelRatio.w))) + vBorderPixelRatio.y;
			}
		}
		
		if( vBoxTexCoord.x < BorderImageWidth.x ) uv.x = (vBoxTexCoord.x / BorderImageWidth.x) * vBorderPixelRatio.x; 
		else if( vBoxTexCoord.x > BoxSize.x - BorderWidth.z ) uv.x = ( ( (vBoxTexCoord.x - ( BoxSize.x - BorderWidth.z) ) / BorderImageWidth.z) * vBorderPixelRatio.z ) + ( 1.0 - vBorderPixelRatio.z );
		if( vBoxTexCoord.y < BorderImageWidth.y ) uv.y = (vBoxTexCoord.y / BorderImageWidth.y) * vBorderPixelRatio.y;
		else if( vBoxTexCoord.y > BoxSize.y - BorderWidth.w ) uv.y = ( ( (vBoxTexCoord.y - ( BoxSize.y - BorderWidth.w) ) / BorderImageWidth.w) * vBorderPixelRatio.w ) + ( 1.0 - vBorderPixelRatio.w );

		float4 r = g_tBorderImage.Sample( g_sClampSampler, uv );
		r.xyz = SrgbGammaToLinear( r.xyz );
		return r;
	}

	float4 DoRadialBlur( float2 startUV, float blurAmount ) 
	{
		const float Pi = 6.28318530718;
		const float Directions = 16.0;
		const float Quality = 4.0;
		
		float2 texelSize = 1.0f / g_vViewport.zw;
		float2 blurSize = texelSize * blurAmount;

		float4 color = g_tFrameBufferCopyTexture.Sample( g_sTrilinearClampSampler, startUV );
		
		[unroll]
		for( float d = 0.0; d < Pi; d += Pi / Directions)
		{
			[unroll]
			for(float j = 1.0 / Quality; j <= 1.0; j += 1.0 / Quality)
			{
				color += g_tFrameBufferCopyTexture.Sample( g_sTrilinearClampSampler, startUV + float2( cos(d), sin(d) ) * blurSize * j );	
			}
		}
		
		color /= (Directions * Quality) + 1.0;
		return color;
	}

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;
		float4 bgTint = BgTint.rgba;
		bgTint.rgb = SrgbGammaToLinear(bgTint.rgb);

		float2 pos = ( BoxSize ) * (i.vTexCoord.xy * 2.0 - 1.0);  
		float dist = GetDistanceFromEdge( pos, BoxSize, CornerRadius );
		float2 screenUV = i.vPositionPanelSpace.xy / g_vViewport.zw;
		float2 texelSize = 1.0f / g_vViewport.zw;
		
		float4 vBox; 

		float2 uvOffset = 0.0;
		float bevelFalloff = 0.0;
		float fresnelTerm = 0.0;
		float totalDisplacementFactor = 0.0;
		float2 refractNormal = 0;

		if ( D_REFRACTION_EFFECT == 1 || D_FRESNEL_EFFECT == 1 || D_EDGE_REFLECTION == 1 )
		{
			refractNormal = DistanceNormal( pos, BoxSize, CornerRadius );
			float distFromEdge = -dist - 0.5;
			bevelFalloff = 1.0 - saturate( distFromEdge / max(BevelWidth, 0.001f) );

			if (D_FRESNEL_EFFECT == 1)
			{
				fresnelTerm = pow(bevelFalloff, FresnelPower);
			}

			if (D_REFRACTION_EFFECT == 1)
			{
				float curvedFalloff = pow(bevelFalloff, BevelCurve);
				float innerBevel = smoothstep(0.0, BevelSplit, curvedFalloff);
				float outerBevel = smoothstep(BevelSplit, 1.0, curvedFalloff);
				float innerBevelStrength = innerBevel * (1.0 - outerBevel);
				totalDisplacementFactor = outerBevel - innerBevelStrength;
				uvOffset = refractNormal * totalDisplacementFactor * RefractAmount * texelSize;
			}
		}

		float2 finalSampleUV = screenUV + uvOffset;

		if (D_BLUR_EFFECT == 1)
		{
			vBox = (D_BLUR_QUALITY == 1) ? DoRadialBlur(finalSampleUV, BlurAmount) : g_tFrameBufferCopyTexture.SampleLevel( g_sTrilinearClampSampler, finalSampleUV, sqrt(max(BlurAmount, 0.0) / 2.0) );
		}
		else
		{
			vBox = g_tFrameBufferCopyTexture.Sample( g_sTrilinearClampSampler, finalSampleUV );
		}

		if (D_REFRACTION_EFFECT == 1 && ChromaticAberrationAmount > 0.0)
		{
			float local_curvedFalloff = pow(bevelFalloff, BevelCurve);
			float local_innerBevel = smoothstep(0.0, BevelSplit, local_curvedFalloff);
			float local_outerBevel = smoothstep(BevelSplit, 1.0, local_curvedFalloff);
			float local_innerBevelStrength = local_innerBevel * (1.0 - local_outerBevel);
			float local_totalDisplacementFactor = local_outerBevel - local_innerBevelStrength;

			float refractionStrengthFactor = saturate(abs(local_totalDisplacementFactor));

			if (refractionStrengthFactor > 0.001)
			{
				float3 ddx_color = ddx_fine(vBox.rgb); 
				float3 ca_factors = float3(1.0, 0.0, -1.0) * ChromaticAberrationAmount;
				
				float3 ca_shift = ddx_color * ca_factors;
				
				vBox.rgb += ca_shift * refractionStrengthFactor;
				
				vBox.rgb = saturate(vBox.rgb);
			}
		}

		float4 tint = RefractTint;
		tint.rgb = SrgbGammaToLinear(tint.rgb);
		vBox.rgb *= tint.rgb;

		if (D_EDGE_REFLECTION == 1)
		{
			float reflectionIntensity = pow(bevelFalloff, BorderReflectFresnelPower);
			float currentBevelDepthFactor = 1.0f - bevelFalloff; 
			const float minReflectionOffsetScale = 0.1f; 
			float reflectionDistanceScale = lerp(minReflectionOffsetScale, 1.0f, currentBevelDepthFactor);
			float dynamicReflectionSampleDistance = BorderReflectAmount * reflectionDistanceScale;
			float2 reflectionUV = screenUV - refractNormal * dynamicReflectionSampleDistance * texelSize;
			float4 reflectionSample = g_tFrameBufferCopyTexture.Sample( g_sTrilinearClampSampler, reflectionUV );
			float3 reflectionColor = SrgbGammaToLinear(BorderReflectTint.rgb) * reflectionSample.rgb;
			vBox.rgb = saturate( vBox.rgb + reflectionColor * reflectionIntensity * BorderReflectTint.a );

			float extremeEdgeFactor = pow(bevelFalloff, ExtremeEdgePower); 

			if (extremeEdgeFactor > 0.005) 
			{
			    float2 extremeReflectionUV = screenUV - refractNormal * ExtremeEdgeSampleDistance * texelSize;

			    float4 extremeReflectionSampleColor = g_tFrameBufferCopyTexture.Sample( g_sTrilinearClampSampler, extremeReflectionUV );
			    
				float3 extremeReflectionTintedColor = SrgbGammaToLinear(BorderReflectTint.rgb) * extremeReflectionSampleColor.rgb;

			    vBox.rgb = saturate(vBox.rgb + extremeReflectionTintedColor * extremeEdgeFactor * BorderReflectTint.a * ExtremeEdgeIntensityScale);
			}
		}

		if (D_FRESNEL_EFFECT == 1)
		{
			float3 fresnelLight = SrgbGammaToLinear(FresnelColor.rgb) * FresnelColor.a;
			vBox.rgb = saturate(vBox.rgb + fresnelLight * fresnelTerm);
		}
		
		vBox = lerp(vBox, i.vColor.rgba, i.vColor.a);
		
		float4 vBoxBorder;
		UI_CommonProcessing_Pre( i );
		if ( D_BORDER_IMAGE )
		{
			vBoxBorder = AddImageBorder( i.vTexCoord.xy ) * i.vColor.a;
		}
		else if ( HasBorder )
		{
			vBoxBorder = AddBorder( i, pos, dist );
		}
		else
		{
			vBoxBorder = 0;
		}
		
		if ( D_BACKGROUND_IMAGE == 1 )
		{
			float2 bgSize = BgPos.zw;
			float2 vOffset = BgPos.xy / bgSize;
			float2 vUV = -vOffset + ( ( i.vTexCoord.xy ) * ( BoxSize / bgSize ) );
			vUV = RotateTexCoord( vUV, BgAngle );
			float4 vImage;
			float mipBias = -1.5; 
			if ( BgRepeat == 0 ) vImage = g_tColor.SampleBias( g_sRepeatSampler, vUV, mipBias );
			else if ( BgRepeat == 1 ) vImage = g_tColor.SampleBias( g_sRepeatXSampler, vUV, mipBias );
			else if ( BgRepeat == 2 ) vImage = g_tColor.SampleBias( g_sRepeatYSampler, vUV, mipBias );
			else vImage = g_tColor.SampleBias( g_sClampSampler, vUV, mipBias );
			if ( BgRepeat != 0 && BgRepeat != 4 )
			{
				if ( BgRepeat != 1 && (vUV.x < 0 || vUV.x > 1) ) vImage = 0;
				if ( BgRepeat != 2 && (vUV.y < 0 || vUV.y > 1) ) vImage = 0;
			}
			vImage.xyz = SrgbGammaToLinear( vImage.xyz );
			vImage *= bgTint;
			vBox = AlphaBlend( vImage, vBox );
		}
		
		o.vColor = vBox;
		if ( D_BORDER_IMAGE == 1 || HasBorder == 1 )
		{
			o.vColor = AlphaBlend( vBoxBorder, o.vColor );
		}
		o.vColor.a *= saturate( -dist - 0.5 );
		
		return UI_CommonProcessing_Post( i, o );
	}
}