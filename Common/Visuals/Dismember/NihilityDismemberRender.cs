using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WeepForTheDeparted.Common.Visuals.Dismember
{
	/// <summary>
	/// InnoVault RenderHandle for capturing sliced enemy appearance into snapshot RenderTargets
	/// before vanilla NPC drawing begins.
	/// </summary>
	public sealed class NihilityDismemberRender : RenderHandle
	{
		private const int MaxCapturesPerFrame = 24;
		private static readonly List<int> pruneScratch = new List<int>();

		public override void UpdateBySystem(int index)
		{
			if (Main.gameMenu && NihilityDismember.SnapRTs.Count > 0)
			{
				NihilityDismember.DisposeAllSnapshots();
			}
		}

		public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
		{
			if (Main.gameMenu)
				return;

			PruneOrphanRTs();

			if (!AnyPendingCapture())
				return;

			if (screenSwap == null || screenSwap.IsDisposed)
				return;

			if (Main.screenTarget == null || Main.screenTarget.IsDisposed)
				return;

			RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

			// Save screenTarget to screenSwap before capturing
			graphicsDevice.SetRenderTarget(screenSwap);
			graphicsDevice.Clear(Color.Transparent);
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
			spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
			spriteBatch.End();

			int captured = 0;
			foreach (DismemberEntry entry in NihilityDismember.Entries)
			{
				if (entry.Captured || entry.SnapWidth <= 0)
					continue;

				NPC npc = Main.npc[entry.NpcIndex];
				if (!npc.active || npc.type != entry.NpcType)
					continue;

				CaptureSnapshot(spriteBatch, graphicsDevice, entry, npc);
				if (++captured >= MaxCapturesPerFrame)
					break;
			}

			// Restore screenTarget
			graphicsDevice.SetRenderTarget(Main.screenTarget);
			graphicsDevice.Clear(Color.Transparent);
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
			spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
			spriteBatch.End();

			if (previousTargets != null && previousTargets.Length > 0 && previousTargets[0].RenderTarget != Main.screenTarget)
			{
				graphicsDevice.SetRenderTargets(previousTargets);
			}
		}

		private static void CaptureSnapshot(SpriteBatch sb, GraphicsDevice gd, DismemberEntry entry, NPC npc)
		{
			RenderTarget2D rt = NihilityDismember.EnsureSnapshotRT(gd, entry);
			if (rt == null)
			{
				entry.SnapWidth = 0;
				entry.SnapHeight = 0;
				return;
			}

			Vector2 center = entry.VisualOnly ? npc.Center : entry.AnchorCenter;
			Vector2 fakeScreenPos = center - new Vector2(rt.Width, rt.Height) * 0.5f;
			Vector2 realScreenPos = Main.screenPosition;
			bool batchBegan = false;
			bool drawSucceeded = false;
			bool batchEnded = false;

			try
			{
				gd.SetRenderTarget(rt);
				gd.Clear(Color.Transparent);
				Main.screenPosition = fakeScreenPos;
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
				batchBegan = true;
				Main.instance.DrawNPCDirect(sb, npc, entry.BehindTiles, fakeScreenPos);
				drawSucceeded = true;
			}
			catch
			{
				drawSucceeded = false;
			}
			finally
			{
				Main.screenPosition = realScreenPos;
				if (batchBegan)
				{
					try
					{
						sb.End();
						batchEnded = true;
					}
					catch
					{
						batchEnded = false;
					}
				}
			}

			if (drawSucceeded && batchEnded)
			{
				entry.Captured = true;
				entry.CaptureFailures = 0;
			}
			else
			{
				entry.CaptureFailures++;
				if (entry.CaptureFailures >= 2)
				{
					entry.SnapWidth = 0;
					entry.SnapHeight = 0;
					if (NihilityDismember.SnapRTs.Remove(entry.NpcIndex, out RenderTarget2D failedRT))
					{
						failedRT?.SafeDispose();
					}
				}
			}
		}

		private static bool AnyPendingCapture()
		{
			foreach (DismemberEntry entry in NihilityDismember.Entries)
			{
				if (!entry.Captured && entry.SnapWidth > 0)
					return true;
			}
			return false;
		}

		private static void PruneOrphanRTs()
		{
			if (NihilityDismember.SnapRTs.Count == 0)
				return;

			pruneScratch.Clear();
			foreach (int npcIndex in NihilityDismember.SnapRTs.Keys)
			{
				if (NihilityDismember.GetEntry(npcIndex) == null)
				{
					pruneScratch.Add(npcIndex);
				}
			}
			foreach (int npcIndex in pruneScratch)
			{
				if (NihilityDismember.SnapRTs.Remove(npcIndex, out RenderTarget2D rt))
				{
					rt?.SafeDispose();
				}
			}
		}
	}
}
