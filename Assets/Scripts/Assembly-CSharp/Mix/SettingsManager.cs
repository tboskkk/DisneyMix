using System;
using System.Collections.Generic;
using Disney.MobileNetwork;
using Disney.Native;
using Mix.DeviceDb;
using Prime31;
using UnityEngine;

namespace Mix
{
	public class SettingsManager : Singleton<SettingsManager>
	{
		private IKeyValDatabaseApi databaseApi;

		private Vector2 screenSize = Vector2.zero;

		private Vector2 screenSizeWithSoftKeys = Vector2.zero;

		public List<string> RecentItems { get; set; }

		public event EventHandler OnPushSettingUpdate = delegate
		{
		};

		public SettingsManager()
		{
			RecentItems = new List<string>();
			databaseApi = Singleton<MixDocumentCollections>.Instance.keyValDocumentCollectionApi;
			SetTargetFramerate(GetDefaultFPS());
		}

		public void SetTargetFramerate(int aFrameRate)
		{
			Application.TargetFrameRate = aFrameRate;
		}

		public int GetDefaultFPS()
		{
			return (!databaseApi.LoadUserValueAsBool("settings_battery_life")) ? 60 : 30;
		}

		public int GetScreenWidth()
		{
			if (screenSize.x == 0f && AccessibilityManager.Instance != null)
			{
				screenSize = AccessibilityManager.Instance.GetScreenSize();
			}
			if (screenSize.x == 0f)
			{
				return Display.displays[0].systemWidth;
			}
			return (int)screenSize.x;
		}

		public int GetScreenWidthWithSoftKeys()
		{
			if (screenSizeWithSoftKeys.x == 0f && AccessibilityManager.Instance != null)
			{
				screenSizeWithSoftKeys = AccessibilityManager.Instance.GetScreenSizeWithSoftKeys();
			}
			if (screenSizeWithSoftKeys.x == 0f)
			{
				return Display.displays[0].systemWidth;
			}
			return (int)screenSizeWithSoftKeys.x;
		}

		public int GetScreenHeight()
		{
			if (screenSize.y == 0f && AccessibilityManager.Instance != null)
			{
				screenSize = AccessibilityManager.Instance.GetScreenSize();
			}
			if (screenSize.y == 0f)
			{
				return Display.displays[0].systemHeight;
			}
			return (int)screenSize.y;
		}

		public int GetScreenHeightWithSoftKeys()
		{
			if (screenSizeWithSoftKeys.y == 0f && AccessibilityManager.Instance != null)
			{
				screenSizeWithSoftKeys = AccessibilityManager.Instance.GetScreenSizeWithSoftKeys();
			}
			if (screenSizeWithSoftKeys.y == 0f)
			{
				return Display.displays[0].systemHeight;
			}
			return (int)screenSizeWithSoftKeys.y;
		}

		public float GetHeightScale()
		{
			return MixConstants.CANVAS_HEIGHT / (float)GetScreenHeight();
		}

		public float GetWidthScale()
		{
			return MixConstants.CANVAS_WIDTH / (float)GetScreenWidth();
		}

		public int GetStatusBarHeight()
		{
			return AccessibilityManager.Instance.GetStatusBarHeight();
		}

public string GetDeviceString()
{
#if UNITY_ANDROID
    return "android";
#elif UNITY_ANDROID && UNITY_EDITOR
    return "android";
#elif UNITY_STANDALONE_WIN && UNITY_EDITOR
    return "windows64";
#else
    return "iphone";
#endif
}

		// The asset bundle folder for this build (AssetManager.BundlePlatform).
		// GetDeviceString stays "iphone" on desktop for the code that relies on
		// it, but desktop bundles are built per platform (windows64,
		// standalonelinux64, standalonemacos), so bundle paths use this.
		public string GetBundlePlatform()
		{
			return Mix.Assets.AssetManager.BundlePlatform;
		}

		public bool UseLocalPrefabs()
		{
			return false;
		}

		public bool UseBundleProjectBundles()
		{
			return false;
		}

		public void SetSoundEnabledSetting(bool setting)
		{
			databaseApi.SaveDeviceValueFromBool("settings_play_sounds", setting);
		}

		public void SetBatteryLifeSetting(bool aSetting)
		{
			databaseApi.SaveUserValueFromBool("settings_battery_life", aSetting);
			SetTargetFramerate(GetDefaultFPS());
		}

		public bool GetBatteryLifeSetting()
		{
			return databaseApi.LoadUserValueAsBool("settings_battery_life");
		}

		public void SetPushNotificationsSetting(bool setting)
		{
			databaseApi.SaveUserValueFromBool("settings_notifications_enabled", setting);
			if (this.OnPushSettingUpdate != null)
			{
				this.OnPushSettingUpdate(this, new EventArgs());
			}
		}

		public bool GetPushNotificationsSetting()
		{
			return databaseApi.LoadUserValueAsBool("settings_notifications_enabled", true);
		}

		public void RemovePushNotificationsSetting()
		{
			databaseApi.RemoveUserKey("settings_notifications_enabled");
			if (this.OnPushSettingUpdate != null)
			{
				this.OnPushSettingUpdate(this, new EventArgs());
			}
		}

		public bool GetSoundEnabledSetting()
		{
			return databaseApi.LoadDeviceValueAsBool("settings_play_sounds", true);
		}

		public void SetChatVoiceOverSetting(bool setting)
		{
			databaseApi.SaveUserValueFromBool("settings_voice_over", setting);
		}

		public bool GetChatVoiceOverSetting()
		{
			if (MonoSingleton<NativeAccessiblityManager>.Instance.AccessibilityLevel == NativeAccessibilityLevel.VOICE)
			{
				return true;
			}
			return databaseApi.LoadUserValueAsBool("settings_voice_over");
		}

		public void SetChatVoiceOverRateSetting(float voiceOverRate)
		{
			databaseApi.SaveUserValueFromFloat("settings_voice_over_rate", voiceOverRate);
		}

		public float GetChatVoiceOverRateSetting()
		{
			return databaseApi.LoadUserValueAsFloat("settings_voice_over_rate", 0.2f);
		}

		public void LoadRecentItems()
		{
			string text = databaseApi.LoadUserValue("recentItems");
			List<string> recentItems = new List<string>();
			if (text != string.Empty)
			{
				List<string> list = Prime31.Json.decode<List<string>>(text);
				if (list != null && list is List<string>)
				{
					recentItems = list;
				}
			}
			RecentItems = recentItems;
		}

		public void SaveRecentItems()
		{
			databaseApi.SaveUserValue("recentItems", Prime31.Json.encode(RecentItems));
		}

		public void SetUserSettings()
		{
			LoadRecentItems();
			SetTargetFramerate(GetDefaultFPS());
			bool flag = false;
			if (Service.IsSet<EnvironmentManager>() && EnvironmentManager.IsMusicPlaying)
			{
				flag = true;
			}
			if (!GetSoundEnabledSetting() || flag)
			{
				Singleton<SoundManager>.Instance.DisableSound();
			}
			else
			{
				Singleton<SoundManager>.Instance.EnableSound();
			}
		}

		public void SetDeviceSettings()
		{
			bool flag = false;
			if (Service.IsSet<EnvironmentManager>() && EnvironmentManager.IsMusicPlaying)
			{
				flag = true;
			}
			if (GetSoundEnabledSetting() && !flag)
			{
				Singleton<SoundManager>.Instance.EnableSound();
			}
			else
			{
				Singleton<SoundManager>.Instance.DisableSound();
			}
		}
	}
}
