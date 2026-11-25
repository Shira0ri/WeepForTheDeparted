using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Content.Shaders
{
	public static class ShaderLoader
	{
		private static Effect slashShader;
		private static bool attemptedSlashLoad = false;

		private static Effect flowShader;
		private static bool attemptedFlowLoad = false;

		private static Effect blackHoleSkyShader;
		private static bool attemptedBlackHoleSkyLoad = false;

		private static Effect worldGradeShader;
		private static bool attemptedWorldGradeLoad = false;

		private static Effect nihilityDomainShader;
		private static bool attemptedNihilityDomainLoad = false;

		private static Effect finaleWoundShader;
		private static bool attemptedFinaleWoundLoad = false;

		private static Effect finaleBladeShader;
		private static bool attemptedFinaleBladeLoad = false;

		private static Effect dismemberShader;
		private static bool attemptedDismemberLoad = false;

		private static Effect flowerBloomShader;
		private static bool attemptedFlowerBloomLoad = false;

		private static Effect sheathFlashShader;
		private static bool attemptedSheathFlashLoad = false;

		private static Effect crimsonKnotShader;
		private static bool attemptedCrimsonKnotLoad = false;

		private static Effect bloodLotusShader;
		private static bool attemptedBloodLotusLoad = false;

		private static Effect bloodBlossomNeedleShader;
		private static bool attemptedBloodBlossomNeedleLoad = false;

		private static Effect voidHorizonShader;
		private static bool attemptedVoidHorizonLoad = false;

		private static Effect syncCrossSlashShader;
		private static bool attemptedSyncCrossSlashLoad = false;

		private static Effect thunderCoreShader;
		private static bool attemptedThunderCoreLoad = false;

		private static Effect witheredRiverShader;
		private static bool attemptedWitheredRiverLoad = false;
		private static Effect ultQuadShader;
		private static bool attemptedUltQuadLoad = false;

		private static Effect LoadShader(string relativeFxcPath)
		{
			if (Main.dedServ)
				return null;

			try
			{
				if (ModContent.FileExists(relativeFxcPath))
				{
					byte[] bytes = ModContent.GetFileBytes(relativeFxcPath);
					return new Effect(Main.graphics.GraphicsDevice, bytes);
				}
			}
			catch (Exception ex)
			{
				ModContent.GetInstance<WeepForTheDeparted>()?.Logger.Warn($"Failed to load shader {relativeFxcPath}: {ex.Message}");
			}
			return null;
		}

		public static Effect SlashShader
		{
			get
			{
				if (!attemptedSlashLoad && !Main.dedServ)
				{
					attemptedSlashLoad = true;
					slashShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniCrimsonSlash.fxc");
				}
				return slashShader;
			}
		}

		public static Effect FlowShader
		{
			get
			{
				if (!attemptedFlowLoad && !Main.dedServ)
				{
					attemptedFlowLoad = true;
					flowShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniKamuiFlow.fxc");
				}
				return flowShader;
			}
		}

		public static Effect BlackHoleSkyShader
		{
			get
			{
				if (!attemptedBlackHoleSkyLoad && !Main.dedServ)
				{
					attemptedBlackHoleSkyLoad = true;
					blackHoleSkyShader = LoadShader("WeepForTheDeparted/Content/Shaders/BlackHoleSky.fxc");
				}
				return blackHoleSkyShader;
			}
		}

		public static Effect NihilityDomainShader
		{
			get
			{
				if (!attemptedNihilityDomainLoad && !Main.dedServ)
				{
					attemptedNihilityDomainLoad = true;
					nihilityDomainShader = LoadShader("WeepForTheDeparted/Content/Shaders/NihilityDomain.fxc");
				}
				return nihilityDomainShader;
			}
		}

		public static Effect WorldGradeShader
		{
			get
			{
				if (!attemptedWorldGradeLoad && !Main.dedServ)
				{
					attemptedWorldGradeLoad = true;
					worldGradeShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniWorldGrade.fxc");
				}
				return worldGradeShader;
			}
		}

		public static Effect FinaleWoundShader
		{
			get
			{
				if (!attemptedFinaleWoundLoad && !Main.dedServ)
				{
					attemptedFinaleWoundLoad = true;
					finaleWoundShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniFinaleWound.fxc");
				}
				return finaleWoundShader;
			}
		}

		public static Effect FinaleBladeShader
		{
			get
			{
				if (!attemptedFinaleBladeLoad && !Main.dedServ)
				{
					attemptedFinaleBladeLoad = true;
					finaleBladeShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniFinaleBlade.fxc");
				}
				return finaleBladeShader;
			}
		}

		public static Effect DismemberShader
		{
			get
			{
				if (!attemptedDismemberLoad && !Main.dedServ)
				{
					attemptedDismemberLoad = true;
					dismemberShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniDismember.fxc");
				}
				return dismemberShader;
			}
		}

		public static Effect FlowerBloomShader
		{
			get
			{
				if (!attemptedFlowerBloomLoad && !Main.dedServ)
				{
					attemptedFlowerBloomLoad = true;
					flowerBloomShader = LoadShader("WeepForTheDeparted/Content/Shaders/FlowerBloom.fxc");
				}
				return flowerBloomShader;
			}
		}

		public static Effect SheathFlashShader
		{
			get
			{
				if (!attemptedSheathFlashLoad && !Main.dedServ)
				{
					attemptedSheathFlashLoad = true;
					sheathFlashShader = LoadShader("WeepForTheDeparted/Content/Shaders/SheathFlash.fxc");
				}
				return sheathFlashShader;
			}
		}

		public static Effect CrimsonKnotShader
		{
			get
			{
				if (!attemptedCrimsonKnotLoad && !Main.dedServ)
				{
					attemptedCrimsonKnotLoad = true;
					crimsonKnotShader = LoadShader("WeepForTheDeparted/Content/Shaders/CrimsonKnot.fxc");
				}
				return crimsonKnotShader;
			}
		}

		public static Effect BloodLotusShader
		{
			get
			{
				if (!attemptedBloodLotusLoad && !Main.dedServ)
				{
					attemptedBloodLotusLoad = true;
					bloodLotusShader = LoadShader("WeepForTheDeparted/Content/Shaders/BloodLotusShockwave.fxc");
				}
				return bloodLotusShader;
			}
		}

		public static Effect BloodBlossomNeedleShader
		{
			get
			{
				if (!attemptedBloodBlossomNeedleLoad && !Main.dedServ)
				{
					attemptedBloodBlossomNeedleLoad = true;
					bloodBlossomNeedleShader = LoadShader("WeepForTheDeparted/Content/Shaders/BloodBlossomNeedle.fxc");
				}
				return bloodBlossomNeedleShader;
			}
		}

		public static Effect VoidHorizonShader
		{
			get
			{
				if (!attemptedVoidHorizonLoad && !Main.dedServ)
				{
					attemptedVoidHorizonLoad = true;
					voidHorizonShader = LoadShader("WeepForTheDeparted/Content/Shaders/VoidHorizonPortal.fxc");
				}
				return voidHorizonShader;
			}
		}

		public static Effect SyncCrossSlashShader
		{
			get
			{
				if (!attemptedSyncCrossSlashLoad && !Main.dedServ)
				{
					attemptedSyncCrossSlashLoad = true;
					syncCrossSlashShader = LoadShader("WeepForTheDeparted/Content/Shaders/OniSyncCrossSlash.fxc");
				}
				return syncCrossSlashShader;
			}
		}

		public static Effect ThunderCoreShader
		{
			get
			{
				if (!attemptedThunderCoreLoad && !Main.dedServ)
				{
					attemptedThunderCoreLoad = true;
					thunderCoreShader = LoadShader("WeepForTheDeparted/Content/Shaders/ThunderCoreLightning.fxc");
				}
				return thunderCoreShader;
			}
		}

		public static Effect WitheredRiverShader
		{
			get
			{
				if (!attemptedWitheredRiverLoad && !Main.dedServ)
				{
					attemptedWitheredRiverLoad = true;
					witheredRiverShader = LoadShader("WeepForTheDeparted/Content/Shaders/WitheredRiver.fxc");
				}
				return witheredRiverShader;
			}
		}

		public static Effect UltQuadShader
		{
			get
			{
				if (!attemptedUltQuadLoad && !Main.dedServ)
				{
					attemptedUltQuadLoad = true;
					ultQuadShader = LoadShader("WeepForTheDeparted/Content/Shaders/UltQuad.fxc");
				}
				return ultQuadShader;
			}
		}
	}
}
