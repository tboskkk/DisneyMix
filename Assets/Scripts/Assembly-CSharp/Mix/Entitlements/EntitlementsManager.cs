using System;
using System.Collections.Generic;
using System.Linq;
using Mix.Assets;
using Mix.Data;
using Mix.DeviceDb;
using Mix.Localization;

namespace Mix.Entitlements
{
	public class EntitlementsManager : Singleton<EntitlementsManager>, IContentData
	{
		public const string STICKER_TYPE_DROP = "drop";

		public const string STICKER_TYPE_ANIMATED = "animated";

		public const string STICKER_TYPE_AUDIO = "audio";

		public const string STICKER_TYPE_STATIC = "static";

		public const string STICKER_TYPE_SEARCH = "search";

		public const string LockableItemStoragePrefix = "LI_";

		private List<string> ClientLaunchGameIds = new List<string> { "b8e1421305639370d6c8501b080f7a41", "f9d53d6c28d5f83727cbb01bb788552a", "fa93ab5d98dfc38b1bc5a701a5b5feb7", "f885bc3506fc7b08050d1c88e7732a45", "4fdaaedbd60a4e6edc07bf2384e24c87", "33e2c1073979f09f866ff343ea762d2f", "96b48cdfa6bb6d41d0658d6c967f2222", "5f4268892a07d81479f68a35ce826d9d", "c10650e60b9d6ec3617baeb29bd5b130" };

		private Dictionary<string, List<string>> RegionalizedGames = new Dictionary<string, List<string>> { 
		{
			"fa93ab5d98dfc38b1bc5a701a5b5feb7",
			new List<string> { "en_US" }
		} };

		private CpipeEnvironment Environment;

		private bool IsEnvCalled;

		private List<string> UnlockedUids = new List<string>();

		private List<string> Inventory;

		private bool IsStickerPacksDirty = true;

		private bool IsStickersDirty = true;

		private bool IsStickerTagsDirty = true;

		private bool IsGagsDirty = true;

		private bool IsGamesDirty = true;

		private bool IsAvatarComponentsDirty = true;

		private bool IsOfficialAccountsDirty = true;

		private bool IsOfficialAccountBotsDirty = true;

		private List<Sticker_Pack> MyStickerPacks;

		private List<Sticker> MyStickers;

		private List<Gag> MyGags;

		private List<Game> MyGames;

		private List<Avatar_Multiplane> MyAvatarComponents;

		private List<Sticker_Pack> AllStickerPacks;

		private List<Sticker> AllStickers;

		private List<Sticker_Tag> AllStickerTags;

		private List<Gag> AllGags;

		private List<Game> AllGames;

		private List<Avatar_Multiplane> AllAvatarComponents;

		private List<Official_Account> AllOfficialAccounts;

		private List<Official_Account_Bot> AllOfficialAccountBots;

		private bool IsLoadingContentData;

		private ContentData contentData;

		private ContentData RefreshContentData;

		private List<IEntitlementsManager> callbacks = new List<IEntitlementsManager>();

		public bool NewGags;

		public bool NewGames;

		public bool NewStickerPacks;

		public bool NewStickers;

		public bool NewOfficialAccounts;

		public List<TagMap> StickerSearchList { get; private set; }

		void IContentData.OnContentDataDone(string aErrorMessage)
		{
			if (aErrorMessage == null)
			{
				if (RefreshContentData != null)
				{
					contentData = RefreshContentData;
					RefreshContentData = null;
					RefreshInventoy();
					CallCallbacks(true);
				}
				else
				{
					CallCallbacks(false);
				}
			}
			else
			{
				Log.Exception(aErrorMessage);
				CallCallbacks(false);
			}
			IsLoadingContentData = false;
		}

		private void CallCallbacks(bool IsLoadSuccessful)
		{
			try
			{
				foreach (IEntitlementsManager callback in callbacks)
				{
					if (callback != null)
					{
						callback.OnEntitlementsManagerReady(IsLoadSuccessful);
					}
				}
			}
			catch (Exception exception)
			{
				Log.Exception(string.Empty, exception);
			}
			callbacks.Clear();
		}

