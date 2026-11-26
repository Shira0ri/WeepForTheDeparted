using System;
using InnoVault;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Content.Items.Weapons;

namespace WeepForTheDeparted.Common.UI
{
	public class SlashedDreamHud : UIHandle
	{
		private static SlashedDreamHud instance;
		public static SlashedDreamHud Instance => instance ??= UIHandleLoader.GetUIHandleOfType<SlashedDreamHud>();

		public float Appear => OpenProgress.Current;
		public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Mouse_Text;

		public override bool Active
		{
			get
			{
				if (Main.gameMenu || Main.dedServ)
					return false;

				Player player = Main.LocalPlayer;
				if (player == null || !player.active || player.dead)
					return false;

				bool holdingNaught = player.HeldItem?.type == ModContent.ItemType<Naught>();
				return holdingNaught || IsOpen || OpenProgress.Current > 0.005f;
			}
		}

		public static float UIScreenW => (float)Main.screenWidth / Main.UIScale;
		public static float UIScreenH => (float)Main.screenHeight / Main.UIScale;

		public static Vector2 NaturalAnchor
		{
			get
			{
				float screenH = UIScreenH;
				// Anchored at bottom-left in UI coordinates matching Calamity Overhaul
				return new Vector2(90f, screenH - 95f);
			}
		}

		public override void SetStaticDefaults()
		{
			instance = this;
		}

		public override void LogicUpdate()
		{
			UpdateState();
		}

		public override void Update()
		{
			UpdateState();
		}

		public void UpdateState()
		{
			CalamityOverhaulHudIntegration.EnsureRegistered();

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead || Main.gameMenu)
			{
				if (IsOpen)
					Close();
				return;
			}

			// Active when holding Naught
			bool holdingNaught = player.HeldItem?.type == ModContent.ItemType<Naught>();
			bool ledgerBlocking = NihilityLedgerUI.Instance != null
				&& (NihilityLedgerUI.Instance.IsOpen || NihilityLedgerUI.Instance.OpenProgress.Current > 0.01f);

			if (holdingNaught)
			{
				if (!IsOpen)
				{
					Open();
					if (OpenProgress.Current < 0.15f)
					{
						OpenProgress.Current = 0.15f;
					}
				}

				// Clicking on the HUD opens the Nihility Ledger (only when Ledger is not open)
				if (!ledgerBlocking)
				{
					Vector2 mouse = new Vector2(Main.mouseX, Main.mouseY);
					if (Vector2.Distance(mouse, NaturalAnchor) <= 45f && Main.mouseLeft && Main.mouseLeftRelease)
					{
						if (NihilityLedgerUI.Instance != null)
						{
							if (NihilityLedgerUI.Instance.IsOpen)
								NihilityLedgerUI.Instance.Close();
							else
								NihilityLedgerUI.Instance.Open();
							Main.mouseLeftRelease = false;
						}
					}
				}
			}
			else
			{
				if (IsOpen)
					Close();
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);
			if (alpha <= 0.005f && !IsOpen)
				return;

			if (IsOpen && alpha < 0.15f)
				alpha = 0.15f;

			float ledgerOcclusion = NihilityLedgerUI.Instance != null
				? MathHelper.Clamp(NihilityLedgerUI.Instance.IsOpen ? Math.Max(0.25f, NihilityLedgerUI.Instance.OpenProgress.Current) : NihilityLedgerUI.Instance.OpenProgress.Current, 0f, 1f)
				: 0f;
			alpha *= 1f - ledgerOcclusion;
			if (alpha <= 0.005f)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			var naughtPlayer = player.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer == null)
				return;

			Vector2 anchor = NaturalAnchor;
			bool isPrimed = naughtPlayer.SlashedDream >= NaughtPlayer.MaxSlashedDream;
			int recastCooldown = naughtPlayer.UltimateRecastCooldown;
			bool onRecastCooldown = recastCooldown > 0;
			bool isReady = isPrimed && !onRecastCooldown;
			float pulseTimer = GlobalTimer * 3.5f;

