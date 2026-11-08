using System;
using Avatar;
using Disney.Mix.SDK;
using Disney.MobileNetwork;
using Mix.AssetBundles;
using Mix.Assets;
using Mix.Avatar;
using Mix.Data;
using Mix.Entitlements;
using Mix.Session.Extensions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mix.Ui
{
	public class MessageLocalNotification : BaseNofitication, IBundleObject
	{
		public IChatThread FromThread;

		public IChatThread ToThread;

		public IChatMessage ChatMessage;

		public Action<IChatThread, IChatThread> Callback;

		private Transform imageTarget;

		private bool isIconLoaded;

		private GameObject instance;

		public MessageLocalNotification(IChatThread aFromThread, IChatThread aToThread, IChatMessage aMessage, Action<IChatThread, IChatThread> aCallback)
		{
			FromThread = aFromThread;
			ToThread = aToThread;
			ChatMessage = aMessage;
			Callback = aCallback;
		}

		void IBundleObject.OnBundleAssetObject(UnityEngine.Object aGameObject, object aUserData)
		{
			if (instance.IsNullOrDisposed())
			{
				return;
			}
			GameObject gameObject = MonoSingleton<AssetManager>.Instance.GetBundleInstance((string)aUserData) as GameObject;
			if (gameObject != null)
			{
				isIconLoaded = true;
				Image component = gameObject.GetComponent<Image>();
				if (component != null && imageTarget != null)
				{
					imageTarget.gameObject.SetActive(true);
					imageTarget.GetComponent<Image>().sprite = component.sprite;
				}
				UnityEngine.Object.Destroy(gameObject);
			}
		}

		public override GameObject GenerateGameObject()
		{
			if (ChatMessage is IStickerMessage)
			{
				string contentId = ((IStickerMessage)ChatMessage).ContentId;
				Sticker stickerData = Singleton<EntitlementsManager>.Instance.GetStickerData(contentId);
				if (stickerData != null && stickerData.GetStickerType() == "drop")
				{
					new LocalNotificationAudioDrop(stickerData);
				}
			}
			GameObject original = Resources.Load<GameObject>("Prefabs/Ui/NotificationPanel");
			instance = UnityEngine.Object.Instantiate(original);
			Text component = instance.transform.Find("Background/ChatText").GetComponent<Text>();
			GameObject gameObject = instance.transform.Find("Background/AvatarHead").gameObject;
			Text component2 = instance.transform.Find("Background/DisplayNameText").GetComponent<Text>();
			Button gotoChatButton = instance.transform.Find("Background").GetComponent<Button>();
			UnityAction listener = null;
			listener = delegate
			{
				gotoChatButton.GetComponent<Button>().onClick.RemoveListener(listener);
				Callback(FromThread, ToThread);
				Analytics.LogLocalNotificationClick(ChatMessage);
			};
			gotoChatButton.GetComponent<Button>().onClick.AddListener(listener);
			component.text = MessageUtils.GetDisplayTextForMessage(ToThread, ChatMessage);
			component2.text = ToThread.GetNickOrDisplayById(ChatMessage.SenderId);
			IAvatarHolder avatarHolderFromId = ToThread.GetAvatarHolderFromId(ChatMessage.SenderId);
			string name = ((!MonoSingleton<AvatarManager>.Instance.AvatarHasGeo(avatarHolderFromId.Avatar)) ? "ImageTarget" : "ImageTarget_Geo");
			imageTarget = gameObject.transform.Find(name);
			int size = (int)imageTarget.GetComponent<RectTransform>().rect.height;
			if (ToThread is IOfficialAccountChatThread)
			{
				string iconOfBotOrOA = Singleton<EntitlementsManager>.Instance.GetIconOfBotOrOA(ChatMessage.SenderId);
				if (!string.IsNullOrEmpty(iconOfBotOrOA))
				{
					MonoSingleton<AssetManager>.Instance.LoadABundle(this, iconOfBotOrOA, iconOfBotOrOA, string.Empty, true, false, true);
				}
			}
			else
			{
				MonoSingleton<AvatarManager>.Instance.GetSnapshotFromDna(avatarHolderFromId.Avatar, (AvatarFlags)0, size, delegate(bool aSuccess, Sprite aSprite)
				{
					if (aSprite != null && imageTarget != null && imageTarget.GetComponent<Image>() != null)
					{
						imageTarget.GetComponent<Image>().sprite = aSprite;
						imageTarget.gameObject.SetActive(true);
					}
				});
			}
			Service.Get<SystemTextManager>().GenerateSystemText(component, new Color32(0, 0, 0, 0));
			return instance;
		}

		public override void Destroy()
		{
			if (MonoSingleton<AssetManager>.Instance != null && isIconLoaded)
			{
				MonoSingleton<AssetManager>.Instance.DestroyBundleInstance(Singleton<EntitlementsManager>.Instance.GetIconOfBotOrOA(ChatMessage.SenderId));
				isIconLoaded = false;
			}
		}
	}
}