		public void LoadNewContentData(IEntitlementsManager aCaller, CachePolicy aPolicy = CachePolicy.DefaultCacheControlProtocol)
		{
			callbacks.Add(aCaller);
			if (!IsLoadingContentData)
			{
				IsLoadingContentData = true;
				RefreshContentData = new ContentData(this, aPolicy);
				RefreshContentData.Init();
			}
		}

		private void LoadNewContentData()
		{
			IsLoadingContentData = true;
			RefreshContentData = new ContentData(this);
			RefreshContentData.Init();
		}

		public void RefreshInventoy()
		{
			IsStickerPacksDirty = true;
			IsStickersDirty = true;
			IsGagsDirty = true;
			IsGamesDirty = true;
			IsAvatarComponentsDirty = true;
			IsOfficialAccountsDirty = true;
			IsOfficialAccountBotsDirty = true;
			IsStickerTagsDirty = true;
			GenerateInventory();
		}

		public void GenerateInventory()
		{
			Inventory = new List<string>();
			GetAllStickerPacks();
			GetAllStickers();
			GetAllStickerTags();
			GetAllGags();
			GetAllGames();
			GetAllAvatarComponents();
			GetAllOfficialAccounts();
			GetAllOfficialAccountBots();
			if (MyStickerPacks != null)
			{
				foreach (Sticker_Pack myStickerPack in MyStickerPacks)
				{
					Inventory.Add(myStickerPack.GetUid());
				}
			}
			if (MyStickers != null)
			{
				foreach (Sticker mySticker in MyStickers)
				{
					Inventory.Add(mySticker.GetUid());
				}
			}
			if (MyGags != null)
			{
				foreach (Gag myGag in MyGags)
				{
					Inventory.Add(myGag.GetUid());
				}
			}
			if (MyGames != null)
			{
				foreach (Game myGame in MyGames)
				{
					Inventory.Add(myGame.GetUid());
				}
			}
			if (MyAvatarComponents == null)
			{
				return;
			}
			foreach (Avatar_Multiplane myAvatarComponent in MyAvatarComponents)
			{
				Inventory.Add(myAvatarComponent.GetUid());
			}
		}

		public void LocalizeStickerSearch()
		{
			StickerSearchList = new List<TagMap>();
			if (AllStickerTags == null)
			{
				return;
			}
			List<TagMap> list = new List<TagMap>();
			for (int i = 0; i < AllStickerTags.Count; i++)
			{
				Sticker_Tag sticker_Tag = AllStickerTags[i];
				string uid = sticker_Tag.GetUid();
				string tagMap = sticker_Tag.GetTagMap();
				string aToken = "ContentData.Sticker_Tag." + uid.Replace("'", "''") + ".SHA";
				string text = Singleton<Localizer>.Instance.getString(aToken);
				if (!string.IsNullOrEmpty(text) && !(text == Localizer.NO_TOKEN))
				{
					list.Add(new TagMap(uid, tagMap, sticker_Tag.GetOrder()));
				}
			}
			StickerSearchList = list.OrderBy((TagMap o) => o.order).ToList();
		}

		public bool IsEntitlementIdInInventory(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return false;
			}
			if (Inventory == null)
			{
				GenerateInventory();
			}
			return Inventory.Contains(id);
		}

		public bool IsUnlocked(string id)
		{
			return UnlockedUids.Contains(id);
		}

		public void UnlockItem(string id)
		{
		}

		public bool DoesUserOwnItem(BaseContentData item)
		{
			if (!item.GetHidden() && (!item.GetUnlockable() || IsUnlocked(item.GetUid())))
			{
				return true;
			}
			return false;
		}

		public bool ShouldShowItem(BaseContentData data)
		{
			if (!IsEnvCalled)
			{
				IsEnvCalled = true;
				Environment = MonoSingleton<AssetManager>.Instance.GetEnvironment();
			}
			if (Environment == CpipeEnvironment.Dev)
			{
				return true;
			}
			return !data.GetPreview();
		}

