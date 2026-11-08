using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Disney.MobileNetwork;
using Mix.AssetBundles;
using Mix.Assets.Worker;
using Mix.Connectivity;
using Mix.DeviceDb;
using Mix.Entitlements;
using Mix.Localization;
using Mix.Ui;
using UnityEngine;
using UnityEngine.UI;
using UnityTest;

namespace Mix.Assets
{
	public class AssetManager : MonoSingleton<AssetManager>, IAssetManager, ICpipeReady, ILoadFile, ISaveFile
	{
		public delegate void AssetObjectCompleteEvent(AssetObject aAssetObject);

		public const string TI = "AogN";

		private const float MIN_UNLOAD_TIME = 1f;

		private const float MAX_UNLOAD_TIME = 300f;

		private const long MAX_DISK_SPACE = 1000000000L;

		private const long MIN_FREE_DISK_SPACE = 300000000L;

		private const float MIN_FREE_DISK_PERCENT = 0.05f;

		public const string CONTENT_DATA_RELATIVE_PATH = "contentdata.zip";

		public const string CONTENT_DATA_JOE_RELATIVE_PATH = "contentdata.joe.zip";

		public const string MANIFEST_RELATIVE_PATH = "manifest/spark";

		public const string MIX_VERSION_DB_KEY = "MixVersion";

		public const string FirstRunManifestCacheFileName = "FirstRunManifest.txt";

		public List<AssetObject> AssetObjectQueue = new List<AssetObject>();

		public int concurrentConnections;

		public int maxConcurrentConnections = 4;

		public CpipeManager cpipeManager;

		private int DiskManagerCheckTime = 1800;

		private float TimeSinceLastDiskManagerCheck;

		public AssetCache assetCache = new LruCache();

		public DiskManager diskManager;

		public static bool SIMULATE_CONNECTION_LOST = false;

		public static bool IsTestRunner = false;

		private bool RefreshContent;

		public static ReferenceCounter RefCounter = new ReferenceCounter();

		private AssetBundleFactory assetBundleFactory;

		private float curUnloadTime;

		private bool shouldUnload;

		private IAssetDatabaseApi assetDatabaseApi;

		private ICpipeReady StartupCaller;

		public bool IsFirstRun;

		private bool areBlockingCallsSuppressedForGameplay;

		public BackgroundLoader BackgroundLoader;

		public static bool IsRunLocal
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		public static bool IS_DEBUG
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		public static bool IS_DEMO
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		public IAssetDatabaseApi AssetDatabaseApi
		{
			get
			{
				return assetDatabaseApi;
			}
			set
			{
				assetDatabaseApi = value;
				if (value != null)
				{
					diskManager.assetDatabaseApi = value;
				}
			}
		}

		public bool AreBlockingCallsSuppressedForGameplay
		{
			get
			{
				return areBlockingCallsSuppressedForGameplay;
			}
			set
			{
				areBlockingCallsSuppressedForGameplay = value;
			}
		}

		public static bool IsBackgroundLoad
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		public event AssetObjectCompleteEvent OnAssetObjectComplete;

		void ISaveFile.OnSaveFile(bool success, string path, object aUserData)
		{
			Action<bool> action = aUserData as Action<bool>;
			if (success)
			{
				diskManager.diskMonitor.AddFile(path);
			}
			if (action != null)
			{
				action(success);
			}
		}

		void ILoadFile.OnLoadFile(bool success, string path, byte[] bytes, object aUserData)
		{
			ImageLoadData imageLoadData = (ImageLoadData)aUserData;
			if (imageLoadData != null)
			{
				if (success)
				{
					imageLoadData.texture.LoadImage(bytes);
					imageLoadData.callback(imageLoadData.texture);
				}
				else
				{
					imageLoadData.callback(null);
				}
			}
		}

		public IAssetDatabaseApi GetDatabaseApiInterface()
		{
			return Singleton<MixDocumentCollections>.Instance.assetCacheDocumentCollectionApi;
		}

		protected void Awake()
		{
			diskManager = new DiskManager();
			Caching.maximumAvailableDiskSpace = 1000000000L;
			AssetDatabaseApi = null;
			VerifyCacheFolder();
		}

