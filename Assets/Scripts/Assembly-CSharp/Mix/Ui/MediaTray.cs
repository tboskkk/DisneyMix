using System;
using System.Collections.Generic;
using Disney.Mix.SDK;
using Mix.Data;
using Mix.DeviceDb;
using Mix.Entitlements;
using UnityEngine;
using UnityEngine.UI;

namespace Mix.Ui
{
	public class MediaTray : BaseMediaTray, ISubSelector<Sticker_Pack>
	{
		public StickerPackSelector PackSelector;

		public BottomNav BottomNav;

		public MediaBar MediaBar;

		public GameObject Tooltip;

		private int? moveDirection;

		private float MediaScrollerHeight;

		private float BottomNavHeight;

		public override void Init(IMediaTray aListener, IChatThread aThread = null)
		{
			base.Init(aListener, aThread);
			MediaScrollerHeight = MediaScroller.GetComponent<RectTransform>().rect.height;
			BottomNavHeight = BottomNav.GetComponent<RectTransform>().rect.height;
		}

		public override void LoadContent(int aDirection)
		{
			moveDirection = aDirection;
			PackSelector.CurrentContent = PackSelector.GetContent(aDirection);
		}

		public void OnApplicationPause(bool goingToBackground)
		{
			if (listener != null)
			{
				listener.OnHidePreviewPanel();
			}
		}

		public void UpdateState(bool aState, string aSelection)
		{
			base.gameObject.SetActive(aState);
			if (aState)
			{
				Populate(aSelection, MiddleContentHolder);
				MediaScroller.Shown();
			}
		}

		public override void ShowPreview(Vector2 aMousePosition)
		{
			foreach (BaseThumb thumbItem in thumbItems)
			{
				GameObject thumb = thumbItem.thumb;
				if (thumb == null)
				{
					continue;
				}
				RectTransform component = thumb.GetComponent<RectTransform>();
				if (!Util.GetRectInScreenSpace(component).Contains(aMousePosition))
				{
					continue;
				}
				if (thumbItem.entitlement != null && !(thumbItem.entitlement is Game))
				{
					listener.OnShowPreviewPanel(thumbItem.entitlement);
				}
				break;
			}
		}

		protected void Populate(string aSelection, Transform aContentHolder)
		{
			Populate(aSelection, aContentHolder, null);
		}

		private void Populate(string aSelection, Transform aContentHolder, Sticker_Pack aStickerPack)
		{
			if (aSelection.Equals("Stickers") && aStickerPack == null)
			{
				return;
			}
			if (aContentHolder.Equals(MiddleContentHolder))
			{
				Clear();
			}
			PackSelector.Setup(aSelection, this);
			if (aSelection.Equals("Stickers") || aSelection.Equals("StickerPacks"))
			{
				MediaScroller.GetComponent<LayoutElement>().minHeight = MediaScrollerHeight;
				BottomNav.gameObject.SetActive(true);
				BottomNav.SearchScrollerVisible(BottomNav.SearchButton.isOn);
			}
			else
			{
				MediaScroller.GetComponent<LayoutElement>().minHeight = MediaScrollerHeight + BottomNavHeight;
				BottomNav.gameObject.SetActive(false);
				BottomNav.SearchScrollerVisible(false);
			}
			if (aSelection.Equals("StickerPacks"))
			{
				ShowTooltip(aSelection);
				return;
			}
			List<BaseContentData> list = null;
			switch (aSelection)
			{
			default:
				return;
			case "Recent":
			{
				List<string> recentItems = Singleton<SettingsManager>.Instance.RecentItems;
				list = new List<BaseContentData>();
				if (recentItems == null)
				{
					break;
				}
				for (int num2 = recentItems.Count - 1; num2 >= 0; num2--)
				{
					BaseContentData myMediaTrayEntitlement = Singleton<EntitlementsManager>.Instance.GetMyMediaTrayEntitlement(recentItems[num2]);
					if (myMediaTrayEntitlement != null && (thread == null || !(thread is IOfficialAccountChatThread) || !(myMediaTrayEntitlement is Gag)))
					{
						list.Add(myMediaTrayEntitlement);
					}
				}
				break;
			}
			case "Stickers":
			{
				list = new List<BaseContentData>();
				List<Sticker> orderedStickersFromPack = Singleton<EntitlementsManager>.Instance.GetOrderedStickersFromPack(aStickerPack);
				for (int num = 0; num < orderedStickersFromPack.Count; num++)
				{
					if (aContentHolder.Equals(MiddleContentHolder) && orderedStickersFromPack[num].GetNew())
					{
						Singleton<MixDocumentCollections>.Instance.contentSeenDocumentCollectionApi.SetContentSeen(orderedStickersFromPack[num].GetUid());
					}
					Singleton<EntitlementsManager>.Instance.UpdateStickerPacksNewStatus();
					Singleton<EntitlementsManager>.Instance.UpdateStickersNewStatus();
					MediaBar.ToggleHighlight(2, Singleton<EntitlementsManager>.Instance.NewStickers || Singleton<EntitlementsManager>.Instance.NewStickerPacks);
					list.Add(orderedStickersFromPack[num]);
				}
				break;
			}
			case "Gags":
				ShowTooltip(aSelection);
				list = Singleton<EntitlementsManager>.Instance.GetMyGags().ConvertAll((Converter<Gag, BaseContentData>)delegate(Gag x)
				{
					if (x.GetNew())
					{
						Singleton<MixDocumentCollections>.Instance.contentSeenDocumentCollectionApi.SetContentSeen(x.GetUid());
					}
					Singleton<EntitlementsManager>.Instance.UpdateGagsNewStatus();
					MediaBar.ToggleHighlight(4, Singleton<EntitlementsManager>.Instance.NewGags);
					return x;
				});
				break;
			case "Games":
				list = Singleton<EntitlementsManager>.Instance.GetMyGames().ConvertAll((Converter<Game, BaseContentData>)delegate(Game x)
				{
					if (x.GetNew())
					{
						Singleton<MixDocumentCollections>.Instance.contentSeenDocumentCollectionApi.SetContentSeen(x.GetUid());
					}
					Singleton<EntitlementsManager>.Instance.UpdateGamesNewStatus();
					MediaBar.ToggleHighlight(3, Singleton<EntitlementsManager>.Instance.NewGames);
					return x;
				});
				break;
			}
			Populate(aContentHolder, list);
		}

