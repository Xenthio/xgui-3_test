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
		attr.Set( "Refraction", 16f );
		attr.Set( "BevelWidth", 30f );
		attr.Set( "BevelCurve", 1f );
		attr.SetCombo( "D_REFRACTION_EFFECT", 1 );
		attr.SetCombo( "D_REFLECTIVE_BORDER", 1 );
		attr.SetCombo( "D_FRESNEL_EFFECT", 1 );
		attr.SetCombo( "D_BACKGROUND_IMAGE", 0 );
		attr.Set( "HasBorder", 1 );
		attr.Set( "BorderSize", new Vector4( 1 ) );

		attr.Set( "BorderColorL", Color.White );
		attr.Set( "BorderColorT", Color.White );
		attr.Set( "BorderColorR", Color.White );
		attr.Set( "BorderColorB", Color.White );

		attr.Set( "BorderReflectAmount", 16f );
		attr.Set( "BorderReflectTint", new Color( 1.4f, 1.4f, 1.4f, 1.0f ) );
		attr.Set( "FresnelColor", new Color( 0.8f, 0.9f, 1.0f, 0.05f ) );

		Graphics.GrabFrameTexture( "FrameBufferCopyTexture", attr );

		Graphics.DrawQuad( rect, mat, Color.Transparent, attr );

		base.DrawBackground( ref state );
	}
}