		public void SetVisibleAndOwnedContent<T>(List<T> aSource, ref List<T> aVisibleList, ref List<T> aMyList) where T : BaseContentData
		{
			if (aSource == null)
			{
				return;
			}
			foreach (T item in aSource)
			{
				if (ShouldShowItem(item))
				{
					aVisibleList.Add((T)item);
					if (DoesUserOwnItem(item))
					{
						aMyList.Add((T)item);
					}
				}
			}
			aVisibleList = aVisibleList.OrderBy((T o) => o.GetOrder()).ToList();
			aMyList = aMyList.OrderBy((T o) => o.GetOrder()).ToList();
		}

		public Sticker_Pack GetStickerPackData(string aId)
		{
			if (string.IsNullOrEmpty(aId) || contentData == null)
			{
				return null;
			}
			GetAllStickerPacks();
			if (AllStickerPacks == null)
			{
				return null;
			}
			foreach (Sticker_Pack allStickerPack in AllStickerPacks)
			{
				if (allStickerPack.GetUid().Equals(aId))
				{
					return allStickerPack;
				}
			}
			return null;
		}

		public List<Sticker_Pack> GetMyStickerPacks()
		{
			RefreshStickerPacks();
			return MyStickerPacks;
		}

		public List<Sticker_Pack> GetAllStickerPacks()
		{
			RefreshStickerPacks();
			return AllStickerPacks;
		}

		public bool IsStickerPackNew(Sticker_Pack aPack)
		{
			List<Sticker> list = GetOrderedStickersFromPack(aPack).FindAll((Sticker x) => x.GetNew());
			if (list == null || list.Count <= 0)
			{
				return false;
			}
			return GetNewableState(list);
		}

		public void UpdateStickerPacksNewStatus()
		{
			NewStickerPacks = GetNewableState(MyStickerPacks);
		}

		private void RefreshStickerPacks()
		{
			if (contentData != null && (AllStickerPacks == null || IsStickerPacksDirty))
			{
				IsStickerPacksDirty = false;
				List<Sticker_Pack> sticker_Pack = contentData.Data.content.objects.Sticker_Pack;
				AllStickerPacks = new List<Sticker_Pack>();
				MyStickerPacks = new List<Sticker_Pack>();
				if (sticker_Pack != null)
				{
					// Packs with no available sticker are hidden (RefreshStickers
					// already dropped the unavailable stickers).
					RefreshStickers();
					List<Sticker_Pack> availablePacks = sticker_Pack.FindAll((Sticker_Pack pack) => HasAvailableSticker(pack));
					SetVisibleAndOwnedContent(availablePacks, ref AllStickerPacks, ref MyStickerPacks);
					UpdateStickerPacksNewStatus();
				}
			}
		}

		// Source builds only have some of Disney's content: a pack shows only if
		// at least one of its stickers has a bundle (AllStickers is already
		// filtered by AssetManager.IsBundleAvailable).
		private bool HasAvailableSticker(Sticker_Pack aPack)
		{
			string[] stickers = aPack.GetStickers();
			if (stickers == null || AllStickers == null)
			{
				return false;
			}
			foreach (string aId in stickers)
			{
				if (AllStickers.Exists((Sticker x) => x.GetUid() == aId))
				{
					return true;
				}
			}
			return false;
		}

		public Sticker GetStickerData(string aId)
		{
			if (string.IsNullOrEmpty(aId) || contentData == null)
			{
				return null;
			}
			GetAllStickers();
			if (AllStickers == null)
			{
				return null;
			}
			foreach (Sticker allSticker in AllStickers)
			{
				if (allSticker.GetUid().Equals(aId))
				{
					return allSticker;
				}
			}
			return null;
		}

		public List<Sticker> GetOrderedStickersFromPack(Sticker_Pack aPack)
		{
			GetAllStickerPacks();
			GetAllStickers();
			if (MyStickers == null || MyStickerPacks == null)
			{
				return new List<Sticker>();
			}
			string[] stickers = aPack.GetStickers();
			List<Sticker> list = new List<Sticker>();
			string[] array = stickers;
			foreach (string aId in array)
			{
				Sticker stickerData = GetStickerData(aId);
				if (stickerData != null && MyStickers.Contains(stickerData))
				{
					list.Add(stickerData);
				}
			}
			return list.OrderBy((Sticker o) => o.GetOrder()).ToList();
		}

		public List<Sticker> GetAllStickers()
		{
			RefreshStickers();
			return AllStickers;
		}

