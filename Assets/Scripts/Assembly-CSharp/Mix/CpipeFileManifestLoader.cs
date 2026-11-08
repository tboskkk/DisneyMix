using System;
using System.Collections;
using Disney.MobileNetwork;
using Mix.Assets;
using Mix.Connectivity;
using UnityEngine;

namespace Mix
{
	public class CpipeFileManifestLoader : ITextAssetObject
	{
		private const int MAX_LOAD_ATTEMPTS = 3;

		private const float LOAD_ATTEMPT_INTERVAL = 0.5f;

		private CpipeCallback onComplete;

		private CpipeErrorCallback onError;

		private CpipeFileManifest manifest;

		private int loadAttempts;

		private string oldVersion;

		private string ContentVersion = string.Empty;

		private bool refreshing;

		// A refresh to an unreachable host (packets dropped, not refused) would
		// otherwise hold up startup for the OS connect timeout: WWW has none.
		private const float REFRESH_TIMEOUT_SECONDS = 10f;

		private bool completed;

		void ITextAssetObject.TextAssetObjectComplete(string aText, object aUserData)
		{
			TextReady(aText, aUserData);
		}

		public void Load(CpipeCallback onComplete, CpipeErrorCallback onError, bool aIsFirstRun, CachePolicy aCachePolicy, string oldVersion)
		{
			this.onComplete = onComplete;
			this.onError = onError;
			this.oldVersion = oldVersion;

			refreshing = !aIsFirstRun;
			completed = false;

			// Startup loads the manifest twice, as Disney's app did. The first
			// load (from AssetManager start) uses the cached or bundled manifest
			// (PreloadData.CpipeData): it can't use the network, because
			// cpipeManager.cpipe is still null until it completes and
			// AssetManager.GetCpipeUrl dereferences it. The second load comes
			// from StartUpSequence's CpipeUpdateOperation and asks cpipe_url for
			// the server's manifest. The response is cached under
			// "cpipeManfiest", so the next boot's first load starts from it.
			if (aIsFirstRun)
			{
				PrepareManifest(null, true);
				return;
			}
			try
			{
				MonoSingleton<AssetManager>.Instance.StartCoroutine(RefreshWatchdog());
				string clientVersion = (EnvironmentManager.BundleVersion != null) ? EnvironmentManager.BundleVersion.ToString() : "0.0.0";
				Singleton<CpipeManifestApi>.Instance.GetCpipeManifest(string.Empty, clientVersion, oldVersion, this, aCachePolicy);
			}
			catch (Exception exception)
			{
				// Startup waits for this callback, so a refresh that can't even
				// start must still report: keep the current manifest.
				Log.Exception("Cpipe manifest refresh failed to start", exception);
				Finish(false);
			}
		}

		private IEnumerator RefreshWatchdog()
		{
			yield return new WaitForSecondsRealtime(REFRESH_TIMEOUT_SECONDS);
			if (!completed)
			{
				Log.Exception("Cpipe manifest refresh timed out; keeping the current manifest");
				Finish(false);
			}
		}

		// Reports to the caller exactly once: the watchdog and the response
		// race, and a late response must not complete the operation twice.
		private void Finish(bool changed)
		{
			if (!completed)
			{
				completed = true;
				onComplete(changed);
			}
		}

		public void TextReady(string aText, object aUserData)
		{
			if (completed)
			{
				return;
			}
			if (aUserData != null)
			{
				Hashtable hashtable = (Hashtable)aUserData;
				if ((string)hashtable["FLOW_HTTPSTATUS"] == "304")
				{
					Finish(false);
					return;
				}
			}
			if (aText != string.Empty && aText != null)
			{
				PrepareManifest(aText, false);
				return;
			}
			if (!string.IsNullOrEmpty(ContentVersion) && MonoSingleton<ConnectionManager>.Instance.IsConnected && aUserData != null)
			{
				Hashtable hashtable2 = (Hashtable)aUserData;
				if ((string)hashtable2["FLOW_HTTPSTATUS"] == "204")
				{
					Log.Exception("No Cpipe Manifest for client version: " + ContentVersion);
				}
			}
			// A refresh that gets no manifest (no server, error status, empty
			// body) keeps the one from the first load instead of failing boot.
			Finish(false);
		}

		public bool IsLoaded()
		{
			return manifest != null;
		}

		public CpipeFileManifest GetManifest()
		{
			if (!IsLoaded())
			{
				throw new Exception("The CPIPE manifest has not been instantiated yet. Has CpipeFileManifestLoader.Load been called?");
			}
			return manifest;
		}

		private void PrepareManifest(string json, bool aFirstRun)
		{
			manifest = new CpipeFileManifest();
			if (!manifest.Prepare(json, aFirstRun))
			{
				if (refreshing)
				{
					// Malformed server manifest: Prepare already dropped the
					// cached copy. Keep the current manifest (CpipeUnchanged).
					Finish(false);
				}
				else
				{
					onError("error preparing manifest");
				}
			}
			else
			{
				Finish(true);
			}
		}
	}
}
