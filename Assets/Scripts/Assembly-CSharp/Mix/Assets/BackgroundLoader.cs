using System.Collections;
using System.Collections.Generic;
using System.IO;
using LitJson;
using Mix.AssetBundles;
using UnityEngine;

namespace Mix.Assets
{
	public class BackgroundLoader : IBundleObject
	{
		public const float BackgroundLoadWaitTime = 0.2f;

		public const string CacheFile = "BackgroundLoader.txt";

		public static bool IsCpipeLoaderStarted;

		protected List<string>[] Files = new List<string>[3]
		{
			new List<string>(),
			new List<string>(),
			new List<string>()
		};

		protected bool IsLoading;

		protected bool IsCancel;

		private MonoBehaviour MonoBehaviourEngine;

		private string device;

		private string LoadedFilesString = string.Empty;

		private int LoadedFilesThreshold = 10;

		private int LoadedFiles;

		private List<string> MemCacheLoadedFiles;

		public string ActiveLoadingPath { get; private set; }

		public int TotalLoaded { get; private set; }

		public BackgroundLoader(MonoBehaviour aMonoBehaviour)
		{
			ActiveLoadingPath = string.Empty;
			TotalLoaded = 0;
			MonoBehaviourEngine = aMonoBehaviour;
			device = Singleton<SettingsManager>.Instance.GetBundlePlatform();
			FileInfo fileInfo = new FileInfo(AssetManager.GetDiskCacheFilePath() + "BackgroundLoader.txt");
			if (!fileInfo.Exists)
			{
				fileInfo.Create();
				MemCacheLoadedFiles = new List<string>();
			}
			else
			{
				string text = File.ReadAllText(fileInfo.FullName);
				MemCacheLoadedFiles = new List<string>(text.Split(','));
			}
		}

		void IBundleObject.OnBundleAssetObject(Object aGameObject, object aUserData)
		{
			IsLoading = false;
			MonoBehaviourEngine.StartCoroutine(UpdateBackgroundLoad());
		}

		public List<string>[] GetFiles()
		{
			return Files;
		}

		public void StartCpipeBackgroundLoading(JsonData aHashes)
		{
			if (!IsCpipeLoaderStarted)
			{
				IsCpipeLoaderStarted = true;
				sortHashes(aHashes);
			}
		}

		private void sortHashes(JsonData aHashes)
		{
			Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
			dictionary["AvatarSd"] = new List<string>();
			dictionary["AvatarHd"] = new List<string>();
			dictionary["AvatarThumbs"] = new List<string>();
			dictionary["Gags"] = new List<string>();
			dictionary["Gogs"] = new List<string>();
			dictionary["Stickers"] = new List<string>();
			dictionary["StickerPacks"] = new List<string>();
			dictionary["Other"] = new List<string>();
			foreach (DictionaryEntry item in (IDictionary)aHashes)
			{
				if (item.Key.ToString().Contains(device + "/hd/"))
				{
					string text = item.Key.ToString().Replace("AssetBundles/" + device + "/hd/", string.Empty);
					if (text.Contains("avatar/"))
					{
						dictionary["AvatarHd"].Add(text);
					}
					else if (text.Contains("avatar_thumbs/"))
					{
						dictionary["AvatarThumbs"].Add(text);
					}
					else if (text.Contains("gags/"))
					{
						dictionary["Gags"].Add(text);
					}
					else if (text.Contains("gogs/"))
					{
						dictionary["Gogs"].Add(text);
					}
					else if (text.Contains("stickerpack_thumbs/"))
					{
						dictionary["StickerPacks"].Add(text);
					}
					else if (text.Contains("stickers/"))
					{
						dictionary["Stickers"].Add(text);
					}
					else
					{
						dictionary["Other"].Add(text);
					}
				}
				else if (item.Key.ToString().Contains(device + "/sd/"))
				{
					dictionary["AvatarSd"].Add(item.Key.ToString().Replace("AssetBundles/" + device + "/sd/", string.Empty));
				}
			}
			AddFiles(dictionary["AvatarThumbs"], LoadPriority.Low);
			AddFiles(dictionary["AvatarSd"], LoadPriority.Low);
			AddFiles(dictionary["AvatarHd"], LoadPriority.Low);
			AddFiles(dictionary["StickerPacks"], LoadPriority.Low);
			AddFiles(dictionary["Stickers"], LoadPriority.Low);
			AddFiles(dictionary["Gags"], LoadPriority.Low);
			AddFiles(dictionary["Gogs"], LoadPriority.Low);
			AddFiles(dictionary["Other"], LoadPriority.Low);
		}