		public void UpdateStickersNewStatus()
		{
			NewStickers = GetNewableState(MyStickers);
		}

		private void RefreshStickers()
		{
			if (contentData != null && (AllStickers == null || IsStickersDirty))
			{
				IsStickersDirty = false;
				List<Sticker> sticker = contentData.Data.content.objects.Sticker;
				AllStickers = new List<Sticker>();
				MyStickers = new List<Sticker>();
				if (sticker != null)
				{
					SetVisibleAndOwnedContent(sticker.FindAll((Sticker x) => MonoSingleton<AssetManager>.Instance.IsBundleAvailable(x.GetHd())), ref AllStickers, ref MyStickers);
					UpdateStickersNewStatus();
				}
			}
		}

		public List<Sticker_Tag> GetAllStickerTags()
		{
			RefreshStickerTags();
			LocalizeStickerSearch();
			return AllStickerTags;
		}

		private void RefreshStickerTags()
		{
			if (contentData != null && (AllStickerTags == null || IsStickerTagsDirty))
			{
				IsStickerTagsDirty = false;
				AllStickerTags = contentData.Data.content.objects.Sticker_Tag;
			}
		}

		public List<Gag> GetMyBookEnds()
		{
			if (contentData == null)
			{
				return null;
			}
			List<Gag> list = new List<Gag>();
			GetAllGags();
			if (AllGags == null)
			{
				return null;
			}
			foreach (Gag allGag in AllGags)
			{
				if (!string.IsNullOrEmpty(allGag.GetGagType()) && allGag.GetGagType().Contains("bookend"))
				{
					list.Add(allGag);
				}
			}
			return list;
		}

		public List<Gag> GetAllGags()
		{
			RefreshGags();
			return AllGags;
		}

		public List<Gag> GetMyGags()
		{
			RefreshGags();
			return MyGags;
		}

		public void UpdateGagsNewStatus()
		{
			NewGags = GetNewableState(MyGags);
		}

		private void RefreshGags()
		{
			if (contentData != null && (AllGags == null || IsGagsDirty))
			{
				IsGagsDirty = false;
				List<Gag> gag = contentData.Data.content.objects.Gag;
				AllGags = new List<Gag>();
				MyGags = new List<Gag>();
				if (gag != null)
				{
					SetVisibleAndOwnedContent(gag.FindAll((Gag x) => MonoSingleton<AssetManager>.Instance.IsBundleAvailable(x.GetHd())), ref AllGags, ref MyGags);
					UpdateGagsNewStatus();
				}
			}
		}

		public Gag GetGagData(string aId)
		{
			if (string.IsNullOrEmpty(aId) || contentData == null)
			{
				return null;
			}
			GetAllGags();
			if (AllGags == null)
			{
				return null;
			}
			foreach (Gag allGag in AllGags)
			{
				if (allGag.GetUid().Equals(aId))
				{
					return allGag;
				}
			}
			return null;
		}

		public BaseGameData GetGameData(string aId)
		{
			GetAllGames();
			if (AllGames == null)
			{
				return null;
			}
			foreach (Game allGame in AllGames)
			{
				if (allGame.GetUid().Equals(aId))
				{
					return allGame;
				}
			}
			return null;
		}

		public List<Game> GetMyGames()
		{
			RefreshGames();
			return MyGames;
		}

		public List<Game> GetAllGames()
		{
			RefreshGames();
			return AllGames;
		}

		public void UpdateGamesNewStatus()
		{
			NewGames = GetNewableState(MyGames);
		}

		public void RefreshGames()
		{
			if (contentData != null && (AllGames == null || IsGamesDirty))
			{
				IsGamesDirty = false;
				List<Game> game = contentData.Data.content.objects.Game;
				AllGames = new List<Game>();
				MyGames = new List<Game>();
				game = FilterGames(game);
				if (game != null)
				{
					SetVisibleAndOwnedContent(game, ref AllGames, ref MyGames);
					UpdateGamesNewStatus();
				}
			}
		}

		private bool IsGameAvailableToRegion(string aUid)
		{
			string locale = Localizer.GetLocale();
			if (RegionalizedGames.ContainsKey(aUid))
			{
				List<string> list = RegionalizedGames[aUid];
				if (!list.Contains(locale))
				{
					return false;
				}
			}
			return true;
		}