			// Request 9-petal individual flower components
			Texture2D texLit = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamPetalLit", AssetRequestMode.ImmediateLoad).Value;
			Texture2D texUnlit = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamPetalUnlit", AssetRequestMode.ImmediateLoad).Value;
			Texture2D texCenter = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamFlowerCenter", AssetRequestMode.ImmediateLoad).Value;
			Texture2D texAwakened = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamFlowerAwakened", AssetRequestMode.ImmediateLoad).Value;

			if (texLit == null || texUnlit == null)
				return;

			Vector2 origin = new Vector2(128f, 128f);
			float baseScale = 0.36f;

			// Overall flower pulse when primed and ready (suppressed during post-Ultimate recast gate)
			float flowerPulse = isReady ? (float)Math.Sin(pulseTimer * 4f) * 0.04f : 0f;
			float scale = (baseScale + flowerPulse) * MathHelper.Lerp(0.85f, 1f, alpha);

			// 1. Draw all 9 Petals (clockwise starting from top petal at 12 o'clock)
			for (int i = 0; i < 9; i++)
			{
				float rot = MathHelper.ToRadians(i * 40f);
				bool isLit = i < naughtPlayer.SlashedDream;

				if (isLit)
				{
					// Lit Petal: energetic pulse wave across all petals, with prominent flare on final 4 crimson petals (5-8)
					float t = i / 8f;
					Color baseColor = Color.Lerp(new Color(215, 115, 255), new Color(255, 30, 45), t);

					// Dynamic traveling wave moving clockwise
					float pulseSpeed = isReady ? 5.5f : 4.0f;
					float pulsePhase = pulseTimer * pulseSpeed - i * 0.65f;

					// Pronounced pulse amplitude
					float amp = i >= 5 ? 0.12f : 0.10f;
					float petalPulse = (float)Math.Sin(pulsePhase) * amp + 1.0f;

					// Outward radial surge motion: physically expands outward and contracts
					float outwardSurge = (float)Math.Sin(pulsePhase) * (i >= 5 ? 4.5f : 3.5f);
					Vector2 dir = new Vector2((float)Math.Sin(rot), -(float)Math.Cos(rot));
					Vector2 petalDrawPos = anchor + dir * outwardSurge;

					// Dynamic luminance & brightness surge
					float brightness = (float)Math.Sin(pulsePhase) * 0.25f + 1.0f;
					Color petalColor = baseColor.MultiplyRGB(new Color(brightness, brightness, brightness));

					// Extra fiery highlight surge for the final 4 crimson petals
					if (i >= 5)
					{
						float flare = Math.Max(0f, (float)Math.Sin(pulsePhase));
						petalColor = Color.Lerp(petalColor, new Color(255, 140, 160), flare * 0.5f);
					}

					if (isReady)
					{
						float primeFlash = (float)Math.Sin(pulseTimer * 6f + i * 0.7f) * 0.35f + 0.35f;
						petalColor = Color.Lerp(petalColor, Color.White, primeFlash);
					}
					else if (onRecastCooldown)
					{
						petalColor = Color.Lerp(petalColor, new Color(65, 55, 75), 0.45f);
					}

					// Draw petal
					spriteBatch.Draw(texLit, petalDrawPos, null, petalColor * alpha, rot, origin, scale * petalPulse, SpriteEffects.None, 0f);
				}
				else
				{
					// Unlit Petal: crisp white/silver slot with gentle breathing pulse
					float unlitPulse = (float)Math.Sin(pulseTimer * 2f + i * 0.35f) * 0.02f + 0.98f;
					Color unlitColor = Color.White * (alpha * (onRecastCooldown ? 0.58f : 0.78f));
					spriteBatch.Draw(texUnlit, anchor, null, unlitColor, rot, origin, scale * unlitPulse, SpriteEffects.None, 0f);
				}
			}

