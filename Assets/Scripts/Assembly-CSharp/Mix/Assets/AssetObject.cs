using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mix.Assets.Worker;
using UnityEngine;

namespace Mix.Assets
{
	public abstract class AssetObject : IFlow, IAddRecord, IGetRecord, IUpdateRecord
	{
		public const string RESPONSE_HEADERS = "responseHeaders";

		public const string CACHE_CONTROL = "Cache-Control";

		public const string EXPIRES = "Expires";

		public const string ETAG = "Etag";

		public const string LAST_MODIFIED = "Last-Modified";

		public const string EL = "AbM";

		protected IAssetManager assetManager;

		protected object userData;

		protected Dictionary<string, string> header;

		public Flow flow { get; private set; }

		public LoadParams LoadParams { get; private set; }

		public AssetObject(IAssetManager aAssetManager, LoadParams aLoadParams, object aUserData)
		{
			assetManager = aAssetManager;
			if (AssetManager.IsRunLocal || AssetManager.IS_DEMO)
			{
				LoadParams loadParams = new LoadParams(aLoadParams.Sha, aLoadParams.Url, CachePolicy.CacheThenBundleThenDownload, aLoadParams.ThreadPriority);
				aLoadParams = loadParams;
			}
			LoadParams = aLoadParams;
			userData = aUserData;
			flow = new Flow(this, LoadParams);
			flow.IsBundled = IsInBundle();
			int fileVersion = MonoSingleton<AssetManager>.Instance.cpipeManager.cpipe.GetFileVersion(LoadParams.Url);
			LoadParams.CpipeManifestVersion = fileVersion;
		}

		void IGetRecord.OnGetRecord(Record aRecord, object aUserData)
		{
			if (aRecord != null && aRecord.Path != null)
			{
				string filePath = GetFilePath();
				if (File.Exists(filePath))
				{
					RecordReturnedFromDBAndPathExistsLocal(filePath);
				}
			}
			flow.UpdateFlow();
		}

		public static string GetHeaderEntry(Dictionary<string, string> aHeaders, string aHeaderName)
		{
			if (aHeaders.ContainsKey(aHeaderName))
			{
				string text = aHeaders[aHeaderName];
				if (text == null)
				{
				}
				return text;
			}
			return null;
		}

		public static long GetMaxAge(Dictionary<string, string> headers)
		{
			string headerEntry = GetHeaderEntry(headers, "Cache-Control");
			if (headerEntry != null && !string.IsNullOrEmpty(headerEntry))
			{
				string[] separator = new string[1] { "," };
				string[] array = headerEntry.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				string[] array2 = array;
				foreach (string text in array2)
				{
					int num = text.IndexOf("max-age=");
					if (num > -1)
					{
						string value = text.Substring(num + 8);
						try
						{
							return Convert.ToInt64(value);
						}
						catch (Exception exception)
						{
							Log.Exception(exception);
						}
						break;
					}
				}
			}
			return -1L;
		}

		public bool IsPathLocalForIsDemoMode(string aPath)
		{
			return assetManager.IsPathLocalForIsDemoMode(aPath);
		}

		public Dictionary<string, string> GetEtagRequestHeader()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			if (header != null && header.Count > 0)
			{
				string headerEntry = GetHeaderEntry(header, "ETag");
				if (headerEntry != null)
				{
					if (dictionary == null)
					{
						dictionary = new Dictionary<string, string>();
					}
					dictionary.Add("If-None-Match", header["ETag"]);
				}
			}
			if (LoadParams.Headers != null)
			{
				if (dictionary == null)
				{
					dictionary = new Dictionary<string, string>();
				}
				foreach (KeyValuePair<string, string> header in LoadParams.Headers)
				{
					dictionary.Add(header.Key, header.Value);
				}
			}
			return dictionary;
		}

		public void CallChildCaller(object[] paramList)
		{
			string name = "CallCaller";
			if (GetType().GetMethod(name) != null)
			{
				MethodInfo method = GetType().GetMethod(name);
				method.Invoke(this, paramList);
			}
			else
			{
				Destroy();
			}
		}

		public virtual void ErrorHandler()
		{
			Destroy();
		}

		public virtual string GetFileExtension()
		{
			return string.Empty;
		}

		public string GetFilePath()
		{
			return Application.PersistentDataPath + "/cache/" + LoadParams.Sha + GetFileExtension();
		}

		public void ParseNextFlowState()
		{
			string methodName = flow.MethodName;
			if (GetType().GetMethod(methodName) != null)
			{
				MethodInfo method = GetType().GetMethod(methodName);
				method.Invoke(this, null);
			}
			else
			{
				flow.MethodName = "ErrorHandler";
			}
		}

		public void CheckHeaders()
		{
			Record record = assetManager.GetDatabaseApiInterface().GetRecord(LoadParams.Sha);
			GotRecord(record);
		}