		public void Abort()
		{
			IsCancel = true;
			if (IsLoading)
			{
				MonoSingleton<AssetManager>.Instance.CancelBundles(ActiveLoadingPath);
			}
			Files = null;
		}

		public void AddFile(string aCpipePath, LoadPriority aLoadPriority)
		{
			if (!string.IsNullOrEmpty(aCpipePath) && !IsPathAlreadyLoaded(aCpipePath))
			{
				List<string> list = Files[(int)aLoadPriority];
				list.Add(aCpipePath);
				Refresh();
			}
		}

		public void AddFiles(List<string> aCpipePaths, LoadPriority aLoadPriority)
		{
			if (aCpipePaths == null)
			{
				return;
			}
			List<string> list = Files[(int)aLoadPriority];
			for (int i = 0; i < aCpipePaths.Count; i++)
			{
				string text = aCpipePaths[i];
				if (!string.IsNullOrEmpty(text) && !IsPathAlreadyLoaded(text))
				{
					list.Add(text);
				}
			}
			Refresh();
		}

		public bool IsPathAlreadyLoaded(string path)
		{
			int num = MemCacheLoadedFiles.IndexOf(MonoSingleton<AssetManager>.Instance.GetCpipeUrl(MonoSingleton<AssetManager>.Instance.GetCpipePrefix(path)));
			if (num < 0)
			{
				return false;
			}
			return true;
		}

		protected string GetNextLoadPath()
		{
			string result = null;
			List<string> list = Files[0];
			if (list.Count > 0)
			{
				result = list[0];
				list.RemoveAt(0);
				return result;
			}
			list = Files[1];
			if (list.Count > 0)
			{
				result = list[0];
				list.RemoveAt(0);
				return result;
			}
			list = Files[2];
			if (list.Count > 0)
			{
				result = list[0];
				list.RemoveAt(0);
				return result;
			}
			return result;
		}

		public void Refresh()
		{
			if (AssetManager.IsBackgroundLoad && !IsLoading && !IsCancel)
			{
				ActiveLoadingPath = GetNextLoadPath();
				if (!string.IsNullOrEmpty(ActiveLoadingPath))
				{
					IsLoading = true;
					MonoSingleton<AssetManager>.Instance.LoadABundle(this, ActiveLoadingPath, ActiveLoadingPath, string.Empty, false, true);
				}
			}
		}

		private IEnumerator UpdateBackgroundLoad()
		{
			yield return new WaitForSeconds(0.2f);
			Refresh();
		}

		public void AddLoadedFilesString(string path)
		{
			int num = MemCacheLoadedFiles.IndexOf(path);
			if (num < 0)
			{
				MemCacheLoadedFiles.Add(path);
				LoadedFilesString = LoadedFilesString + path + ",";
				LoadedFiles++;
				if (LoadedFiles >= LoadedFilesThreshold)
				{
					DumpFileString(LoadedFilesString);
					LoadedFiles = 0;
					LoadedFilesString = string.Empty;
				}
			}
		}

		private void DumpFileString(string str)
		{
			FileInfo fileInfo = new FileInfo(AssetManager.GetDiskCacheFilePath() + "BackgroundLoader.txt");
			File.AppendAllText(fileInfo.FullName, str);
		}
	}
}