		public void AssetObjectDone(AssetObject aAssetObject)
		{
			concurrentConnections--;
			AssetObjectQueue.Remove(aAssetObject);
			UpdateQueue();
			if (this.OnAssetObjectComplete != null)
			{
				this.OnAssetObjectComplete(aAssetObject);
			}
		}

		public void UpdateQueue()
		{
			if (AssetObjectQueue.Count <= 0 || concurrentConnections >= maxConcurrentConnections)
			{
				return;
			}
			AssetObject assetObject = AssetObjectQueue[0];
			if (assetObject == null)
			{
				AssetObjectQueue.RemoveAt(0);
				UpdateQueue();
				return;
			}
			AssetObjectQueue.RemoveAt(0);
			concurrentConnections++;
			assetObject.flow.Init();
			if (concurrentConnections < maxConcurrentConnections)
			{
				UpdateQueue();
			}
		}

		public void OnLowMemoryEvent()
		{
			assetCache.LowMemoryWarning();
		}

		private void Start()
		{
			EnvironmentManager.LowMemoryEvent += OnLowMemoryEvent;
			if (Service.Get<MemoryMonitorManager>() != null)
			{
				long heapSize = (long)Service.Get<MemoryMonitorManager>().GetHeapSize();
				assetCache.MaxBytes = heapSize / 5;
			}
		}

		private void Update()
		{
			Updates();
		}

		public void Updates()
		{
			if (!IsRunLocal && !IS_DEMO)
			{
				TimeSinceLastDiskManagerCheck += Time.deltaTime;
				if (TimeSinceLastDiskManagerCheck > (float)DiskManagerCheckTime)
				{
					TimeSinceLastDiskManagerCheck = 0f;
					CheckDiskSpace();
				}
				if (cpipeManager != null && !areBlockingCallsSuppressedForGameplay)
				{
					cpipeManager.Update();
				}
			}
			if (assetBundleFactory != null)
			{
				assetBundleFactory.assetBundleController.UpdateQueue();
			}
			curUnloadTime += Time.deltaTime;
			if (!areBlockingCallsSuppressedForGameplay && ((shouldUnload && curUnloadTime > 1f) || curUnloadTime > 300f))
			{
				Resources.UnloadUnusedAssets();
				curUnloadTime = 0f;
				shouldUnload = false;
			}
		}

		public void CheckDiskSpace(Action aCallback = null)
		{
			MemoryMonitorManager memoryMonitorManager = Service.Get<MemoryMonitorManager>();
			if (memoryMonitorManager == null)
			{
				Debug.LogWarning("[AssetManager] CheckDiskSpace skipped: MemoryMonitorManager is null.");
				if (aCallback != null)
				{
					aCallback();
				}
				return;
			}

			long freeBytes = (long)memoryMonitorManager.GetFreeBytes();
			long totalBytes = (long)memoryMonitorManager.GetTotalBytes();
			if (totalBytes <= 0)
			{
				totalBytes = 1L;
			}

			float num = (float)((double)freeBytes / (double)totalBytes);
			long num2 = Caching.spaceOccupied + freeBytes - 300000000;
			if (num2 < 0)
			{
				num2 = 0L;
			}

			if (freeBytes < 300000000 || num < 0.05f || num2 < Caching.maximumAvailableDiskSpace)
			{
				Caching.maximumAvailableDiskSpace = num2;
				if (diskManager != null && diskManager.diskMonitor != null)
				{
					diskManager.diskMonitor.MaxCacheSize = num2;
				}
				IsBackgroundLoad = false;
			}

			if (freeBytes <= 300000000)
			{
				if (diskManager != null && diskManager.diskMonitor != null)
				{
					diskManager.diskMonitor.CheckForMaxSizeReached();
				}

				PanelManager panelManager = Singleton<PanelManager>.Instance;
				if (panelManager != null)
				{
					GenericPanel genericPanel = (GenericPanel)panelManager.ShowPanel(Panels.GENERIC);
					panelManager.SetupTokens(new Dictionary<Text, string>
					{
						{ genericPanel.TitleText, null },
						{ genericPanel.MessageText, "customtokens.global.low_disk_space" },
						{ genericPanel.ButtonTwoText, "customtokens.panels.button_ok" }
					});
					genericPanel.ButtonOne.gameObject.SetActive(false);
					panelManager.SetupButtons(new Dictionary<Button, Action> { { genericPanel.ButtonTwo, aCallback } });
				}
				else if (aCallback != null)
				{
					aCallback();
				}
			}
			else if (aCallback != null)
			{
				aCallback();
			}
		}

