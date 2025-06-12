using Sandbox.UI;

namespace Sandbox;

public class GlassPanel : Panel
{
	public override bool HasContent => true;
	public override void DrawContent( ref RenderState state )
	{
		base.DrawContent( ref state );
	}
	public override void DrawBackground( ref RenderState state )
	{

		var mat = Material.FromShader( "shaders/ui.fluidglass.shader" );

		// draw a background with the fluid glass material
		var rect = this.Box.Rect;
		var attr = new RenderAttributes();
		attr.Set( "BoxPosition", new Vector2( rect.Left, rect.Top ) );
		attr.Set( "BoxSize", new Vector2( rect.Width, rect.Height ) );
		attr.Set( "BorderRadius", 32f );

		attr.Set( "Refraction", 24f );
		attr.Set( "BevelWidth", 50f );
		attr.Set( "BevelCurve", 1f );
		attr.Set( "BevelSplit", 0.5f );

		attr.Set( "Blur", 1.8f );
		attr.SetCombo( "D_REFRACTION_EFFECT", 1 );
		attr.SetCombo( "D_EDGE_REFLECTION", 1 );
		attr.SetCombo( "D_FRESNEL_EFFECT", 1 );
		attr.SetCombo( "D_BLUR_EFFECT", 1 );
		attr.SetCombo( "D_BLUR_QUALITY", 1 );
		attr.SetCombo( "D_DOUBLE_BEVEL", 1 );
		attr.SetCombo( "D_BACKGROUND_IMAGE", 0 );
		attr.Set( "HasBorder", 0 );
		attr.Set( "BorderSize", new Vector4( 1 ) );

		attr.Set( "BorderColorL", Color.White );
		attr.Set( "BorderColorT", Color.White );
		attr.Set( "BorderColorR", Color.White );
		attr.Set( "BorderColorB", Color.White );

		attr.Set( "BorderReflectAmount", 32f );
		attr.Set( "BorderReflectFresnelPower", 8f );
		attr.Set( "BorderReflectTint", new Color( 1.0f, 1.0f, 1.0f, 1.0f ) );

		attr.Set( "ExtremeEdgePower", 24f );
		attr.Set( "ExtremeEdgeSampleDistance", 25f );
		attr.Set( "ExtremeEdgeIntensityScale", 0.75f );

		attr.Set( "ChromaticAberrationAmount", 1.2f );

		attr.Set( "FresnelColor", new Color( 0.8f, 0.9f, 1.0f, 0.025f ) );

		attr.Set( "RefractTint", new Color( 1.1f, 1.1f, 1.1f, 1.0f ) );


		//attr.Set( "BorderReflectTint", new Color( 0.8f, 0.8f, 0.8f, 1.0f ) );
		//attr.Set( "RefractTint", new Color( 0.6f, 0.6f, 0.6f, 1.0f ) );

		Graphics.GrabFrameTexture( "FrameBufferCopyTexture", attr );

		Graphics.DrawQuad( rect, mat, Color.Transparent, attr );

		base.DrawBackground( ref state );
	}
}
