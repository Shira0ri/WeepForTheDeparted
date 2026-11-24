using Terraria.Audio;

namespace WeepForTheDeparted.Common
{
	public static class WeepSound
	{
		public static readonly SoundStyle KatanaA = new SoundStyle("WeepForTheDeparted/Assets/Sounds/KatanaA");
		public static readonly SoundStyle KatanaB = new SoundStyle("WeepForTheDeparted/Assets/Sounds/KatanaB");
		public static readonly SoundStyle KatanaSwing = new SoundStyle("WeepForTheDeparted/Assets/Sounds/KatanaSwing");
		public static readonly SoundStyle KatanaHit = new SoundStyle("WeepForTheDeparted/Assets/Sounds/KatanaHit")
		{
			Volume = 0.58f,
			PitchVariance = 0.12f,
			MaxInstances = 4
		};
		public static readonly SoundStyle KatanaHitB = new SoundStyle("WeepForTheDeparted/Assets/Sounds/KatanaHitB")
		{
			Volume = 0.70f,
			PitchVariance = 0.08f,
			MaxInstances = 2
		};
		public static readonly SoundStyle KatanaSprint = new SoundStyle("WeepForTheDeparted/Assets/Sounds/KatanaSprint");

		// Acheron (Honkai: Star Rail) Ultimate Slash Sounds: Heavy sub-bass impact, crisp metallic razor bite & dimensional chime
		public static readonly SoundStyle AcheronWindup = new SoundStyle("WeepForTheDeparted/Assets/Sounds/AcheronWindup")
		{
			Volume = 0.95f,
			MaxInstances = 3
		};
		public static readonly SoundStyle AcheronSlash1 = new SoundStyle("WeepForTheDeparted/Assets/Sounds/AcheronSlash1")
		{
			Volume = 1.05f,
			PitchVariance = 0.04f,
			MaxInstances = 3
		};
		public static readonly SoundStyle AcheronSlash2 = new SoundStyle("WeepForTheDeparted/Assets/Sounds/AcheronSlash2")
		{
			Volume = 1.05f,
			PitchVariance = 0.04f,
			MaxInstances = 3
		};
		public static readonly SoundStyle AcheronSlash3 = new SoundStyle("WeepForTheDeparted/Assets/Sounds/AcheronSlash3")
		{
			Volume = 1.15f,
			PitchVariance = 0.03f,
			MaxInstances = 3
		};
		public static readonly SoundStyle AcheronResurge = new SoundStyle("WeepForTheDeparted/Assets/Sounds/AcheronResurge")
		{
			Volume = 1.30f,
			MaxInstances = 2
		};
	}
}