		public string GetCpipeUrl(string aUrl)
		{
			if (cpipeManager != null)
			{
				string fileUrl = cpipeManager.cpipe.GetFileUrl(aUrl);
				if (string.IsNullOrEmpty(fileUrl))
				{
					return aUrl;
				}
				return fileUrl;
			}
			return aUrl;
		}

		public void FlagRefreshContent()
		{
			RefreshContent = true;
		}

		public bool LatestManifestVersionNewerThanCachedVersion(string aRelativePathToFile)
		{
			int cpipeManifestVersion = Singleton<MixDocumentCollections>.Instance.assetCacheDocumentCollectionApi.GetCpipeManifestVersion(GetShaString(aRelativePathToFile));
			if (cpipeManager.cpipe.GetFileVersion(aRelativePathToFile) > cpipeManifestVersion)
			{
				return true;
			}
			return false;
		}

		public void OnCpipeReady(CpipeEvent aCpipeEvent)
		{
			if (StartupCaller != null)
			{
				StartupCaller.OnCpipeReady(aCpipeEvent);
				StartupCaller = null;
			}
			if (IsFirstRun)
			{
				IsFirstRun = false;
			}
			else if (aCpipeEvent != CpipeEvent.CpipeUnchanged || RefreshContent)
			{
				if (RefreshContent || LatestManifestVersionNewerThanCachedVersion(ConfigurationManager.RelativePathToConfigFile))
				{
					Singleton<ConfigurationManager>.Instance.Refresh(false, "AssetManager");
				}
				if (RefreshContent || LatestManifestVersionNewerThanCachedVersion("contentdata.joe.gz"))
				{
					Singleton<EntitlementsManager>.Instance.LoadContentData();
				}
				if (RefreshContent || LatestManifestVersionNewerThanCachedVersion(Localizer.GetLocFileUrl()))
				{
					Singleton<Localizer>.Instance.ReloadLocData(this, CachePolicy.DefaultCacheControlProtocol);
				}
				if (BackgroundLoader != null && IsBackgroundLoad)
				{
					BackgroundLoader.StartCpipeBackgroundLoading(cpipeManager.cpipe.GetHashes());
				}
				RefreshContent = false;
			}
		}

		public void OnCpipeFail(CpipeEvent aCpipeEvent)
		{
			if (StartupCaller != null)
			{
				StartupCaller.OnCpipeFail(aCpipeEvent);
				StartupCaller = null;
			}
		}

		public CpipeEnvironment GetEnvironment()
		{
			CpipeEnvironment result = CpipeEnvironment.Prod;
			string value = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(ConfigurationManager.EnvironmentString);
			try
			{
				result = (CpipeEnvironment)(int)Enum.Parse(typeof(CpipeEnvironment), value);
			}
			catch (Exception exception)
			{
				Log.Exception(exception);
			}
			return result;
		}

		public string GetBuildNumber()
		{
			TextAsset textAsset = Resources.Load<TextAsset>("version");
			if (textAsset != null)
			{
				return textAsset.text;
			}
			return string.Empty;
		}

		public static string GetCpipeFileNameInStreamingAssets()
		{
			return "1123402340.txt";
		}

		private IEnumerator WaitForPreloadData(ICpipeReady aCaller)
		{
			while (!PreloadData.CpipeDataComplete || !PreloadData.FirstRunCpipeDataComplete)
			{
				yield return null;
			}
			IsFirstRun = PreloadData.FirstRun;
			if (PreloadData.NewVersion)
			{
				FlagRefreshContent();
			}
			TestRunner testRunner = (TestRunner)UnityEngine.Object.FindObjectOfType(typeof(TestRunner));
			if (testRunner != null)
			{
				IsTestRunner = true;
			}
			BackgroundLoader = new BackgroundLoader(this);
			StartupCaller = aCaller;
			curUnloadTime = 0f;
			shouldUnload = false;
			cpipeManager = new CpipeManager(this);
			cpipeManager.OnCpipeLoaded += CpipeEventCallback;
			cpipeManager.init();
			assetBundleFactory = new AssetBundleFactory(this, this, RefCounter, assetCache);
		}