		public void Populate(Transform aContentHolder, List<BaseContentData> aCd)
		{
			if (aContentHolder == MiddleContentHolder)
			{
				contentDataMiddle = aCd;
				contentHolderMiddle = aContentHolder;
				stickerIconCountMiddle = 0;
				if (IsInvoking("IconLoader"))
				{
					CancelInvoke("IconLoader");
				}
				InvokeRepeating("IconLoader", 0f, 0.01f);
			}
			else if (aContentHolder == LeftContentHolder)
			{
				contentDataLeft = aCd;
				contentHolderLeft = aContentHolder;
				stickerIconCountLeft = 0;
				if (IsInvoking("IconLoaderLeft"))
				{
					CancelInvoke("IconLoaderLeft");
				}
				InvokeRepeating("IconLoaderLeft", 0f, 0.01f);
			}
			else if (aContentHolder == RightContentHolder)
			{
				contentDataRight = aCd;
				contentHolderRight = aContentHolder;
				stickerIconCountRight = 0;
				if (IsInvoking("IconLoaderRight"))
				{
					CancelInvoke("IconLoaderRight");
				}
				InvokeRepeating("IconLoaderRight", 0f, 0.01f);
			}
		}

		public override void CancelPreview()
		{
			listener.OnHidePreviewPanel();
		}

		private void ShowTooltip(string aType)
		{
			int num = Singleton<MixDocumentCollections>.Instance.keyValDocumentCollectionApi.LoadUserValueAsInt("TraySeen_" + aType);
			if (num < 3)
			{
				Singleton<MixDocumentCollections>.Instance.keyValDocumentCollectionApi.SaveUserValueFromInt("TraySeen_" + aType, ++num);
				Tooltip.SetActive(true);
			}
		}

		protected override void UpdateTooltip(string aMessage)
		{
			Tooltip component = Tooltip.GetComponent<Tooltip>();
			component.SetTooltip(aMessage);
			Tooltip.SetActive(true);
		}

		public void OnSubSelected(Sticker_Pack aStickerPack)
		{
			int? num = moveDirection;
			if (num.HasValue)
			{
				int? num2 = moveDirection;
				if (num2.HasValue && num2.Value < 0)
				{
					MoveContent(MiddleContentHolder, RightContentHolder);
					MoveContent(LeftContentHolder, MiddleContentHolder);
					Populate("Stickers", LeftContentHolder, PackSelector.GetContent(-1));
					listener.OnContentHolderChanged();
					goto IL_016a;
				}
			}
			int? num3 = moveDirection;
			if (num3.HasValue)
			{
				int? num4 = moveDirection;
				if (num4.HasValue && num4.Value > 0)
				{
					MoveContent(MiddleContentHolder, LeftContentHolder);
					MoveContent(RightContentHolder, MiddleContentHolder);
					Populate("Stickers", RightContentHolder, PackSelector.GetContent(1));
					listener.OnContentHolderChanged();
					goto IL_016a;
				}
			}
			PackSelector.CurrentContent = aStickerPack;
			Populate("Stickers", MiddleContentHolder, aStickerPack);
			Populate("Stickers", LeftContentHolder, PackSelector.GetContent(-1));
			Populate("Stickers", RightContentHolder, PackSelector.GetContent(1));
			goto IL_016a;
			IL_016a:
			moveDirection = null;
		}
	}
}
