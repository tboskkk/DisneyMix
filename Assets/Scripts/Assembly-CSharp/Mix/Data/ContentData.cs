using System;
using System.Collections.Generic;
using System.IO;
using Core.MetaData;
using LitJson;
using Mix.Assets;

namespace Mix.Data
{
	public class ContentData : IZipFileAssetObject, IZipJsonAssetObject
	{
		public const string CONTENT_DATA_JSON_ZIP = "contentdata.gz";

		public const string CONTENT_DATA_JOE_ZIP = "contentdata.joe.gz";

		public const string CONTENT_DATA_PATH = "contentdata.txt";

		public const string CONTENT_DATA_JOE_PATH = "contentdata.joe";

		private IContentData Caller;

		private CachePolicy CachePolicy;

		public ContentObj Data;

		public Catalog catalog;

		public ContentData(IContentData aCaller, CachePolicy aPolicy = CachePolicy.DefaultCacheControlProtocol)
		{
			Caller = aCaller;
			CachePolicy = aPolicy;
		}

		void IZipJsonAssetObject.OnZipJsonAssetObject(object aObject, object aUserData)
		{
			if (aObject is ContentObj)
			{
				Data = (ContentObj)aObject;
				bool flag = false;
				try
				{
					if (Data.content.objects.Avatar_Multiplane.Count > 0 && Data.content.objects.Gag.Count > 0 && Data.content.objects.Game.Count > 0 && Data.content.objects.Sticker.Count > 0 && Data.content.objects.Sticker_Tag.Count > 0 && Data.content.objects.Sticker_Pack.Count > 0 && Data.content.objects.Official_Account.Count > 0 && Data.content.objects.Official_Account_Bot.Count > 0)
					{
						flag = true;
					}
				}
				catch (Exception)
				{
				}
				if (flag)
				{
					Caller.OnContentDataDone(null);
					return;
				}
			}
			Caller.OnContentDataDone("Error loading json zip.");
		}

		public void Init()
		{
			Refresh(CachePolicy);
		}

		public void Refresh(CachePolicy aPolicy = CachePolicy.DefaultCacheControlProtocol)
		{
			LoadParams aLoadParams = new LoadParams(AssetManager.GetShaString("contentdata.joe.gz"), "contentdata.joe.gz", aPolicy);
			MonoSingleton<AssetManager>.Instance.LoadFileFromZip("contentdata.joe.gz", "contentdata.joe", this, aLoadParams);
		}

		public void OnZipFileAssetObject(string zipFileBasePath, object userData)
		{
			if (string.IsNullOrEmpty(zipFileBasePath))
			{
				Caller.OnContentDataDone("Error loading JOE data.");
				return;
			}
			// The catalog ships in StreamingAssets. Path.Combine picks the
			// platform's separator; a hard-coded "\\" made Linux and macOS
			// read "StreamingAssets\\contentdata.joe", a file that doesn't exist.
			ParseJoeData(Path.Combine(Application.StreamingAssetsPath, "contentdata.joe"));
			Caller.OnContentDataDone(null);
		}

		private void ParseJoeData(string joeFilePath)
		{
			try
			{
				catalog = new Catalog();
				catalog.PatchData(joeFilePath, null);
				Data = new ContentObj();
				Data.content = new Content();
				Data.content.objects = new Objects();
				Sheet sheet = catalog.GetSheet("Avatar_Multiplane");
				Data.content.objects.Avatar_Multiplane = new List<Avatar_Multiplane>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow in sheet.GetAllRows())
					{
						Data.content.objects.Avatar_Multiplane.Add(new Avatar_Multiplane(sheet, allRow.Value));
					}
				}
				sheet = catalog.GetSheet("Gag");
				Data.content.objects.Gag = new List<Gag>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow2 in sheet.GetAllRows())
					{
						Data.content.objects.Gag.Add(new Gag(sheet, allRow2.Value));
					}
				}
				sheet = catalog.GetSheet("Sticker");
				Data.content.objects.Sticker = new List<Sticker>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow3 in sheet.GetAllRows())
					{
						Data.content.objects.Sticker.Add(new Sticker(sheet, allRow3.Value));
					}
				}
				sheet = catalog.GetSheet("Sticker_Tag");
				Data.content.objects.Sticker_Tag = new List<Sticker_Tag>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow4 in sheet.GetAllRows())
					{
						Data.content.objects.Sticker_Tag.Add(new Sticker_Tag(sheet, allRow4.Value));
					}
				}
				sheet = catalog.GetSheet("Sticker_Pack");
				Data.content.objects.Sticker_Pack = new List<Sticker_Pack>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow5 in sheet.GetAllRows())
					{
						Data.content.objects.Sticker_Pack.Add(new Sticker_Pack(sheet, allRow5.Value));
					}
				}
				sheet = catalog.GetSheet("Game");
				Data.content.objects.Game = new List<Game>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow6 in sheet.GetAllRows())
					{
						Data.content.objects.Game.Add(new Game(sheet, allRow6.Value));
					}
				}
				sheet = catalog.GetSheet("Official_Account");
				Data.content.objects.Official_Account = new List<Official_Account>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow7 in sheet.GetAllRows())
					{
						Data.content.objects.Official_Account.Add(new Official_Account(sheet, allRow7.Value));
					}
				}
				sheet = catalog.GetSheet("Official_Account_Bot");
				Data.content.objects.Official_Account_Bot = new List<Official_Account_Bot>();
				if (sheet != null)
				{
					foreach (KeyValuePair<string, Row> allRow8 in sheet.GetAllRows())
					{
						Data.content.objects.Official_Account_Bot.Add(new Official_Account_Bot(sheet, allRow8.Value));
					}
				}
				Caller.OnContentDataDone(null);
			}
			catch (Exception ex)
			{
				Caller.OnContentDataDone("Error parsing JOE data." + ex.Message);
				DeleteZipCache();
			}
		}

		public object ParseJsonString(string json)
		{
			object result = null;
			try
			{
				DateTime now = DateTime.Now;
				result = JsonMapper.ToObject<ContentObj>(json);
			}
			catch (Exception exception)
			{
				Log.Exception(string.Empty, exception);
				DeleteZipCache();
			}
			return result;
		}

		private void DeleteZipCache()
		{
			string empty = string.Empty;
			empty = AssetManager.GetShaString("contentdata.joe.gz");
			MonoSingleton<AssetManager>.Instance.AssetDatabaseApi.DeleteRecordBySha(empty);
			MonoSingleton<AssetManager>.Instance.FlagRefreshContent();
		}
	}
}