		public void Init(ICpipeReady aCaller)
		{
			StartCoroutine(WaitForPreloadData(aCaller));
		}

		public void CpipeEventCallback(CpipeEvent aCpipeEvent)
		{
			switch (aCpipeEvent)
			{
			case CpipeEvent.AuthToken:
				AuthToken();
				break;
			case CpipeEvent.AuthTokenFailed:
				if (!MonoSingleton<ConnectionManager>.Instance.IsConnected)
				{
					Singleton<EntitlementsManager>.Instance.RefreshInventoy();
					OnCpipeReady(aCpipeEvent);
				}
				AuthTokenFailed();
				break;
			case CpipeEvent.CpipeData:
				Singleton<EntitlementsManager>.Instance.RefreshInventoy();
				OnCpipeReady(aCpipeEvent);
				break;
			case CpipeEvent.CpipeDataFailed:
				OnCpipeFail(aCpipeEvent);
				break;
			case CpipeEvent.CpipeUnchanged:
				OnCpipeReady(aCpipeEvent);
				break;
			}
		}

		private void AuthToken()
		{
		}

		private void AuthTokenFailed()
		{
		}

		public static string GetShaString(string s)
		{
			SHA1Managed sHA1Managed = new SHA1Managed();
			UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
			byte[] bytes = unicodeEncoding.GetBytes(s);
			byte[] array = sHA1Managed.ComputeHash(bytes);
			string text = BitConverter.ToString(array);
			return text.Replace("-", string.Empty);
		}

		public static string GetUnpackFolder()
		{
			return Application.PersistentDataPath + "/cache/Unpack";
		}

		public static void VerifyCacheFolder()
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(Application.PersistentDataPath + "/cache");
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
			directoryInfo = new DirectoryInfo(Application.PersistentDataPath + "/cache/Avatar");
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
			directoryInfo = new DirectoryInfo(GetUnpackFolder());
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
		}

		public bool IsPathLocalForIsDemoMode(string path)
		{
			if (path.Contains("inventory.json") || path.Contains("localization") || path.Contains("donotcopy") || path.Contains("1184757944.txt") || path.Contains("manifest/spark") || path.Contains("contentdata.zip"))
			{
				return true;
			}
			return false;
		}

		// True when an entitlement's bundle can be loaded: the loaded cpipe
		// manifest lists it (the server has it) or the build ships it in
		// StreamingAssets. Used to hide stickers and gags we can't show.
		public bool IsBundleAvailable(string entitlementPath)
		{
			if (string.IsNullOrEmpty(entitlementPath))
			{
				return false;
			}
			string prefix = GetCpipePrefix(entitlementPath);
			if (cpipeManager != null && cpipeManager.cpipe != null && !string.IsNullOrEmpty(cpipeManager.cpipe.GetFileUrl(prefix)))
			{
				return true;
			}
			return File.Exists(Path.Combine(Application.StreamingAssetsPath, prefix));
		}

		public bool IsBundleCached(string relativeCpipePath)
		{
			if (string.IsNullOrEmpty(relativeCpipePath))
			{
				return false;
			}

			relativeCpipePath = GetCpipePrefix(relativeCpipePath);

			if (string.IsNullOrEmpty(relativeCpipePath) || cpipeManager == null || cpipeManager.cpipe == null)
			{
				return false;
			}

			string fileUrl = cpipeManager.cpipe.GetFileUrl(relativeCpipePath);
			int fileVersion = cpipeManager.cpipe.GetFileVersion(relativeCpipePath);
			string streamingFilePath = GetStreamingFilePath(relativeCpipePath);

			// Prevent ArgumentException: Input AssetBundle url cannot be null or empty.
			if (!string.IsNullOrEmpty(fileUrl) && Caching.IsVersionCached(fileUrl, fileVersion))
			{
				return true;
			}

			if (MonoSingleton<AssetManager>.Instance.DoesExist(string.Empty, streamingFilePath))
			{
				return true;
			}

			return false;
		}

