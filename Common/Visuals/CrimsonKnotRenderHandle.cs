using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using WeepForTheDeparted.Common.GlobalNPCs;

namespace WeepForTheDeparted.Common.Visuals
{
	/// <summary>
	/// RenderHandle that renders Crimson Knot stack indicators directly after entities.
	/// This completely bypasses NPCLoader.PreDraw and NPCLoader.PostDraw, guaranteeing
	/// that Crimson Knots are rendered above all enemies and bosses, including bosses whose
	/// drawing is overridden or suppressed by other mods (such as Calamity Overhaul's Brutal NPCs).
	/// </summary>
	public class CrimsonKnotRenderHandle : RenderHandle
	{
		public override float Weight => 2.0f; // Render after standard entity passes

		public override void DrawBeforeInfernoRings(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			CrimsonKnotGlobalNPC.DrawAllKnots(spriteBatch, Main.screenPosition);
		}
	}
}