		public void GotRecord(Record aRecord)
		{
			if (aRecord != null)
			{
				Dictionary<string, string> dictionary = aRecord.Header;
				header = dictionary;
				if (header != null)
				{
					flow.IsCached = true;
					flow.IsHeaderStale = isHeaderStale(header, Convert.ToDateTime(aRecord.InsertTime));
				}
			}
			flow.UpdateFlow();
		}

		public virtual void RecordReturnedFromDBAndPathExistsLocal(string Path)
		{
			throw new Exception("Override RecordReturnedFromDBAndPathExistsLocal");
		}

		public virtual void LoadFromWeb()
		{
			throw new Exception("Override LoadFromWeb");
		}

		public virtual void LoadFromDB()
		{
			flow.IsAlreadyLoadFromDB = true;
			new GetRecord(this, LoadParams.Sha, assetManager.GetDatabaseApiInterface());
		}

		private IEnumerator LoadTextCoroutine(string aUrl, Dictionary<string, string> aHeaders)
		{
			WWW www = new WWW(aUrl, null, aHeaders);
			yield return www;
			string text = www.text;
			Dictionary<string, string> headers = www.responseHeaders;
			string error = www.error;
			bool success = string.IsNullOrEmpty(error);
			if (success && string.IsNullOrEmpty(text))
			{
				if (headers.ContainsKey("STATUS"))
				{
					string status = headers["STATUS"];
					if (status.Contains("304"))
					{
						flow.HttpStatus = 304;
					}
					else
					{
						if (!status.Contains("204"))
						{
						}
						success = false;
						flow.HttpStatus = 204;
					}
				}
			}
			else if (text.StartsWith("<") && !AssetManager.IsTestRunner)
			{
				success = false;
				flow.HttpStatus = 500;
			}
			else
			{
				flow.HttpStatus = 200;
			}
			WebTextResponseHandler(success, text, headers);
		}

		public void LoadTextFromWeb()
		{
			flow.IsAlreadyLoadFromWeb = true;
			Dictionary<string, string> etagRequestHeader = GetEtagRequestHeader();
			ThreadPoolManager.Instance.StartCoroutine(LoadTextCoroutine(assetManager.GetCpipeUrl(LoadParams.Url), etagRequestHeader));
		}

		private IEnumerator LoadBinaryCoroutine(string aUrl, Dictionary<string, string> aHeaders)
		{
			WWW www = new WWW(aUrl, null, aHeaders);
			yield return www;
			byte[] bytes = null;
			Dictionary<string, string> headers = www.responseHeaders;
			string error = www.error;
			if (www.bytes != null)
			{
				bytes = new byte[www.bytes.Length];
				www.bytes.CopyTo(bytes, 0);
			}
			bool success = string.IsNullOrEmpty(error);
			// www.bytes is null after an HTTP error with an empty body (e.g. a
			// 404); dereferencing it killed the coroutine and the load never
			// finished.
			if (bytes == null || bytes.Length == 0)
			{
				success = false;
				flow.HttpStatus = 204;
			}
			else
			{
				flow.HttpStatus = 200;
			}
			WebBinaryResponseHandler(success, bytes, headers);
		}

		public void LoadBinaryFromWeb()
		{
			flow.IsAlreadyLoadFromWeb = true;
			Dictionary<string, string> etagRequestHeader = GetEtagRequestHeader();
			ThreadPoolManager.Instance.StartCoroutine(LoadBinaryCoroutine(assetManager.GetCpipeUrl(LoadParams.Url), etagRequestHeader));
		}

		public virtual void Destroy()
		{
			MonoSingleton<AssetManager>.Instance.AssetObjectDone(this);
			flow.Destroy();
			flow = null;
			LoadParams = null;
			assetManager = null;
		}

		public bool IsInBundle()
		{
			string text = AssetManager.GetStreamingFilePath(LoadParams.Url);
			if (Application.Platform == 11 && text.EndsWith(".gz"))
			{
				text = text.Remove(text.Length - 3);
			}
			if (assetManager.DoesExist(string.Empty, text))
			{
				return true;
			}
			return false;
		}

		public bool isHeaderStale(Dictionary<string, string> aHeader, DateTime aInsertTime)
		{
			double num = Math.Round((DateTime.Now - aInsertTime).TotalMilliseconds);
			long maxAge = GetMaxAge(aHeader);
			if (maxAge > -1)
			{
				if (num > (double)(maxAge * 1000))
				{
					return true;
				}
				return false;
			}
			string headerEntry = GetHeaderEntry(aHeader, "Expires");
			if (headerEntry != null)
			{
				if (headerEntry == "-1")
				{
					return true;
				}
				try
				{
					DateTime dateTime = DateTime.Parse(headerEntry);
					double num2 = Math.Round((dateTime - DateTime.Now).TotalMilliseconds);
					if (num2 < 0.0)
					{
						return true;
					}
					return false;
				}
				catch (Exception)
				{
				}
			}
			return true;
		}

		public void OnUpdateRecord(object aUserData)
		{
		}

		public void OnAddRecord(object aUserData)
		{
		}