		public bool IsBundleAssetCached(string bundlePath)
		{
			return assetCache.GetIndexOfKey(ConvertEntitlementPathToCpipePath(bundlePath)) >= 0;
		}

		public bool WillBundleLoadFromWeb(string bundlePath)
		{
			return !IsBundleAssetCached(bundlePath) && !IsBundleCached(bundlePath);
		}

        // Define these at the top of your AssetManager class
#if UNITY_ANDROID && !UNITY_EDITOR
    private const string PathProtocol = "";
    public const string BundlePlatform = "android";
#elif UNITY_IOS && !UNITY_EDITOR
    private const string PathProtocol = "file://";
    public const string BundlePlatform = "iphone";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR
        // Specifically targets Windows or the Editor
        private const string PathProtocol = "file://";
        public const string BundlePlatform = "windows64";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
        // Specifically targets Windows or the Editor
        private const string PathProtocol = "file://";
        public const string BundlePlatform = "standalonemacos";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR
        // Specifically targets Windows or the Editor
        private const string PathProtocol = "file://";
        public const string BundlePlatform = "standalonelinux64";
#else
    // Fallback for other platforms (Mac, Linux, WebGL, etc.)
    private const string PathProtocol = "file://";
    public const string BundlePlatform = "android";
#endif
        // The folder each platform's bundles live in, both in StreamingAssets
        // and in the cpipe manifest's paths (AssetBundles/<platform>/hd/...).
        // Linux is standalonelinux64, the folder RebuildAssetBundles writes for
        // StandaloneLinux64.
        private const string BundleFolder = "AssetBundles/" + BundlePlatform + "/";

        public static string GetStreamingFilePath(string url)
        {
            // 1. Look for the platform folder (e.g., AssetBundles/android/)
            int index = url.IndexOf(BundleFolder, StringComparison.OrdinalIgnoreCase);

            if (index != -1)
            {
                // 2. Use string interpolation and range operator for cleanliness
				Debug.Log($"GetStreamingFilePath: Found platform folder in URL. Returning path: {Path.Combine($"{PathProtocol}{Application.StreamingAssetsPath}", url[index..])}");
                return Path.Combine($"{PathProtocol}{Application.StreamingAssetsPath}", url[index..]);
            }

            // 3. Fallback: sanitize and combine
            return Path.Combine(Application.StreamingAssetsPath, url.TrimStart('/'));
        }


        public static string GetDiskCacheFilePath()
		{
			return Application.PersistentDataPath + "/cache/";
		}

		public void UnloadAssetsWhenPossible()
		{
			shouldUnload = true;
		}

		public bool DoesExist(string relPath, string FullPath = null)
		{
			string text = FullPath;
			if (FullPath == null)
			{
				text = GetDiskCacheFilePath() + relPath;
			}
			if (text.StartsWith("file://"))
			{
				text = text.Substring(7);
			}
			if (IsPathAndroidStreamingAssets(text))
			{
				bool result = false;
				using (WWW wWW = new WWW(text))
				{
					while (!wWW.isDone && string.IsNullOrEmpty(wWW.error) && !(wWW.progress > 0f))
					{
					}
					if (string.IsNullOrEmpty(wWW.error))
					{
						result = true;
					}
					wWW.Dispose();
					return result;
				}
			}
			return File.Exists(text);
		}

		public bool SaveText(string relPath, string text, string FullPath = null)
		{
			string text2 = FullPath;
			if (FullPath == null)
			{
				text2 = GetDiskCacheFilePath() + relPath;
			}
			try
			{
				File.WriteAllText(text2, text);
			}
			catch (Exception exception)
			{
				Log.Exception(exception);
				return false;
			}
			diskManager.diskMonitor.AddFile(text2);
			return true;
		}

