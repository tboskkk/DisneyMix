using System.Collections.Generic;
using Disney.Mix.SDK;
using Disney.Native;
using Mix.Entitlements;
using Mix.Session.Extensions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mix.Ui
{
	public class MediaBar : MonoBehaviour
	{
		public enum MediaTypes
		{
			Keyboard = 0,
			Media = 1,
			Camera = 2
		}

		public const int CHAT_TOGGLE = 0;

		public const int RECENTS_TOGGLE = 1;

		public const int STICKER_TOGGLE = 2;

		public const int GAME_TOGGLE = 3;

		public const int GAG_TOGGLE = 4;

		public const int UGC_TOGGLE = 5;

		private ToggleGroup toggleGroup;

		private IMediaBar listener;

		private IChatThread thread;

		private bool inited;

		private List<Toggle> toggles = new List<Toggle>();

		private bool disabled;

		private Dictionary<Toggle, UnityAction<bool>> valueChangedCallbacks = new Dictionary<Toggle, UnityAction<bool>>();

		public void Init(IMediaBar aListener, IChatThread aThread)
		{
			listener = aListener;
			thread = aThread;
			ToggleState(false, true);
			inited = false;
			if (thread is IOneOnOneChatThread)
			{
				disabled = !(thread as IOneOnOneChatThread).IsOtherUserFriend();
			}
			else
			{
				disabled = false;
			}
			if (!disabled)
			{
				Invoke("EnableMediaBar", 1f);
			}
		}

		// Disney shipped the Media (camera) tab switched off in the prefab and nothing turns it on.
		// It must be active before Start() collects the toggles (that ignores inactive children).
		public static bool ShowMediaTab(Transform aMediaBar)
		{
			Transform media = aMediaBar.Find("Media");
			if (media == null)
			{
				return false;
			}
			media.gameObject.SetActive(true);
			return true;
		}

		private void Start()
		{
			// `var`: a global-namespace NativeCamera (the plugin) shadows Disney.Native.NativeCamera by name.
			var camera = MonoSingleton<NativeCameraManager>.Instance.Native;
			if (camera != null && camera.IsSupported)
			{
				ShowMediaTab(base.transform);
			}
			toggleGroup = GetComponent<ToggleGroup>();
			Toggle[] toggleArray = base.transform.GetComponentsInChildren<Toggle>();
			for (int i = 0; i < toggleArray.Length; i++)
			{
				if (!disabled)
				{
					int index = i;
					valueChangedCallbacks[toggleArray[i]] = delegate(bool value)
					{
						OnToggleChanged(toggleArray[index], value);
					};
					toggleArray[i].onValueChanged.AddListener(valueChangedCallbacks[toggleArray[i]]);
					toggles.Add(toggleArray[i]);
				}
				else
				{
					toggleArray[i].interactable = false;
				}
			}
			ToggleHighlight(2, Singleton<EntitlementsManager>.Instance.NewStickers || Singleton<EntitlementsManager>.Instance.NewStickerPacks);
			ToggleHighlight(4, Singleton<EntitlementsManager>.Instance.NewGags);
			ToggleHighlight(3, Singleton<EntitlementsManager>.Instance.NewGames);
			if (thread is IOfficialAccountChatThread)
			{
				toggleArray[4].interactable = false;
			}
		}

		private void Update()
		{
			if (MonoSingleton<FakeFriendManager>.Instance.IsFake(thread) && toggles.Count > 5)
			{
				MonoSingleton<FakeFriendManager>.Instance.HighlightAnimator(FakeFriendManager.TYPE_MEDIA_STICKER, toggles[2].GetComponent<Animator>(), toggles[2].isOn);
				MonoSingleton<FakeFriendManager>.Instance.HighlightAnimator(FakeFriendManager.TYPE_MEDIA_GAME, toggles[3].GetComponent<Animator>(), toggles[3].isOn);
				MonoSingleton<FakeFriendManager>.Instance.HighlightAnimator(FakeFriendManager.TYPE_MEDIA_GAG, toggles[4].GetComponent<Animator>(), toggles[4].isOn);
				MonoSingleton<FakeFriendManager>.Instance.HighlightAnimator(FakeFriendManager.TYPE_MEDIA_UGC, toggles[5].GetComponent<Animator>(), toggles[5].isOn);
			}
		}

		public void ToggleHighlight(int aIcon, bool aState)
		{
			if ((aIcon != 4 || !(thread is IOfficialAccountChatThread)) && toggles != null && toggles.Count > aIcon)
			{
				toggles[aIcon].GetComponent<Animator>().enabled = true;
				toggles[aIcon].GetComponent<Animator>().Play((!aState) ? "idle" : "Highlight");
			}
		}

		public void UpdateState(ChatController.UIState aState)
		{
			if (aState.Equals(ChatController.UIState.KeyboardTray))
			{
				base.transform.Find("Keyboard").GetComponent<Toggle>().isOn = true;
			}
			else if (aState.Equals(ChatController.UIState.Default) || aState.Equals(ChatController.UIState.GamePausedTray))
			{
				toggleGroup.SetAllTogglesOff();
			}
			else if (aState.Equals(ChatController.UIState.GameTray) && toggles != null && toggles.Count > 3)
			{
				toggles[3].onValueChanged.RemoveListener(valueChangedCallbacks[toggles[3]]);
				toggles[3].isOn = true;
				toggles[3].onValueChanged.AddListener(valueChangedCallbacks[toggles[3]]);
			}
		}

		public void SetSelectedVisualStateOfToggle(string aSelected, bool isOn = false)
		{
			int num = -1;
			if (aSelected == "Keyboard")
			{
				num = 0;
			}
			if (aSelected == "Recent")
			{
				num = 1;
			}
			if (aSelected == "StickerPacks")
			{
				num = 2;
			}
			if (aSelected == "Games")
			{
				num = 3;
			}
			if (aSelected == "Gags")
			{
				num = 4;
			}
			if (aSelected == "Media")
			{
				num = 5;
			}
			if (num != -1)
			{
				toggles[num].onValueChanged.RemoveListener(valueChangedCallbacks[toggles[num]]);
				toggles[num].isOn = isOn;
				toggles[num].onValueChanged.AddListener(valueChangedCallbacks[toggles[num]]);
			}
		}

		public void ToggleState(bool aState, bool force = false)
		{
			if (inited || force)
			{
				Toggle[] componentsInChildren = base.transform.GetComponentsInChildren<Toggle>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].interactable = aState;
				}
				if (thread is IOfficialAccountChatThread)
				{
					componentsInChildren[4].interactable = false;
				}
			}
		}

		private void EnableMediaBar()
		{
			inited = true;
			ToggleState(true);
		}

		private void OnToggleChanged(Toggle aToggle, bool aState)
		{
			if (aToggle.isOn && aState)
			{
				switch (aToggle.name)
				{
				case "Keyboard":
					listener.OnCategorySelected(ChatController.UIState.KeyboardTray, aToggle.name);
					break;
				case "Recent":
					listener.OnCategorySelected(ChatController.UIState.MediaTray, aToggle.name);
					Analytics.LogViewRecentTray(thread);
					break;
				case "StickerPacks":
					listener.OnCategorySelected(ChatController.UIState.MediaTray, aToggle.name);
					Analytics.LogViewStickersTray(thread);
					break;
				case "Games":
					listener.OnCategorySelected(ChatController.UIState.MediaTray, aToggle.name);
					Analytics.LogViewGamesTray(thread);
					break;
				case "Gags":
					listener.OnCategorySelected(ChatController.UIState.MediaTray, aToggle.name);
					Analytics.LogViewGagsTray(thread);
					break;
				case "Media":
					listener.OnCategorySelected(ChatController.UIState.CameraTray, aToggle.name);
					Analytics.LogViewMediaTray(thread);
					break;
				}
			}
		}
	}
}
