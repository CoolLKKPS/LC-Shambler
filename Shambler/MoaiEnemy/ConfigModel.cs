using System;
using BepInEx.Configuration;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;

namespace MoaiEnemy
{
	internal class ConfigModel
	{
		public static void setupConfig()
		{
			FloatSliderConfigItem sizeSlider = new FloatSliderConfigItem(ConfigModel.moaiGlobalSize, new FloatSliderOptions
			{
				Min = 0.05f,
				Max = 5f
			});
			FloatSliderConfigItem voluimeSlider = new FloatSliderConfigItem(ConfigModel.moaiGlobalMusicVol, new FloatSliderOptions
			{
				Min = 0f,
				Max = 2f
			});
			FloatSliderConfigItem raritySlider = new FloatSliderConfigItem(ConfigModel.moaiGlobalRarity, new FloatSliderOptions
			{
				Min = 0f,
				Max = 10f
			});
			FloatSliderConfigItem speedSlider = new FloatSliderConfigItem(ConfigModel.moaiGlobalSpeed, new FloatSliderOptions
			{
				Min = 0f,
				Max = 5f
			});
		}

		public static ConfigEntry<float> moaiGlobalSize = null!;

		public static ConfigEntry<float> moaiGlobalMusicVol = null!;

		public static ConfigEntry<float> moaiGlobalRarity = null!;

		public static ConfigEntry<float> moaiGlobalSpeed = null!;
	}
}