		public static string LoadTextStatic(string relPath, string FullPath = null)
		{
			string text = FullPath;
			if (FullPath == null)
			{
				text = GetDiskCacheFilePath() + relPath;
			}
			try
			{
				if (IsPathAndroidStreamingAssetsStatic(text))
				{
					using (WWW wWW = new WWW(text))
					{
						while (!wWW.isDone)
						{
						}
						if (!string.IsNullOrEmpty(wWW.error))
						{
						}
						if (IS_DEBUG)
						{
						}
						string text2 = wWW.text;
						wWW.Dispose();
						return text2;
					}
				}
				return File.ReadAllText(text);
			}
			catch (IOException exception)
			{
				Log.Exception(exception);
				return string.Empty;
			}
		}

		public string LoadText(string relPath, string FullPath = null)
		{
			return LoadTextStatic(relPath, FullPath);
		}

		public byte[] LoadBytes(string filepath)
		{
			try
			{
				if (IsPathAndroidStreamingAssets(filepath))
				{
					using (WWW wWW = new WWW(filepath))
					{
						while (!wWW.isDone)
						{
						}
						if (!string.IsNullOrEmpty(wWW.error))
						{
						}
						if (IS_DEBUG)
						{
						}
						byte[] bytes = wWW.bytes;
						wWW.Dispose();
						return bytes;
					}
				}
				return File.ReadAllBytes(filepath);
			}
			catch (IOException exception)
			{
				Log.Exception(exception);
				return null;
			}
		}

		public bool DeleteFile(string relPath, string FullPath = null)
		{
			string text = FullPath;
			if (FullPath == null)
			{
				text = GetDiskCacheFilePath() + relPath;
			}
			if (DoesExist(string.Empty, text))
			{
				try
				{
					File.Delete(text);
				}
				catch (Exception exception)
				{
					Log.Exception(exception);
					return false;
				}
				return true;
			}
			return false;
		}

		public bool SaveBytes(string relPath, byte[] bytes, string FullPath = null)
		{
			string text = FullPath;
			if (FullPath == null)
			{
				text = GetDiskCacheFilePath() + relPath;
			}
			try
			{
				File.WriteAllBytes(text, bytes);
			}
			catch (Exception exception)
			{
				Log.Exception(exception);
				return false;
			}
			diskManager.diskMonitor.AddFile(text);
			return true;
		}

		public void SaveImage(string relPath, Texture2D image, Action<bool> callback, string FullPath = null)
		{
			string text = FullPath;
			if (FullPath == null)
			{
				text = GetDiskCacheFilePath() + relPath;
			}
			if (text.EndsWith(".jpg"))
			{
				try
				{
					if (image != null && text != null && image.width > 0 && image.height > 0)
					{
						byte[] array = image.EncodeToJPG();
						if (array != null && array.Length > 0)
						{
							new SaveFile(this, text, array.Length, array, false, callback);
						}
						else
						{
							callback(false);
						}
					}
					return;
				}
				catch (Exception exception)
				{
					Log.Exception(exception);
					callback(false);
					return;
				}
			}
			try
			{
				if (!(image != null) || text == null || image.width <= 0 || image.height <= 0)
				{
					return;
				}
				try
				{
					byte[] array2 = image.EncodeToPNG();
					if (array2 != null && array2.Length > 0)
					{
						new SaveFile(this, text, array2.Length, array2, false, callback);
					}
					else
					{
						callback(false);
					}
				}
				catch (UnityException)
				{
					callback(false);
				}
			}
			catch (Exception exception2)
			{
				Log.Exception(exception2);
				callback(false);
			}
		}

		public static bool IsPathAndroidStreamingAssetsStatic(string path)
		{
			if (path.StartsWith("jar:file://") && Application.Platform == 11)
			{
				return true;
			}
			return false;
		}

		public bool IsPathAndroidStreamingAssets(string path)
		{
			return IsPathAndroidStreamingAssetsStatic(path);
		}

