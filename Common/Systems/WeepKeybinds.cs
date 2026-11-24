using Terraria.ModLoader;

namespace WeepForTheDeparted.Common.Systems
{
	public class WeepKeybinds : ModSystem
	{
		public static ModKeybind UltimateKeybind { get; private set; }
		public static ModKeybind DomainKeybind { get; private set; }
		public static ModKeybind LedgerKeybind { get; private set; }

		public override void Load()
		{
			UltimateKeybind = KeybindLoader.RegisterKeybind(Mod, "Ultimate: Slashed Dream Cries in Red", "V");
			DomainKeybind = KeybindLoader.RegisterKeybind(Mod, "Domain: Nihility", "B");
			LedgerKeybind = KeybindLoader.RegisterKeybind(Mod, "Nihility Ledger: Charms & Eidolons", "C");
		}

		public override void Unload()
		{
			UltimateKeybind = null;
			DomainKeybind = null;
			LedgerKeybind = null;
		}
	}
}