		private List<Game> FilterGames(List<Game> aGames)
		{
			List<Game> list = new List<Game>();
			if (Environment == CpipeEnvironment.Dev)
			{
				return aGames;
			}
			foreach (Game aGame in aGames)
			{
				if (ClientLaunchGameIds.Contains(aGame.GetUid()) && IsGameAvailableToRegion(aGame.GetUid()))
				{
					list.Add(aGame);
				}
			}
			return list;
		}

		public Avatar_Multiplane GetAvatarData(string aId)
		{
			RefreshAvatarComponents();
			if (AllAvatarComponents == null)
			{
				return null;
			}
			foreach (Avatar_Multiplane allAvatarComponent in AllAvatarComponents)
			{
				if (allAvatarComponent.GetUid().Equals(aId))
				{
					return allAvatarComponent;
				}
			}
			return null;
		}

		public Avatar_Multiplane GetAvatarByReferenceId(string refId)
		{
			RefreshAvatarComponents();
			int num = 0;
			try
			{
				num = int.Parse(refId);
			}
			catch (Exception)
			{
				return null;
			}
			if (AllAvatarComponents == null)
			{
				return null;
			}
			foreach (Avatar_Multiplane allAvatarComponent in AllAvatarComponents)
			{
				if (allAvatarComponent.GetReferenceId() == num)
				{
					return allAvatarComponent;
				}
			}
			return null;
		}

		public List<Avatar_Multiplane> GetMyAvatarDataByCategory(string category)
		{
			return GetAvatarDataByCategory(category, MyAvatarComponents);
		}

		public List<Avatar_Multiplane> GetAllAvatarDataByCategory(string category)
		{
			return GetAvatarDataByCategory(category, AllAvatarComponents);
		}

		private List<Avatar_Multiplane> GetAvatarDataByCategory(string category, List<Avatar_Multiplane> array)
		{
			RefreshAvatarComponents();
			if (array == null)
			{
				return null;
			}
			List<Avatar_Multiplane> list = new List<Avatar_Multiplane>();
			foreach (Avatar_Multiplane item in array)
			{
				if (item.GetCategory().Equals(category))
				{
					list.Add(item);
				}
			}
			return list;
		}

		public List<Avatar_Multiplane> GetMyAvatarComponents()
		{
			RefreshAvatarComponents();
			return MyAvatarComponents;
		}

		public List<Avatar_Multiplane> GetAllAvatarComponents()
		{
			RefreshAvatarComponents();
			return AllAvatarComponents;
		}

		public bool IsAvatarCategoryNew(string aCategory)
		{
			List<Avatar_Multiplane> list = GetMyAvatarDataByCategory(aCategory).FindAll((Avatar_Multiplane x) => x.GetNew());
			if (list == null || list.Count <= 0)
			{
				return false;
			}
			return GetNewableState(list);
		}

		private void RefreshAvatarComponents()
		{
			if (contentData != null && (AllAvatarComponents == null || IsAvatarComponentsDirty))
			{
				IsAvatarComponentsDirty = false;
				List<Avatar_Multiplane> list = contentData.Data.content.objects.Avatar_Multiplane.Cast<Avatar_Multiplane>().ToList();
				AllAvatarComponents = new List<Avatar_Multiplane>();
				MyAvatarComponents = new List<Avatar_Multiplane>();
				if (list != null)
				{
					SetVisibleAndOwnedContent(list, ref AllAvatarComponents, ref MyAvatarComponents);
				}
			}
		}

		public List<Avatar_Multiplane> GetAllAvatars()
		{
			return GetAllAvatarComponents();
		}

		public bool AreAvatarsLoaded()
		{
			return true;
		}

		public void UpdateOfficialAccountNewStatus()
		{
			NewOfficialAccounts = GetNewableState(AllOfficialAccounts);
		}

		public Official_Account GetOfficialAccount(string aOaid)
		{
			GetAllOfficialAccounts();
			if (AllOfficialAccounts == null)
			{
				return null;
			}
			foreach (Official_Account allOfficialAccount in AllOfficialAccounts)
			{
				if (allOfficialAccount.GetOAID().Equals(aOaid))
				{
					return allOfficialAccount;
				}
			}
			return null;
		}