			// 2. Draw Central Core Knot in the middle
			if (texCenter != null)
			{
				float corePulse = (float)Math.Sin(pulseTimer * 3f) * 0.03f + 0.98f;
				Color coreColor = isReady
					? Color.Lerp(Color.White, new Color(255, 100, 130), (float)Math.Sin(pulseTimer * 5f) * 0.35f + 0.35f)
					: (onRecastCooldown ? new Color(165, 150, 175) : Color.White);

				spriteBatch.Draw(texCenter, anchor, null, coreColor * alpha, 0f, origin, scale * corePulse, SpriteEffects.None, 0f);
			}

			// 3. Draw Awakened Lightning Ring when Primed & Ready (dimmed during recast cooldown)
			if (isPrimed && texAwakened != null)
			{
				if (isReady)
				{
					float awkPulse = (float)Math.Sin(pulseTimer * 5f) * 0.25f + 0.75f;
					spriteBatch.Draw(texAwakened, anchor, null, Color.White * (alpha * awkPulse), 0f, origin, scale * 1.05f, SpriteEffects.None, 0f);
				}
				else
				{
					spriteBatch.Draw(texAwakened, anchor, null, new Color(145, 125, 160) * (alpha * 0.22f), 0f, origin, scale * 1.02f, SpriteEffects.None, 0f);
				}
			}

			// 3b. Draw subtle post-Ultimate recast cooldown timer centered on the flower core
			if (onRecastCooldown)
			{
				DrawRecastCooldownTimer(spriteBatch, anchor, alpha, recastCooldown);
			}

			// 4. Draw Overflow Number (if overflow charges exist)
			if (naughtPlayer.OverflowDream > 0)
			{
				DrawOverflowNumber(spriteBatch, anchor, alpha, naughtPlayer.OverflowDream, pulseTimer);
			}

			// 5. Draw Status & Text
			DrawStatusText(spriteBatch, anchor, alpha, naughtPlayer, isPrimed, isReady, onRecastCooldown, pulseTimer);