		public void BaseLoadFromBundle(string aType)
		{
			flow.IsAlreadyLoadFromBundle = true;
			string text = AssetManager.GetStreamingFilePath(LoadParams.Url);
			if (Application.Platform == 11 && text.EndsWith(".gz"))
			{
				text = text.Remove(text.Length - 3);
			}
			if (assetManager.DoesExist(string.Empty, text))
			{
				object[] array = new object[1];
				switch (aType)
				{
				case "text":
				{
					string text2 = assetManager.LoadText(string.Empty, text);
					if (!string.IsNullOrEmpty(text2))
					{
						array[0] = text2;
						CallChildCaller(array);
					}
					break;
				}
				case "image":
				{
					Texture2D image = new Texture2D(4, 4, TextureFormat.ARGB4444, false);
					if (assetManager.GetImage(string.Empty, ref image, text))
					{
						array[0] = image;
						CallChildCaller(array);
					}
					break;
				}
				case "path":
					if (!string.IsNullOrEmpty(text))
					{
						array[0] = text;
						CallChildCaller(array);
					}
					break;
				default:
					throw new Exception("no type defined for LoadFromBundle call");
				}
			}
			else
			{
				flow.IsBundled = false;
			}
			flow.UpdateFlow();
		}

		private void SetReponseHeadersInUserData(Dictionary<string, string> aHeaders)
		{
			if (userData is Hashtable)
			{
				Hashtable hashtable = (Hashtable)userData;
				if (hashtable.ContainsKey("responseHeaders"))
				{
					hashtable["responseHeaders"] = aHeaders;
					hashtable["FLOW_HTTPSTATUS"] = flow.HttpStatus.ToString();
				}
			}
		}

		protected virtual void WebTextResponseHandler(bool aIsSuccess, string aBody, Dictionary<string, string> aHeaders)
		{
			if (this == null)
			{
				return;
			}
			SetReponseHeadersInUserData(aHeaders);
			if (!aIsSuccess || string.IsNullOrEmpty(aBody))
			{
				flow.UpdateFlow();
				return;
			}
			if (aHeaders != null)
			{
				header = aHeaders;
			}
			if (flow.HttpStatus == 200)
			{
				string filePath = GetFilePath();
				if (!assetManager.SaveText(string.Empty, aBody, filePath))
				{
					flow.UpdateFlow();
					return;
				}
				if (flow.IsCached)
				{
					Record aRecord = new Record(LoadParams.Sha, header, filePath, -1L, null, LoadParams.CpipeManifestVersion);
					new UpdateRecord(this, aRecord, assetManager.GetDatabaseApiInterface());
				}
				else
				{
					Record aRecord2 = new Record(LoadParams.Sha, header, filePath, 0L, null, LoadParams.CpipeManifestVersion);
					new AddRecord(this, aRecord2, assetManager.GetDatabaseApiInterface());
				}
				CallChildCaller(new object[1] { aBody });
				flow.UpdateFlow();
			}
			else if (flow.HttpStatus == 304)
			{
				flow.UpdateFlow();
			}
			else
			{
				flow.UpdateFlow();
			}
		}

		protected virtual void WebBinaryResponseHandler(bool aIsSuccess, byte[] aBody, Dictionary<string, string> aHeaders, bool aIsReturnPathToAsset = false)
		{
			if (this == null)
			{
				return;
			}
			SetReponseHeadersInUserData(aHeaders);
			if (!aIsSuccess || aBody == null || aBody.Length == 0)
			{
				flow.UpdateFlow();
				return;
			}
			header = aHeaders;
			if (flow.HttpStatus == 200)
			{
				if (!aIsReturnPathToAsset)
				{
					try
					{
						Texture2D texture2D = new Texture2D(4, 4, TextureFormat.ARGB4444, false);
						texture2D.LoadImage(aBody);
						CallChildCaller(new object[1] { texture2D });
					}
					catch (Exception exception)
					{
						Log.Exception(exception);
						flow.UpdateFlow();
						return;
					}
				}
				string filePath = GetFilePath();
				if (!assetManager.SaveBytes(string.Empty, aBody, filePath))
				{
					flow.UpdateFlow();
					return;
				}
				if (flow.IsCached)
				{
					Record aRecord = new Record(LoadParams.Sha, header, filePath, -1L, null, LoadParams.CpipeManifestVersion);
					new UpdateRecord(this, aRecord, assetManager.GetDatabaseApiInterface());
				}
				else
				{
					Record aRecord2 = new Record(LoadParams.Sha, header, filePath, 0L, null, LoadParams.CpipeManifestVersion);
					new AddRecord(this, aRecord2, assetManager.GetDatabaseApiInterface());
				}
				if (aIsReturnPathToAsset)
				{
					CallChildCaller(new object[1] { filePath });
				}
				flow.UpdateFlow();
			}
			else if (flow.HttpStatus == 304)
			{
				flow.UpdateFlow();
			}
			else
			{
				flow.UpdateFlow();
			}
		}
	}
}