		public bool GetImage(string relPath, ref Texture2D image, string FullPath = null)
		{
			string text = FullPath;
			if (FullPath == null)
			{
				text = GetDiskCacheFilePath() + relPath;
			}
			try
			{
				byte[] array = null;
				if (IsPathAndroidStreamingAssets(text))
				{
					using (WWW wWW = new WWW(text))
					{
						while (!wWW.isDone)
						{
						}
						if (!string.IsNullOrEmpty(wWW.error))
						{
							wWW.Dispose();
							return false;
						}
						if (IS_DEBUG)
						{
						}
						bool result = image.LoadImage(wWW.bytes);
						wWW.Dispose();
						return result;
					}
				}
				array = File.ReadAllBytes(text);
				if (array.Length > 0)
				{
					return image.LoadImage(array);
				}
				return false;
			}
			catch (IOException exception)
			{
				Log.Exception(exception);
				return false;
			}
		}

		public void LoadImage(string relPath, ref Texture2D image, OnImageLoaded aCallback, string FullPath = null)
		{
			if (FullPath == null)
			{
			}
			try
			{
				bool image2 = GetImage(relPath, ref image);
				aCallback((!image2) ? null : image);
			}
			catch (IOException exception)
			{
				Log.Exception(exception);
				aCallback(null);
			}
		}

		public AssetObject LoadAssetStoreImage(IPNGAssetObject aCaller, string aSwid, string aId, bool aIsThumb = false, object aUserData = null)
		{
			string empty = string.Empty;
			if (aIsThumb)
			{
			}
			LoadParams aLoadParams = new LoadParams(GetShaString(empty), empty, CachePolicy.CacheThenBundleThenDownload);
			return LoadPng(aCaller, aLoadParams, aUserData);
		}

		public AssetObject LoadAssetStoreVideo(IVideoAssetObject aCaller, string aSwid, string aId, object aUserData = null)
		{
			string empty = string.Empty;
			LoadParams aLoadParams = new LoadParams(GetShaString(empty), empty, CachePolicy.CacheThenBundleThenDownload);
			VideoAssetObject videoAssetObject = new VideoAssetObject(this, aCaller, aLoadParams, aUserData);
			AssetObjectQueue.Add(videoAssetObject);
			return videoAssetObject;
		}

		public void LoadZip(IZipAssetObject aCaller, LoadParams aLoadParams, object aUserData = null)
		{
			AssetObjectQueue.Add(new ZipAssetObject(this, aCaller, aLoadParams, aUserData));
			UpdateQueue();
		}

		public void LoadJsonFromZip(Func<string, object> aMethod, string aZipName, string aEntryName, IZipJsonAssetObject aCaller, LoadParams aLoadParams, object aUserData = null)
		{
			ZipJsonAssetObject zipJsonAssetObject = new ZipJsonAssetObject(aMethod, aZipName, aEntryName, this, aCaller, aLoadParams, aUserData);
			AssetObjectQueue.Add(zipJsonAssetObject.zipAssetObject);
			UpdateQueue();
		}

		public void LoadFileFromZip(string aZipName, string aEntryName, IZipFileAssetObject aCaller, LoadParams aLoadParams, object aUserData = null)
		{
			ZipFileAssetObject zipFileAssetObject = new ZipFileAssetObject(aZipName, aEntryName, this, aCaller, aLoadParams, aUserData);
			AssetObjectQueue.Add(zipFileAssetObject.zipAssetObject);
			UpdateQueue();
		}

		public void LoadText(ITextAssetObject aCaller, LoadParams aLoadParams, object aUserData = null)
		{
			AssetObjectQueue.Add(new TextAssetObject(this, aCaller, aLoadParams, aUserData));
			UpdateQueue();
		}

		public AssetObject LoadPng(IPNGAssetObject aCaller, LoadParams aLoadParams, object aUserData = null)
		{
			PNGAssetObject pNGAssetObject = new PNGAssetObject(this, aCaller, aLoadParams, aUserData);
			AssetObjectQueue.Add(pNGAssetObject);
			UpdateQueue();
			return pNGAssetObject;
		}

		public string ConvertEntitlementPathToCpipePath(string aPath)
		{
			if (string.IsNullOrEmpty(aPath))
			{
				return null;
			}
			if (aPath.StartsWith("http://") || aPath.StartsWith("https://") || aPath.StartsWith("file://"))
			{
				return aPath;
			}
			string cpipePrefix = GetCpipePrefix(aPath);
			aPath = MonoSingleton<AssetManager>.Instance.cpipeManager.cpipe.GetFileUrl(cpipePrefix);
			if (string.IsNullOrEmpty(aPath))
			{
				return cpipePrefix;
			}
			return aPath;
		}