			// 6. Handle Hover & Tooltip (suppressed while Nihility Ledger is open)
			if (ledgerOcclusion <= 0.01f)
			{
				HandleHoverTooltip(anchor, naughtPlayer, isPrimed, recastCooldown);
			}
		}

		private static void DrawRecastCooldownTimer(SpriteBatch spriteBatch, Vector2 anchor, float alpha, int recastCooldownTicks)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (font == null)
				return;

			string cdStr = $"{recastCooldownTicks / 60f:0.0}s";
			Vector2 textSize = font.MeasureString(cdStr) * 0.85f;
			Vector2 textPos = anchor - textSize * 0.5f + new Vector2(0f, 1f);
			Color cdColor = new Color(225, 200, 235);
			Utils.DrawBorderStringFourWay(spriteBatch, font, cdStr, textPos.X, textPos.Y, cdColor * alpha, Color.Black * (alpha * 0.95f), Vector2.Zero, 0.85f);
		}

		private void DrawOverflowNumber(SpriteBatch spriteBatch, Vector2 anchor, float alpha, int overflowCount, float pulseTimer)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (font == null)
				return;

			string overStr = $"+{overflowCount}";
			Vector2 textSize = font.MeasureString(overStr) * 0.85f;
			Vector2 textPos = anchor + new Vector2(26f, -28f) - textSize * 0.5f;

			Color amberGold = Color.Lerp(new Color(255, 195, 60), new Color(255, 240, 150), (float)Math.Sin(pulseTimer * 4f) * 0.35f + 0.35f);
			Utils.DrawBorderStringFourWay(spriteBatch, font, overStr, textPos.X, textPos.Y, amberGold * alpha, Color.Black * alpha, Vector2.Zero, 0.85f);
		}

		private void DrawStatusText(SpriteBatch spriteBatch, Vector2 anchor, float alpha, NaughtPlayer naughtPlayer, bool isPrimed, bool isReady, bool onRecastCooldown, float pulseTimer)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (font == null)
				return;

			string countStr = $"{naughtPlayer.SlashedDream} / {NaughtPlayer.MaxSlashedDream}";
			if (naughtPlayer.OverflowDream > 0)
			{
				countStr += $" (+{naughtPlayer.OverflowDream})";
			}

			Vector2 countSize = font.MeasureString(countStr) * 0.82f;
			Vector2 countPos = anchor + new Vector2(0f, 52f) - countSize * 0.5f;

			Color textColor = isReady
				? Color.Lerp(new Color(255, 60, 90), Color.White, (float)Math.Sin(pulseTimer * 5f) * 0.35f + 0.35f)
				: (isPrimed && onRecastCooldown ? new Color(175, 130, 165) : new Color(215, 160, 255));

			Utils.DrawBorderStringFourWay(spriteBatch, font, countStr, countPos.X, countPos.Y, textColor * alpha, Color.Black * (alpha * 0.9f), Vector2.Zero, 0.82f);

			if (naughtPlayer.IsEmancipatorCritActive)
			{
				int critBonus = (int)MathF.Round(WeepBalance.Active.EmancipatorCritBonus);
				string critStr = $"+{critBonus}% CRIT {naughtPlayer.EmancipatorCritBuffTimer / 60f:0.0}s";
				Vector2 critSize = font.MeasureString(critStr) * 0.72f;
				Vector2 critPos = anchor + new Vector2(0f, 70f) - critSize * 0.5f;
				Color critColor = Color.Lerp(new Color(255, 70, 95), new Color(255, 210, 100), (float)Math.Sin(pulseTimer * 6f) * 0.5f + 0.5f);
				Utils.DrawBorderStringFourWay(spriteBatch, font, critStr, critPos.X, critPos.Y, critColor * alpha, Color.Black * (alpha * 0.9f), Vector2.Zero, 0.72f);
			}
		}

		private void HandleHoverTooltip(Vector2 anchor, NaughtPlayer naughtPlayer, bool isPrimed, int recastCooldownTicks)
		{
			Vector2 mouse = new Vector2(Main.mouseX, Main.mouseY);
			Rectangle bounds = new Rectangle((int)anchor.X - 50, (int)anchor.Y - 50, 100, 100);

			if (bounds.Contains(mouse.ToPoint()))
			{
				Main.LocalPlayer.mouseInterface = true;
				bool onRecastCooldown = recastCooldownTicks > 0;
				float recastSec = recastCooldownTicks / 60f;

				string tooltip;
				if (isPrimed && onRecastCooldown)
				{
					tooltip = $"Slashed Dream: {naughtPlayer.SlashedDream}/9 [RECAST COOLDOWN: {recastSec:0.0}s]\nOverflow Charges: {naughtPlayer.OverflowDream}/{naughtPlayer.CurrentMaxOverflowDream}\nUltimate on post-cast cooldown ({recastSec:0.0}s remaining).\nInflict debuffs or cycle clones to charge.\n[Click to open Nihility Ledger (Charms & Eidolons)]";
				}
				else if (isPrimed)
				{
					tooltip = $"Slashed Dream: {naughtPlayer.SlashedDream}/9 [PRIMED]\nOverflow Charges: {naughtPlayer.OverflowDream}/{naughtPlayer.CurrentMaxOverflowDream}\nReady to unleash: Slashed Dream Cries in Red\nInflict debuffs or cycle clones to charge.\n[Click to open Nihility Ledger (Charms & Eidolons)]";
				}
				else
				{
					tooltip = $"Slashed Dream: {naughtPlayer.SlashedDream}/9\nOverflow Charges: {naughtPlayer.OverflowDream}/{naughtPlayer.CurrentMaxOverflowDream}\nReaches 9 to prime Ultimate.\nInflict debuffs or cycle clones to charge.\n[Click to open Nihility Ledger (Charms & Eidolons)]";
					if (onRecastCooldown)
					{
						tooltip += $"\nUltimate Recast Cooldown: {recastSec:0.0}s";
					}
				}

				if (naughtPlayer.IsEmancipatorCritActive)
				{
					int critBonus = (int)MathF.Round(WeepBalance.Active.EmancipatorCritBonus);
					tooltip += $"\nSevered Will: +{critBonus}% True Melee Crit ({naughtPlayer.EmancipatorCritBuffTimer / 60f:0.0}s)";
				}

				Main.instance.MouseText(tooltip);
			}
		}
	}
}