		public List<Official_Account> GetAllOfficialAccounts()
		{
			RefreshOfficialAccounts();
			return AllOfficialAccounts;
		}

		private void RefreshOfficialAccounts()
		{
			if (contentData == null || (AllOfficialAccounts != null && !IsOfficialAccountsDirty))
			{
				return;
			}
			IsOfficialAccountsDirty = false;
			List<Official_Account> official_Account = contentData.Data.content.objects.Official_Account;
			AllOfficialAccounts = new List<Official_Account>();
			if (official_Account != null)
			{
				AllOfficialAccounts = official_Account;
				AllOfficialAccounts = AllOfficialAccounts.OrderBy((Official_Account o) => o.GetOrder()).ToList();
			}
			UpdateOfficialAccountNewStatus();
		}

		public string GetIconOfBotOrOA(string aBotOrOAId)
		{
			Official_Account_Bot officialAccountBot = GetOfficialAccountBot(aBotOrOAId);
			if (officialAccountBot != null)
			{
				return officialAccountBot.GetIcon();
			}
			Official_Account officialAccount = GetOfficialAccount(aBotOrOAId);
			if (officialAccount != null)
			{
				return officialAccount.GetIcon();
			}
			return null;
		}

		public Official_Account_Bot GetOfficialAccountBot(string aBotId)
		{
			GetAllOfficialAccountBots();
			if (AllOfficialAccountBots == null)
			{
				return null;
			}
			foreach (Official_Account_Bot allOfficialAccountBot in AllOfficialAccountBots)
			{
				if (allOfficialAccountBot.GetBotId().Equals(aBotId))
				{
					return allOfficialAccountBot;
				}
			}
			return null;
		}

		public List<Official_Account_Bot> GetAllOfficialAccountBots()
		{
			RefreshOfficialAccountBots();
			return AllOfficialAccountBots;
		}

		private void RefreshOfficialAccountBots()
		{
			if (contentData != null && (AllOfficialAccountBots == null || IsOfficialAccountBotsDirty))
			{
				IsOfficialAccountBotsDirty = false;
				List<Official_Account_Bot> official_Account_Bot = contentData.Data.content.objects.Official_Account_Bot;
				AllOfficialAccountBots = new List<Official_Account_Bot>();
				if (official_Account_Bot != null)
				{
					AllOfficialAccountBots = official_Account_Bot;
				}
			}
		}

		private bool GetNewableState<T>(List<T> aItems) where T : BaseContentData
		{
			foreach (T aItem in aItems)
			{
				T current = aItem;
				if (current.GetNew() && !Singleton<MixDocumentCollections>.Instance.contentSeenDocumentCollectionApi.IsContentSeen(current.GetUid()))
				{
					return true;
				}
			}
			return false;
		}

		public void LoadMyEntitlements()
		{
			if (!IsLoadingContentData)
			{
				LoadNewContentData();
			}
		}

		public void LoadContentData()
		{
			if (!IsLoadingContentData)
			{
				LoadNewContentData();
			}
		}

		public BaseContentData GetMyMediaTrayEntitlement(string entitlementId)
		{
			BaseContentData gagData = GetGagData(entitlementId);
			if (gagData != null)
			{
				return gagData;
			}
			gagData = GetStickerData(entitlementId);
			if (gagData != null)
			{
				return gagData;
			}
			gagData = GetGameData(entitlementId);
			if (gagData != null)
			{
				return gagData;
			}
			return null;
		}

		public bool DoesEntitlementExist(string entitlementId)
		{
			BaseContentData gagData = GetGagData(entitlementId);
			if (gagData != null)
			{
				return true;
			}
			gagData = GetStickerPackData(entitlementId);
			if (gagData != null)
			{
				return true;
			}
			gagData = GetStickerData(entitlementId);
			if (gagData != null)
			{
				return true;
			}
			gagData = GetGameData(entitlementId);
			if (gagData != null)
			{
				return true;
			}
			gagData = GetAvatarData(entitlementId);
			if (gagData != null)
			{
				return true;
			}
			return false;
		}
	}
}