		public string GetCpipePrefix(string aEntitlementPath)
		{
			string bundlePlatform = Singleton<SettingsManager>.Instance.GetBundlePlatform();
			// Manifest keys and URLs always use "/". Path.Combine alone would give
			// "AssetBundles/windows64/hd\\..." on Windows, which no manifest lists.
			aEntitlementPath = Path.Combine("AssetBundles/" + bundlePlatform + "/hd", aEntitlementPath).Replace('\\', '/');
			if (aEntitlementPath.Contains("avatar") && aEntitlementPath.Contains("_sd"))
			{
				aEntitlementPath = aEntitlementPath.Replace("/hd/", "/sd/");
				aEntitlementPath = aEntitlementPath.Replace("_hd", "_sd");
			}
			return aEntitlementPath;
		}

		public void AddBundleInstance(string aUrl, UnityEngine.Object aObject, int aReferenceCount = 0)
		{
			string aKey = ConvertEntitlementPathToCpipePath(aUrl);
			RefCounter.AddBundleInstance(aKey, aObject, aReferenceCount);
		}

		public bool HasBundleInstance(string aUrl)
		{
			string key = ConvertEntitlementPathToCpipePath(aUrl);
			return RefCounter.BundleInstances.ContainsKey(key);
		}

		public UnityEngine.Object GetBundleInstance(string aUrl)
		{
			string text = ConvertEntitlementPathToCpipePath(aUrl);
			UnityEngine.Object obj = (UnityEngine.Object)assetCache.Get(text);
			if (obj != null)
			{
				if (!(obj is Texture2D))
				{
					obj = UnityEngine.Object.Instantiate(obj);
				}
				IncrementBundleReferenceCount(text);
				return obj;
			}
			return RefCounter.GetInstance(text);
		}

		public void DestroyBundleInstance(string aUrl, UnityEngine.Object aObject = null)
		{
			RefCounter.DestroyInstance(ConvertEntitlementPathToCpipePath(aUrl), aObject);
		}

		public void IncrementBundleReferenceCount(string aUrl)
		{
			RefCounter.IncrementReferenceCount(ConvertEntitlementPathToCpipePath(aUrl));
		}

		public void DecrementBundleReferenceCount(string aUrl)
		{
			RefCounter.DecrementReferenceCount(ConvertEntitlementPathToCpipePath(aUrl));
		}

		public int GetReferenceCount(string aUrl)
		{
			return RefCounter.GetRefCount(ConvertEntitlementPathToCpipePath(aUrl));
		}

		public void LoadABundle(IBundleObject aCaller, string aUrl, object aUserData = null, string aAssetName = "", bool aMemCache = false, bool aIsBackgroundLoad = false, bool aUseCache = false)
		{
			bool aUnpackTexture = aUrl.Contains("_thumb.");
			assetBundleFactory.LoadAssetBundle(aUrl, aCaller, null, aUnpackTexture, aUserData, aUseCache);
		}

		public void LoadAssetBundle(string aUrl, IBundleObject aContentCallback, string aCacheKey, bool aUnpackTexture = false, object aUserData = null, bool aUseMemCache = true, Action<float> aDownloadPercentCallback = null, ThreadPriority aThreadPriority = ThreadPriority.Normal, string aAssetName = null)
		{
			assetBundleFactory.LoadAssetBundle(aUrl, aContentCallback, aCacheKey, aUnpackTexture, aUserData, aUseMemCache, aDownloadPercentCallback, aThreadPriority, aAssetName);
		}

		public void CancelBundles(IBundleObject aCaller)
		{
			assetBundleFactory.assetBundleController.CancelBundleLoad(aCaller);
		}

		public void CancelBundles(string aUrl)
		{
			assetBundleFactory.assetBundleController.CancelBundleLoad(aUrl);
		}

		public static void CancelAllBundles()
		{
		}
	}
}
