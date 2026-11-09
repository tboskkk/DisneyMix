using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avatar;
using Disney.Mix.SDK;
using Disney.MobileNetwork;
using Disney.Native;
using LitJson;
using Mix.AssetBundles;
using Mix.Assets;
using Mix.Avatar;
using Mix.Connectivity;
using Mix.Data;
using Mix.Entitlements;
using Mix.GagManagement;
using Mix.Games;
using Mix.Games.Data;
using Mix.Games.Message;
using Mix.Native;
using Mix.Session;
using Mix.Session.Extensions;
using Mix.Session.Local.Thread;
using Mix.Tracking;
using Mix.Ui.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mix.Ui
{
	public class ChatController : BaseController, INativeCamera, IGameTray, IBundleObject, IChatBar, IGagSendTray, IGroupOptionsPanel, IShowNotification, IMediaBar, IMediaPreview, IMediaTray
	{
		public enum UIState
		{
			Default = 0,
			AnimateDown = 1,
			MediaTray = 2,
			KeyboardTray = 3,
			GameTray = 4,
			CameraTray = 5,
			GagSendTray = 6,
			GamePausedTray = 7
		}

		private sealed class SendGameStateMessage_003Ec__AnonStorey293
		{
			internal Action<object> callback;

			internal IGameStateMessage localMessage;

			internal Action<ISendGameStateMessageResult> newCallback;

			internal ChatController _003C_003Ef__this;

			internal void _003C_003Em__574(ISendGameStateMessageResult o)
			{
				callback(o);
			}

			internal void _003C_003Em__575(ISendGameStateMessageResult result)
			{
				if (!_003C_003Ef__this.IsNullOrDisposed() && !(_003C_003Ef__this.gameObject == null) && _003C_003Ef__this.chatThread != null && result != null)
				{
					if (!result.Success)
					{
						_003C_003Ef__this.MessageUpdateInternal(localMessage);
					}
					else
					{
						_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					}
					newCallback(result);
				}
			}

			internal void _003C_003Em__576(ISendGameStateMessageResult result)
			{
				if (!_003C_003Ef__this.IsNullOrDisposed() && !(_003C_003Ef__this.gameObject == null) && _003C_003Ef__this.chatThread != null && result != null)
				{
					if (!result.Success)
					{
						_003C_003Ef__this.MessageUpdateInternal(localMessage);
					}
					else
					{
						_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					}
					newCallback(result);
				}
			}

			internal void _003C_003Em__577(ISendGameStateMessageResult result)
			{
				if (!_003C_003Ef__this.IsNullOrDisposed() && !(_003C_003Ef__this.gameObject == null) && _003C_003Ef__this.chatThread != null && result != null)
				{
					if (!result.Success)
					{
						_003C_003Ef__this.MessageUpdateInternal(localMessage);
					}
					else
					{
						_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					}
					newCallback(result);
				}
			}
		}

		private sealed class UpdateGameStateMessage_003Ec__AnonStorey294
		{
			internal Action<object> callback;

			internal void _003C_003Em__578(IUpdateGameStateMessageResult o)
			{
				callback(o);
			}
		}

		private sealed class OnSendTextMessage_003Ec__AnonStorey295
		{
			internal string aMessage;

			internal ITextMessage localMessage;

			internal ChatController _003C_003Ef__this;

			internal void _003C_003Em__579(ISendTextMessageResult result)
			{
				if (_003C_003Ef__this.IsNullOrDisposed() || _003C_003Ef__this.chatThread == null)
				{
					return;
				}
				if (result == null || !result.Success)
				{
					_003C_003Ef__this.MessageUpdateInternal(localMessage);
				}
				else if (result != null)
				{
					_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					if (result.IsModerated)
					{
						Analytics.LogChatModerated(_003C_003Ef__this.chatThread);
					}
				}
			}
		}

		private sealed class OnSendGroupGag_003Ec__AnonStorey296
		{
			internal IGagMessage localMessage;

			internal Gag aGag;

			internal ChatController _003C_003Ef__this;

			internal void _003C_003Em__57A(ISendGagMessageResult result)
			{
				if (!_003C_003Ef__this.IsNullOrDisposed() && _003C_003Ef__this.chatThread != null && localMessage != null)
				{
					if (result == null || !result.Success)
					{
						_003C_003Ef__this.MessageUpdateInternal(localMessage);
					}
					else if (result != null)
					{
						_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					}
				}
			}
		}

		private sealed class SendVideo_003Ec__AnonStorey297
		{
			internal IVideoMessage localMessage;

			internal ChatController _003C_003Ef__this;

			internal void _003C_003Em__57B(ISendVideoMessageResult result)
			{
				if (!_003C_003Ef__this.IsNullOrDisposed() && _003C_003Ef__this.chatThread != null)
				{
					if (result == null || !result.Success)
					{
						_003C_003Ef__this.MessageUpdateInternal(localMessage);
					}
					else if (result != null)
					{
						_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					}
				}
			}
		}

		private sealed class SendPhoto_003Ec__AnonStorey298
		{
			internal IPhotoMessage localMessage;

			internal ChatController _003C_003Ef__this;

			internal void _003C_003Em__57C(ISendPhotoMessageResult result)
			{
				if (!_003C_003Ef__this.IsNullOrDisposed() && _003C_003Ef__this.chatThread != null)
				{
					if (result == null || !result.Success)
					{
						_003C_003Ef__this.MessageUpdateInternal(localMessage);
					}
					else if (result != null)
					{
						_003C_003Ef__this.CheckAndAddTimestampForMessage(localMessage);
					}
				}
			}
		}

		private const int chatRetrievalChunk = 50;

		public static EventHandler<ChatThreadEnterEventArgs> OnChatThreadEnter;

		public static EventHandler<ChatThreadExitEventArgs> OnChatThreadExit;

		public GameObject AvatarHeads;

		public GameObject GroupOptionsButton;

		public GameObject ReportButton;

		public GameObject NotificationBar;

		public GameObject ForceUpdateNotificationBar;

		public Animator BackButtonAnimator;

		public ChatBar ChatBar;

		public MediaBar MediaBar;

		public BottomNav BottomNav;

		public MediaPreview MediaPreview;

		public GroupOptionsPanel GroupOptions;

		public MediaTray MediaTray;

		public GameTray GameTray;

		public CameraTray CameraTray;

		public KeyboardTray KeyboardTray;

		public GagSendTray GagSendTray;

		public Text PendingCounterText;

		public Text HeaderText;

		public Text SubHeaderText;

		public Text GroupOptionsHeaderText;

		public GameObject Header;

		public GameObject SafeChatIcon;

		public ScrollView ScrollView;

		public RuntimeAnimatorController mixbotAnimController;

		public GameObject AvatarLeft;

		public GameObject AvatarRight;

		public GameObject GroupChatBevel;

		public GameObject LoadingMessageBar;

		public GameObject TransitionShadow;

		private Dictionary<IChatMessage, long> clientMessages = new Dictionary<IChatMessage, long>();

		private RectTransform gagStage;

		private bool isFriend;

		private List<IChatMessage> localReferences = new List<IChatMessage>();

		private IChatMessageRetriever chatRetriever;

		public SdkEvents eventGenerator = new SdkEvents();

		public SdkActions actionGenerator = new SdkActions();

		private bool isBackgroundLoaded;

		private Official_Account OAEntitlement;

		private bool animatingTray;

		private int chatMessagesSent;

		private int timeChatStarted;

		private int unreadCountFromOtherThreads;

		private int chatsReceivedInThisThread;

		private int chatsSentInThisThread;

		private bool isFriendSet;

		private bool chatUISet;

		private bool areMoreMessagesToLoad = true;

		private bool doneRetrieving = true;

		private ReportAPlayerPanel mReportPanel;

		private Hashtable addedMessages = new Hashtable();

		private bool IsStateChange;

		private string lastMediaBarState = string.Empty;

		public GameObject DateStampText;

		private bool comingFromPushNote;

		private DateTime lastMessageTime = DateTime.MinValue;

		private bool sessionHasPaused;

		private GameObject chatFloor;

		public IChatThread chatThread { get; private set; }

		void IBundleObject.OnBundleAssetObject(UnityEngine.Object aGameObject, object aUserData)
		{
			if (!this.IsNullOrDisposed() && aUserData != null)
			{
				GameObject gameObject = (GameObject)MonoSingleton<AssetManager>.Instance.GetBundleInstance((string)aUserData);
				if (gameObject != null)
				{
					isBackgroundLoaded = true;
					GameObject gameObject2 = base.transform.Find("UI_BG_Holder/UI_BG_ChatScreen/Background/ImageTarget").gameObject;
					gameObject2.GetComponent<Image>().color = Color.white;
					gameObject2.GetComponent<Image>().sprite = gameObject.GetComponent<Image>().sprite;
					UnityEngine.Object.Destroy(gameObject);
				}
			}
		}

		void IMediaBar.OnCategorySelected(UIState aState, string aSelection)
		{
			if (aSelection != lastMediaBarState)
			{
				PlayAvatarTrigger(AvatarRight, "MediaTrayCategorySelected");
			}
			lastMediaBarState = aSelection;
			if (!IsStateChange)
			{
				IsStateChange = true;
				StartCoroutine(WaitForStateChange(aSelection));
				if (aState.Equals(UIState.KeyboardTray))
				{
					ChatBar.ToggleInput(true);
				}
				else
				{
					MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
					UpdateUIState(aState, aSelection);
				}
			}
			else
			{
				MediaBar.SetSelectedVisualStateOfToggle(aSelection);
			}
			MonoSingleton<GameManager>.Instance.PauseGameSession();
		}

		void INativeCamera.SendPhoto(JsonData aJsonData)
		{
			// TODO(upload-spec): remove when AbstractChatThread.SendPhotoMessage/SendVideoMessage are implemented
			if (!CanSendMedia())
			{
				ShowCameraComingSoon();
				return;
			}
			SendPhoto_003Ec__AnonStorey298 CS_0024_003C_003E8__locals10 = new SendPhoto_003Ec__AnonStorey298();
			CS_0024_003C_003E8__locals10._003C_003Ef__this = this;
			CS_0024_003C_003E8__locals10.localMessage = null;
			// TODO(upload-spec): remove when AbstractChatThread.SendPhotoMessage/SendVideoMessage are implemented
			try
			{
				CS_0024_003C_003E8__locals10.localMessage = chatThread.SendPhotoMessage((string)aJsonData["photoPath"], PhotoEncoding.Jpeg, (int)aJsonData["photoWidth"], (int)aJsonData["photoHeight"], actionGenerator.CreateAction(delegate(ISendPhotoMessageResult result)
				{
					if (!CS_0024_003C_003E8__locals10._003C_003Ef__this.IsNullOrDisposed() && CS_0024_003C_003E8__locals10._003C_003Ef__this.chatThread != null)
					{
						if (result == null || !result.Success)
						{
							CS_0024_003C_003E8__locals10._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals10.localMessage);
						}
						else if (result != null)
						{
							CS_0024_003C_003E8__locals10._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals10.localMessage);
						}
					}
				}));
			}
			catch (NotImplementedException)
			{
				ShowCameraComingSoon();
				return;
			}
			ScrollView.SetScrollToBottom();
			AddPhotoMessage(chatThread, CS_0024_003C_003E8__locals10.localMessage, true);
			Singleton<SoundManager>.Instance.PlaySoundEvent("MainApp/UI/SFX_3_MediaMessage");
			chatMessagesSent++;
		}

		void INativeCamera.SendVideo(JsonData aJsonData)
		{
			// TODO(upload-spec): remove when AbstractChatThread.SendPhotoMessage/SendVideoMessage are implemented
			if (!CanSendMedia())
			{
				ShowCameraComingSoon();
				return;
			}
			SendVideo_003Ec__AnonStorey297 CS_0024_003C_003E8__locals10 = new SendVideo_003Ec__AnonStorey297();
			CS_0024_003C_003E8__locals10._003C_003Ef__this = this;
			CS_0024_003C_003E8__locals10.localMessage = null;
			// TODO(upload-spec): remove when AbstractChatThread.SendPhotoMessage/SendVideoMessage are implemented
			try
			{
				CS_0024_003C_003E8__locals10.localMessage = chatThread.SendVideoMessage((string)aJsonData["videoPath"], VideoFormat.Mp4, (int)aJsonData["bitrate"], (int)aJsonData["duration"], (int)aJsonData["videoWidth"], (int)aJsonData["videoHeight"], (string)aJsonData["thumbPath"], PhotoEncoding.Jpeg, (int)aJsonData["thumbWidth"], (int)aJsonData["thumbHeight"], actionGenerator.CreateAction(delegate(ISendVideoMessageResult result)
				{
					if (!CS_0024_003C_003E8__locals10._003C_003Ef__this.IsNullOrDisposed() && CS_0024_003C_003E8__locals10._003C_003Ef__this.chatThread != null)
					{
						if (result == null || !result.Success)
						{
							CS_0024_003C_003E8__locals10._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals10.localMessage);
						}
						else if (result != null)
						{
							CS_0024_003C_003E8__locals10._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals10.localMessage);
						}
					}
				}));
			}
			catch (NotImplementedException)
			{
				ShowCameraComingSoon();
				return;
			}
			ScrollView.SetScrollToBottom();
			AddVideoMessage(chatThread, CS_0024_003C_003E8__locals10.localMessage, true);
			Singleton<SoundManager>.Instance.PlaySoundEvent("MainApp/UI/SFX_3_MediaMessage");
			chatMessagesSent++;
		}

		void INativeCamera.CameraError(CameraError aError)
		{
			string token = MediaPrep.ErrorToken(aError);
			if (token == null)
			{
				return;
			}
			ShowCameraPanel(null, token);
			MonoSingleton<NativeCameraManager>.Instance.Native.CloseOverlay();
		}

		// Only the onboarding fake-friend thread can store photos/videos today. A real or a
		// new (LocalThread) conversation would queue the send and then hit the SDK's
		// NotImplementedException later, inside a coroutine, leaving the message stuck.
		// TODO(upload-spec): remove when uploads exist.
		private bool CanSendMedia()
		{
			return chatThread is global::Mix.FakeFriend.Datatypes.FakeThread;
		}

		private void ShowCameraComingSoon()
		{
			ShowCameraPanel("chatmixscreen.cameratray.comingsoontext", null);
		}

		private void ShowCameraPanel(string aTitleToken, string aMessageToken)
		{
			GenericPanel genericPanel = (GenericPanel)Singleton<PanelManager>.Instance.ShowPanel(Panels.GENERIC);
			Singleton<PanelManager>.Instance.SetupTokens(new Dictionary<Text, string>
			{
				{ genericPanel.TitleText, aTitleToken },
				{ genericPanel.MessageText, aMessageToken },
				{ genericPanel.ButtonOneText, "customtokens.panels.button_ok" }
			});
		}

		void IMediaTray.OnEntitlementClicked(BaseContentData aEntitlement)
		{
			if (aEntitlement is IEntitlementGameData)
			{
				MonoSingleton<GagManager>.Instance.ClearGags();
				MonoSingleton<GameManager>.Instance.LoadGame(aEntitlement as IEntitlementGameData, chatThread, this);
				Singleton<TechAnalytics>.Instance.TrackTimeToLoadGameStart((BaseGameData)aEntitlement);
				ChatBar.ToggleListeners(false);
			}
			else if (aEntitlement is Gag && chatThread is IGroupChatThread)
			{
				GagSendTray.Init(this, (Gag)aEntitlement, chatThread);
				UpdateUIState(UIState.GagSendTray);
			}
			else
			{
				OnSendEntitlement(aEntitlement);
			}
		}

		void IMediaTray.OnShowPreviewPanel(BaseContentData aEntitlement)
		{
			MediaPreview.Show(this, aEntitlement, chatThread);
		}

		void IMediaTray.OnContentHolderChanged()
		{
			if (lastMediaBarState == "StickerPacks")
			{
				PlayAvatarTrigger(AvatarRight, "MediaTrayCategorySelected");
			}
		}

		void IGagSendTray.OnSendGroupGag(Gag aGag, IRemoteChatMember targetUser)
		{
			OnSendGroupGag_003Ec__AnonStorey296 CS_0024_003C_003E8__locals15 = new OnSendGroupGag_003Ec__AnonStorey296();
			CS_0024_003C_003E8__locals15.aGag = aGag;
			CS_0024_003C_003E8__locals15._003C_003Ef__this = this;
			CS_0024_003C_003E8__locals15.localMessage = null;
			CS_0024_003C_003E8__locals15.localMessage = chatThread.SendGagMessage(CS_0024_003C_003E8__locals15.aGag.GetUid(), targetUser.Id, actionGenerator.CreateAction(delegate(ISendGagMessageResult result)
			{
				if (!CS_0024_003C_003E8__locals15._003C_003Ef__this.IsNullOrDisposed() && CS_0024_003C_003E8__locals15._003C_003Ef__this.chatThread != null && CS_0024_003C_003E8__locals15.localMessage != null)
				{
					if (result == null || !result.Success)
					{
						CS_0024_003C_003E8__locals15._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals15.localMessage);
					}
					else if (result != null)
					{
						CS_0024_003C_003E8__locals15._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals15.localMessage);
					}
				}
			}));
			if (CS_0024_003C_003E8__locals15.localMessage != null)
			{
				ScrollView.SetScrollToBottom();
				AddGagMessage(chatThread, CS_0024_003C_003E8__locals15.localMessage, true);
				Analytics.LogSendGag(chatThread, CS_0024_003C_003E8__locals15.aGag.GetName());
				Singleton<SoundManager>.Instance.PlaySoundEvent("MainApp/UI/SFX_3_MediaMessage");
				UpdateUIState(UIState.Default);
				chatMessagesSent++;
			}
		}

		void IChatBar.OnKeyboardShown()
		{
			SetAvatarAnimBool(AvatarRight, "IsKeyBoardActive", true);
			PlayAvatarTrigger(AvatarRight, "UserTyping");
			if (MonoSingleton<GameManager>.Instance.IsSessionsPaused)
			{
				GameTray.PausedPanel.SetActive(false);
			}
			UpdateUIState(UIState.KeyboardTray);
		}

		void IChatBar.OnKeyboardHidden()
		{
			SetAvatarAnimBool(AvatarRight, "IsKeyBoardActive", false);
			if (MonoSingleton<GameManager>.Instance.IsSessionsPaused)
			{
				GameTray.PausedPanel.SetActive(true);
				UpdateUIState(UIState.GamePausedTray);
			}
			else
			{
				KeyboardTray.UpdateState(false);
			}
		}

		void IChatBar.OnSendTextMessage(string aMessage)
		{
			OnSendTextMessage_003Ec__AnonStorey295 CS_0024_003C_003E8__locals13 = new OnSendTextMessage_003Ec__AnonStorey295();
			CS_0024_003C_003E8__locals13.aMessage = aMessage;
			CS_0024_003C_003E8__locals13._003C_003Ef__this = this;
			CS_0024_003C_003E8__locals13.localMessage = null;
			CS_0024_003C_003E8__locals13.localMessage = chatThread.SendTextMessage(CS_0024_003C_003E8__locals13.aMessage, actionGenerator.CreateAction(delegate(ISendTextMessageResult result)
			{
				if (!CS_0024_003C_003E8__locals13._003C_003Ef__this.IsNullOrDisposed() && CS_0024_003C_003E8__locals13._003C_003Ef__this.chatThread != null)
				{
					if (result == null || !result.Success)
					{
						CS_0024_003C_003E8__locals13._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals13.localMessage);
					}
					else if (result != null)
					{
						CS_0024_003C_003E8__locals13._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals13.localMessage);
						if (result.IsModerated)
						{
							Analytics.LogChatModerated(CS_0024_003C_003E8__locals13._003C_003Ef__this.chatThread);
						}
					}
				}
			}));
			ScrollView.SetScrollToBottom();
			AddTextMessage(chatThread, CS_0024_003C_003E8__locals13.localMessage, true);
			chatMessagesSent++;
		}

		void IGameTray.OnGamePause(string aLogo)
		{
			if (!this.IsNullOrDisposed() && !GameTray.IsNullOrDisposed())
			{
				GameTray.OnGamePause(aLogo);
			}
		}

		void IGameTray.OnGameError(string aLogo)
		{
			if (!this.IsNullOrDisposed() && !GameTray.IsNullOrDisposed())
			{
				GameTray.OnGameError(aLogo);
			}
		}

		void IGameTray.SetGamePanel(GameTrayState aState)
		{
			switch (aState)
			{
			case GameTrayState.NONE:
				UpdateUIState(UIState.Default);
				break;
			case GameTrayState.GAME:
				PlayAvatarTrigger(AvatarRight, "UserPlayingGame");
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
				UpdateUIState(UIState.GameTray);
				break;
			case GameTrayState.PAUSE:
				UpdateUIState(UIState.GamePausedTray);
				break;
			case GameTrayState.ERROR:
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
				UpdateUIState(UIState.GamePausedTray);
				break;
			}
		}

		void IGameTray.UpdateGameHeight(int aHeight)
		{
			GameTray.SetSize(aHeight);
		}

		void IGameTray.UpdateGameLoader(string aLogo)
		{
			UpdateUIState(UIState.GameTray);
			GameTray.UpdateGameLoader(aLogo);
		}

		void IGameTray.UpdateGameExit(Texture2D aGameScreenshot)
		{
			UpdateUIState(UIState.GameTray);
			GameTray.UpdateGameScreenshot(aGameScreenshot);
		}

		void IGameTray.UpdateGameStateMessage(IGameMessageData messageData, Dictionary<string, object> payload, Action<object> callback)
		{
			UpdateGameStateMessage_003Ec__AnonStorey294 CS_0024_003C_003E8__locals2 = new UpdateGameStateMessage_003Ec__AnonStorey294();
			CS_0024_003C_003E8__locals2.callback = callback;
			Action<IUpdateGameStateMessageResult> action = delegate(IUpdateGameStateMessageResult o)
			{
				CS_0024_003C_003E8__locals2.callback(o);
			};
			IGameStateMessage gameStateMessage = null;
			gameStateMessage = ((!(messageData.MixMessageType is IGameEventMessage)) ? ((IGameStateMessage)messageData.MixMessageType) : ((IGameEventMessage)messageData.MixMessageType).GameStateMessage);
			if (chatThread is IOneOnOneChatThread)
			{
				((IOneOnOneChatThread)chatThread).UpdateGameStateMessage(gameStateMessage, payload, actionGenerator.CreateAction(action));
			}
			if (chatThread is IGroupChatThread)
			{
				((IGroupChatThread)chatThread).UpdateGameStateMessage(gameStateMessage, payload, actionGenerator.CreateAction(action));
			}
			if (chatThread is IOfficialAccountChatThread)
			{
				((IOfficialAccountChatThread)chatThread).UpdateGameStateMessage(gameStateMessage, payload, actionGenerator.CreateAction(action));
			}
		}

		void IGameTray.SendGameStateMessage(string gameName, Dictionary<string, object> payload, Action<object> callback)
		{
			SendGameStateMessage_003Ec__AnonStorey293 CS_0024_003C_003E8__locals34 = new SendGameStateMessage_003Ec__AnonStorey293();
			CS_0024_003C_003E8__locals34.callback = callback;
			CS_0024_003C_003E8__locals34._003C_003Ef__this = this;
			if (this.IsNullOrDisposed() || base.gameObject == null || CS_0024_003C_003E8__locals34.callback == null)
			{
				return;
			}
			CS_0024_003C_003E8__locals34.localMessage = null;
			CS_0024_003C_003E8__locals34.newCallback = delegate(ISendGameStateMessageResult o)
			{
				CS_0024_003C_003E8__locals34.callback(o);
			};
			if (chatThread is IOneOnOneChatThread)
			{
				CS_0024_003C_003E8__locals34.localMessage = ((IOneOnOneChatThread)chatThread).SendGameStateMessage(gameName, payload, actionGenerator.CreateAction(delegate(ISendGameStateMessageResult result)
				{
					if (!CS_0024_003C_003E8__locals34._003C_003Ef__this.IsNullOrDisposed() && !(CS_0024_003C_003E8__locals34._003C_003Ef__this.gameObject == null) && CS_0024_003C_003E8__locals34._003C_003Ef__this.chatThread != null && result != null)
					{
						if (!result.Success)
						{
							CS_0024_003C_003E8__locals34._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals34.localMessage);
						}
						else
						{
							CS_0024_003C_003E8__locals34._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals34.localMessage);
						}
						CS_0024_003C_003E8__locals34.newCallback(result);
					}
				}));
			}
			if (chatThread is IGroupChatThread)
			{
				CS_0024_003C_003E8__locals34.localMessage = ((IGroupChatThread)chatThread).SendGameStateMessage(gameName, payload, actionGenerator.CreateAction(delegate(ISendGameStateMessageResult result)
				{
					if (!CS_0024_003C_003E8__locals34._003C_003Ef__this.IsNullOrDisposed() && !(CS_0024_003C_003E8__locals34._003C_003Ef__this.gameObject == null) && CS_0024_003C_003E8__locals34._003C_003Ef__this.chatThread != null && result != null)
					{
						if (!result.Success)
						{
							CS_0024_003C_003E8__locals34._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals34.localMessage);
						}
						else
						{
							CS_0024_003C_003E8__locals34._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals34.localMessage);
						}
						CS_0024_003C_003E8__locals34.newCallback(result);
					}
				}));
			}
			if (chatThread is IOfficialAccountChatThread)
			{
				CS_0024_003C_003E8__locals34.localMessage = ((IOfficialAccountChatThread)chatThread).SendGameStateMessage(gameName, payload, actionGenerator.CreateAction(delegate(ISendGameStateMessageResult result)
				{
					if (!CS_0024_003C_003E8__locals34._003C_003Ef__this.IsNullOrDisposed() && !(CS_0024_003C_003E8__locals34._003C_003Ef__this.gameObject == null) && CS_0024_003C_003E8__locals34._003C_003Ef__this.chatThread != null && result != null)
					{
						if (!result.Success)
						{
							CS_0024_003C_003E8__locals34._003C_003Ef__this.MessageUpdateInternal(CS_0024_003C_003E8__locals34.localMessage);
						}
						else
						{
							CS_0024_003C_003E8__locals34._003C_003Ef__this.CheckAndAddTimestampForMessage(CS_0024_003C_003E8__locals34.localMessage);
						}
						CS_0024_003C_003E8__locals34.newCallback(result);
					}
				}));
			}
			ScrollView.SetScrollToBottom();
			AddGameMessage(chatThread, CS_0024_003C_003E8__locals34.localMessage, true);
			chatMessagesSent++;
		}

		void IGameTray.SendEntitlement(IEntitlementGameData aEntitlement)
		{
			SendEntitlement(aEntitlement as BaseGameData, MixSession.User.Id);
		}

		string IGameTray.GetCurrentThreadId()
		{
			return chatThread.Id;
		}

		void IGroupOptionsPanel.OnNicknameUpdated(string aNickname)
		{
			if (!this.IsNullOrDisposed())
			{
				UpdateHeader(aNickname);
			}
		}

		void IGroupOptionsPanel.OnClosing()
		{
			if (!this.IsNullOrDisposed() && !ScrollView.IsNullOrDisposed())
			{
				ScrollView.Show();
			}
		}

		private void Start()
		{
			gagStage = base.transform.Find("UI_FG_Holder/UI_FG_ChatScreen/GagStage").GetComponent<RectTransform>();
			CreateAvatar(AvatarRight, false, new Vector3(0f, -50f, 660f));
			CreateAvatar(AvatarLeft, true, new Vector3(0f, -50f, 660f));
			CreateAvatar(MediaPreview.transform.Find("AvatarHeadsPlacement/Holder/PlaceholderAvatarRight/AvatarHeadRight").gameObject, false, Vector3.zero, true);
			CreateAvatar(MediaPreview.transform.Find("AvatarHeadsPlacement/Holder/PlaceholderAvatarLeft/AvatarHeadLeft").gameObject, true);
			CreateAvatar(gagStage.transform.Find("PlaceholderAvatarRight/AvatarHeadRight").gameObject, false, Vector3.zero, true);
			CreateAvatar(gagStage.transform.Find("PlaceholderAvatarLeft/AvatarHeadLeft").gameObject, true);
			if (chatThread != null)
			{
				UpdateHeader(chatThread.GetThreadTitle());
				if (chatThread is LocalThread)
				{
					((LocalThread)chatThread).OnThreadCreated += OnThreadCreated;
				}
				ToggleReportButton();
				SetSafeChatIcon();
				SetGroupOptionsButton();
				if (chatThread is IOfficialAccountChatThread)
				{
					AvatarHeads.SetActive(false);
					ScrollView.bottomOffset = 10;
					ScrollView.Reposition();
				}
				else if (chatThread is IGroupChatThread)
				{
					AvatarHeads.SetActive(false);
					ScrollView.bottomOffset = 10;
					ScrollView.Reposition();
				}
				else if (chatThread is IOneOnOneChatThread)
				{
					bool isMixbot = MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread);
					if (!MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
					{
						IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
						isFriend = thread.IsOtherUserFriend();
					}
					SetupAvatarAnimationHelper(AvatarLeft, isMixbot);
					SetupAvatarAnimationHelper(AvatarRight, false);
					FlipAvatarUvs(AvatarRight);
					FlipAvatarGeo(AvatarRight);
					PlayAvatarTrigger(AvatarRight, "ChatEntrance");
				}
				MonoSingleton<LocalNotificationManager>.Instance.RemoveNotificationsForThread(chatThread);
			}
			else
			{
				Log.Exception("ChatController Start without chatThread");
			}
			timeChatStarted = (int)Time.time;
			if ((double)(MixConstants.CANVAS_HEIGHT / MixConstants.CANVAS_WIDTH) < 1.33)
			{
				GameObject gameObject = base.transform.Find("UI_BG_Holder/UI_BG_ChatScreen/Background/ImageTarget").gameObject;
				RectTransform component = gameObject.GetComponent<RectTransform>();
				component.sizeDelta = new Vector2(MixConstants.CANVAS_WIDTH, component.rect.height);
			}
		}

		public void CreateAvatar(GameObject parent, [Optional][DefaultParameterValue(false)] bool flippedAvatar, [Optional] Vector3 position, bool flipX = false)
		{
			Quaternion localRotation = new Quaternion(0f, 180f, 0f, 1f);
			GameObject gameObject = MonoSingleton<AvatarManager>.Instance.CreateAvatarObject(flippedAvatar);
			GameObject gameObject2 = gameObject.transform.Find("cube_rig").gameObject;
			gameObject2.transform.SetParent(parent.transform, false);
			gameObject2.transform.localPosition = position;
			gameObject2.transform.localRotation = localRotation;
			if (flipX)
			{
				Vector3 localScale = gameObject2.transform.localScale;
				localScale.x *= -1f;
				gameObject2.transform.localScale = localScale;
			}
			Util.SetLayerRecursively(gameObject2, parent.layer);
			UnityEngine.Object.Destroy(gameObject);
		}

		private void FlipAvatarUvs(GameObject go)
		{
			if (!this.IsNullOrDisposed() && !go.IsNullOrDisposed())
			{
				Transform transform = go.transform.Find("cube_rig");
				if (transform != null)
				{
					GameObject gameObject = transform.gameObject;
					Vector3 localScale = gameObject.transform.localScale;
					gameObject.transform.localScale = new Vector3(localScale.x * -1f, 1f, 1f);
					FlipObjectAtTransform(gameObject, "grp_offset/head");
					FlipObjectAtTransform(gameObject, "grp_offset/face_mouth");
				}
			}
		}

		private void FlipObjectAtTransform(GameObject go, string transformPath)
		{
			Transform transform = go.transform.Find(transformPath);
			if (!(transform != null))
			{
				return;
			}
			GameObject gameObject = transform.gameObject;
			if (!gameObject.IsNullOrDisposed())
			{
				Mesh sharedMesh = gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
				if (!sharedMesh.IsNullOrDisposed())
				{
					Vector2[] uv = sharedMesh.uv;
					sharedMesh.uv = sharedMesh.uv2;
					sharedMesh.uv2 = uv;
				}
			}
		}

		private void FlipAvatarGeo(GameObject go)
		{
			if (this.IsNullOrDisposed() || go.IsNullOrDisposed())
			{
				return;
			}
			Transform transform = go.transform.Find("cube_rig");
			if (!(transform != null))
			{
				return;
			}
			GameObject gameObject = transform.gameObject;
			Transform transform2 = gameObject.transform.Find("grp_offset/def_front");
			if (transform2 != null && transform2.childCount > 0)
			{
				Transform child = transform2.GetChild(0);
				if (child != null)
				{
					child.localScale = new Vector3(child.localScale.x * -1f, 1f, 1f);
				}
			}
			Transform transform3 = gameObject.transform.Find("grp_offset/def_hatBase");
			if (transform3 != null && transform3.childCount > 0)
			{
				Transform child2 = transform3.GetChild(0);
				if (child2 != null)
				{
					child2.localScale = new Vector3(child2.localScale.x * -1f, 1f, 1f);
				}
			}
			Transform transform4 = gameObject.transform.Find("grp_offset/face_mouth");
			if (!(transform4 != null))
			{
				return;
			}
			GameObject gameObject2 = transform4.gameObject;
			if (!gameObject2.IsNullOrDisposed())
			{
				SkinnedMeshRenderer component = gameObject2.GetComponent<SkinnedMeshRenderer>();
				if (!component.IsNullOrDisposed())
				{
					float blendShapeWeight = component.GetBlendShapeWeight(0);
					component.SetBlendShapeWeight(0, 100f - blendShapeWeight);
				}
			}
		}

		private void ToggleReportButton()
		{
			if (!chatThread.IsFakeOrLocal() && chatThread is IOneOnOneChatThread)
			{
				ReportButton.SetActive(true);
				ReportButton.GetComponent<Button>().interactable = MixSession.connection == MixSession.ConnectionState.ONLINE;
			}
			else
			{
				ReportButton.SetActive(false);
			}
		}

		private void OnThreadCreated(object sender)
		{
			if (!MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
			{
				ToggleReportButton();
				SetSafeChatIcon();
				SetGroupOptionsButton();
			}
		}

		private void Update()
		{
			Singleton<TechAnalytics>.Instance.TrackFPSOnChat(chatThread is IGroupChatThread);
			if (MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
			{
				MonoSingleton<FakeFriendManager>.Instance.HighlightAnimator(FakeFriendManager.TYPE_CHAT_BACK, BackButtonAnimator);
				if (!ChatBar.InputField.isFocused)
				{
					MonoSingleton<FakeFriendManager>.Instance.HighlightAnimator(FakeFriendManager.TYPE_CHAT_INPUT, ChatBar.GetComponent<Animator>(), false);
				}
			}
			UpdateState();
			UpdateScrollView();
		}

		private void FixedUpdate()
		{
			if (!animatingTray)
			{
				return;
			}
			animatingTray = GameTray.AnimateDown();
			if (!animatingTray)
			{
				GameTray.OnAnimateDownComplete();
				if (!GameTray.PausedPanel.activeSelf)
				{
					MonoSingleton<GameManager>.Instance.CloseGameSession();
				}
				UpdateUIState(UIState.Default);
			}
		}

		private void OnDestroy()
		{
			if (ScrollView != null)
			{
				ScrollView scrollView = ScrollView;
				scrollView.OnPointerDownDelegates = (ScrollView.OnPointerDownDelegate)Delegate.Remove(scrollView.OnPointerDownDelegates, new ScrollView.OnPointerDownDelegate(OnScrollGotPointerDown));
				ScrollView scrollView2 = ScrollView;
				scrollView2.OnUserScrolledToTopDelegates = (ScrollView.OnUserScrolledToTopDelegate)Delegate.Remove(scrollView2.OnUserScrolledToTopDelegates, new ScrollView.OnUserScrolledToTopDelegate(OnUserScrollToTop));
				ScrollView = null;
			}
			if (chatThread != null && MixSession.User != null)
			{
				MixSession.User.OnAddedToGroupChatThread -= eventGenerator.GetEventHandler<AbstractAddedToGroupChatThreadEventArgs>(this, OnAddedToGroupChatThread);
				MixSession.User.OnAddedToOfficialAccountChatThread -= eventGenerator.GetEventHandler<AbstractAddedToOfficialAccountThreadEventArgs>(this, OnAddedToOfficialAccountThread);
				MixSession.User.OnAddedToOneOnOneChatThread -= eventGenerator.GetEventHandler<AbstractAddedToOneOnOneChatThreadEventArgs>(this, OnAddedToOneOnOneChatThread);
				foreach (IChatThread item in MixSession.User.ChatThreadsFromUsers())
				{
					RemoveThreadListeners(item);
				}
				if (chatThread is LocalThread)
				{
					RemoveThreadListeners(chatThread);
					((LocalThread)chatThread).OnThreadCreated -= OnThreadCreated;
				}
				if (chatThread is IOneOnOneChatThread)
				{
					IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
					MixSession.User.OnAvatarChanged -= eventGenerator.GetEventHandler<AbstractAvatarChangedEventArgs>(this, UpdateAvatars);
					IAvatarHolder otherAvatarHolder = thread.GetOtherAvatarHolder();
					otherAvatarHolder.OnAvatarChanged -= eventGenerator.GetEventHandler<AbstractAvatarChangedEventArgs>(otherAvatarHolder, UpdateAvatars);
					FlipAvatarUvs(AvatarRight);
				}
				chatThread.OnMemberAdded -= eventGenerator.GetEventHandler<AbstractChatThreadMemberAddedEventArgs>(this, OnMemberAdded);
				chatThread.OnMemberRemoved -= eventGenerator.GetEventHandler<AbstractChatThreadMemberRemovedEventArgs>(this, OnMemberRemoved);
				chatThread.OnTrustStatusChanged -= eventGenerator.GetEventHandler<AbstractChatThreadTrustStatusChangedEventArgs>(this, OnThreadTrustChanged);
				if (!MonoSingleton<ConnectionManager>.Instance.IsNullOrDisposed() && MonoSingleton<ConnectionManager>.Instance.IsConnected && (chatsReceivedInThisThread > 0 || chatsSentInThisThread > 0))
				{
					chatThread.ClearUnreadMessageCount(actionGenerator.CreateAction<IClearUnreadMessageCountResult>(delegate
					{
					}));
				}
			}
			if (MonoSingleton<AssetManager>.Instance != null && isBackgroundLoaded && OAEntitlement != null)
			{
				MonoSingleton<AssetManager>.Instance.DestroyBundleInstance(OAEntitlement.GetBackground());
			}
			MixSession.OnConnectionChanged -= OnConnectionChanged;
			addedMessages.Clear();
			clientMessages.Clear();
			localReferences.Clear();
		}

		private void OnApplicationPause(bool aPauseStatus)
		{
			if (aPauseStatus)
			{
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
				if (!MonoSingleton<NavigationManager>.Instance.IsOverlayRequest)
				{
					UpdateUIState(UIState.Default);
				}
			}
			if (aPauseStatus && chatThread != null)
			{
				chatThread.ClearUnreadMessageCount(delegate
				{
				});
			}
			int num = (int)Time.time;
			Analytics.LogLeaveChatPage(chatThread, chatMessagesSent);
			Analytics.LogLeaveChatPageTimingAction(num - timeChatStarted, chatThread);
			chatMessagesSent = 0;
			timeChatStarted = num;
		}

		public override void OnDataReceived(string aToken, object aData)
		{
			if (aToken.Contains("thread:"))
			{
				chatThread = (IChatThread)aData;
			}
			if (aToken.Contains("fromPushNote") && MixSession.connection == MixSession.ConnectionState.ONLINE)
			{
			}
		}

		public override void OnUILoaded(NavigationRequest aNavigationRequest = null)
		{
			MediaBar.Init(this, chatThread);
			MediaTray.Init(this, chatThread);
			CameraTray.Init(this, CanSendMedia, ShowCameraComingSoon);
			ChatBar.Init(this, chatThread);
			BottomNav.Init(this);
			if (chatThread is IOfficialAccountChatThread)
			{
				OAEntitlement = Singleton<EntitlementsManager>.Instance.GetOfficialAccount(((IOfficialAccountChatThread)chatThread).OfficialAccount.AccountId);
				if (OAEntitlement != null)
				{
					GameObject gameObject = base.transform.Find("UI_BG_Holder/UI_BG_ChatScreen/Background/ImageTarget").gameObject;
					gameObject.SetActive(true);
					Color color = Util.HexToColor(OAEntitlement.GetTintColor1());
					color.a = 0.25f;
					gameObject.GetComponent<Image>().color = color;
					MonoSingleton<AssetManager>.Instance.LoadABundle(this, OAEntitlement.GetBackground(), OAEntitlement.GetBackground(), string.Empty, true, false, true);
					Util.UpdateTintablesForOfficialAccount(OAEntitlement, base.gameObject);
				}
			}
			if (OnChatThreadEnter != null)
			{
				OnChatThreadEnter(this, new ChatThreadEnterEventArgs(chatThread, this));
			}
		}

		public override void OnUIUnLoaded(NavigationRequest aNavigationRequest)
		{
			if (OnChatThreadExit != null)
			{
				OnChatThreadExit(this, new ChatThreadExitEventArgs(chatThread, this));
			}
		}

		private void Awake()
		{
			if (!(chatThread is IGroupChatThread) && AvatarRight != null && AvatarLeft != null)
			{
				AvatarRight.SetActive(false);
				AvatarLeft.SetActive(false);
			}
		}

		public override void OnUITransitionEnd()
		{
			if (chatThread == null)
			{
				OnBackButtonClicked();
				return;
			}
			Analytics.LogViewChat(chatThread);
			if (TransitionShadow != null)
			{
				TransitionShadow.SetActive(false);
			}
			ScrollView.SetScrollToBottom();
			if (!MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
			{
				LoadingMessageBar.SetActive(true);
				MonoSingleton<ChatPrimerManager>.Instance.OnFirstPageReady(chatThread, delegate
				{
					if (!this.IsNullOrDisposed() && chatThread != null)
					{
						LoadingMessageBar.SetActive(false);
						LoadUpMessages();
					}
				});
			}
			else
			{
				LoadUpMessages();
			}
		}

		private void LoadUpMessages()
		{
			MonoSingleton<FakeFriendManager>.Instance.ChatVisited(chatThread);
			ScrollView scrollView = ScrollView;
			scrollView.OnPointerDownDelegates = (ScrollView.OnPointerDownDelegate)Delegate.Combine(scrollView.OnPointerDownDelegates, new ScrollView.OnPointerDownDelegate(OnScrollGotPointerDown));
			ScrollView scrollView2 = ScrollView;
			scrollView2.OnUserScrolledToTopDelegates = (ScrollView.OnUserScrolledToTopDelegate)Delegate.Combine(scrollView2.OnUserScrolledToTopDelegates, new ScrollView.OnUserScrolledToTopDelegate(OnUserScrollToTop));
			SkinAvatarHeads();
			if (chatThread is IOneOnOneChatThread)
			{
				IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
				MixSession.User.OnAvatarChanged += eventGenerator.AddEventHandler<AbstractAvatarChangedEventArgs>(this, UpdateAvatars);
				IAvatarHolder otherAvatarHolder = thread.GetOtherAvatarHolder();
				otherAvatarHolder.OnAvatarChanged += eventGenerator.AddEventHandler<AbstractAvatarChangedEventArgs>(otherAvatarHolder, UpdateAvatars);
			}
			SetSafeChatIcon();
			SetGroupOptionsButton();
			ToggleReportButton();
			MixSession.OnConnectionChanged += OnConnectionChanged;
			RestartChatRetriever();
			MixSession.User.OnAddedToGroupChatThread += eventGenerator.AddEventHandler<AbstractAddedToGroupChatThreadEventArgs>(this, OnAddedToGroupChatThread);
			MixSession.User.OnAddedToOfficialAccountChatThread += eventGenerator.AddEventHandler<AbstractAddedToOfficialAccountThreadEventArgs>(this, OnAddedToOfficialAccountThread);
			MixSession.User.OnAddedToOneOnOneChatThread += eventGenerator.AddEventHandler<AbstractAddedToOneOnOneChatThreadEventArgs>(this, OnAddedToOneOnOneChatThread);
			foreach (IChatThread item in MixSession.User.ChatThreadsFromUsers())
			{
				AddThreadListeners(item);
			}
			if (chatThread is LocalThread)
			{
				AddThreadListeners(chatThread);
			}
			unreadCountFromOtherThreads = 0;
			chatThread.ClearUnreadMessageCount(actionGenerator.CreateAction<IClearUnreadMessageCountResult>(delegate
			{
			}));
			chatThread.OnMemberAdded += eventGenerator.AddEventHandler<AbstractChatThreadMemberAddedEventArgs>(this, OnMemberAdded);
			chatThread.OnMemberRemoved += eventGenerator.AddEventHandler<AbstractChatThreadMemberRemovedEventArgs>(this, OnMemberRemoved);
			chatThread.OnTrustStatusChanged += eventGenerator.AddEventHandler<AbstractChatThreadTrustStatusChangedEventArgs>(this, OnThreadTrustChanged);
		}

		private void RestartChatRetriever(bool startRetrieve = true)
		{
			areMoreMessagesToLoad = true;
			chatRetriever = chatThread.CreateChatMessageRetriever(50);
			if (startRetrieve)
			{
				chatRetriever.RetrieveMessages(actionGenerator.CreateAction<IRetrieveChatThreadMessagesResult, bool>(DBRetrieverCallback, false));
			}
		}

		private void OnConnectionChanged(MixSession.ConnectionState newState, MixSession.ConnectionState oldState)
		{
			if (this.IsNullOrDisposed() || MonoSingleton<FakeFriendManager>.Instance.IsNullOrDisposed() || MonoSingleton<ChatPrimerManager>.Instance.IsNullOrDisposed())
			{
				return;
			}
			if (newState == MixSession.ConnectionState.ONLINE)
			{
				if (!MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread) && sessionHasPaused)
				{
					MonoSingleton<ChatPrimerManager>.Instance.PrimeFirstPageNow(chatThread);
					MonoSingleton<ChatPrimerManager>.Instance.OnFirstPageReady(chatThread, delegate
					{
						if (!this.IsNullOrDisposed() && chatThread != null && chatThread.ChatMessages.Any() && !ScrollView.IsNullOrDisposed() && chatThread.ChatMessages.Last().TimeSent != lastMessageTime)
						{
							LoadingMessageBar.SetActive(true);
							addedMessages.Clear();
							clientMessages.Clear();
							localReferences.Clear();
							ScrollView.Clear();
							RestartChatRetriever(false);
							StartCoroutine(DelayedRetrieve(false));
						}
					});
				}
			}
			else
			{
				if (chatThread.ChatMessages.Any())
				{
					lastMessageTime = chatThread.ChatMessages.Last().TimeSent;
				}
				sessionHasPaused = true;
			}
			if (comingFromPushNote)
			{
				comingFromPushNote = false;
			}
			SetGroupOptionsButton();
			ToggleReportButton();
		}

		private IEnumerator DelayScrollToBottom()
		{
			yield return new WaitForEndOfFrame();
			if (!this.IsNullOrDisposed() && ScrollView != null)
			{
				ScrollView.SetScrollToBottom();
			}
		}

		private void DBRetrieverCallback(IRetrieveChatThreadMessagesResult e, bool aIsLoadedMoreMessages = false)
		{
			if (e.Success && !this.IsNullOrDisposed() && !ScrollView.IsNullOrDisposed())
			{
				bool flag = ScrollView.IsAtBottom();
				float height = ScrollView.container.GetComponent<RectTransform>().rect.height;
				foreach (IChatMessage chatMessage2 in e.ChatMessages)
				{
					AddChatMessage(chatMessage2);
				}
				if (aIsLoadedMoreMessages)
				{
					float height2 = ScrollView.container.GetComponent<RectTransform>().rect.height;
					ScrollView.ModifyScrollPosition(height2 - height);
				}
				else if (flag)
				{
					ScrollView.SetScrollToBottom();
				}
				ScrollView.Update();
				ScrollView.Reposition();
				ScrollView.UpdateListNow();
				MonoSingleton<ChatPrimerManager>.Instance.ContinuePriming(chatThread);
				if (LoadingMessageBar != null)
				{
					LoadingMessageBar.SetActive(false);
				}
				areMoreMessagesToLoad = !e.IsDone || !MonoSingleton<ChatPrimerManager>.Instance.IsDonePriming(chatThread);
				if (!areMoreMessagesToLoad && chatThread is IOfficialAccountChatThread)
				{
					IChatMessage chatMessage = e.ChatMessages.LastOrDefault((IChatMessage message) => !message.IsMine());
					if (chatMessage != null)
					{
						Analytics.LogOAMessageRecieved(chatThread, chatMessage);
					}
				}
			}
			else if (LoadingMessageBar != null)
			{
				LoadingMessageBar.SetActive(false);
			}
			doneRetrieving = true;
		}

		public override void OnAndroidBackButtonClicked()
		{
			if (!Singleton<PanelManager>.Instance.OnAndroidBackButton() && !MonoSingleton<NativeCameraManager>.Instance.Native.OnAndroidBackButton())
			{
				OnBackButtonClicked();
			}
		}

		public void OnFriendAvatarClicked()
		{
			if ((!(chatThread is IOneOnOneChatThread) && !MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread)) || MonoSingleton<GagManager>.Instance.IsGagPlaying())
			{
				return;
			}
			List<Gag> list = Singleton<EntitlementsManager>.Instance.GetAllGags().FindAll((Gag x) => x.GetPokable());
			if (list != null && list.Count > 0)
			{
				Gag gag = list[UnityEngine.Random.Range(0, list.Count)];
				if (gag != null)
				{
					SendEntitlement(gag, (chatThread as IOneOnOneChatThread).GetOtherUser().Id);
				}
			}
		}

		public void OnBackButtonClicked()
		{
			if (MonoSingleton<GameManager>.Instance.ActiveSession != null)
			{
				MonoSingleton<GameManager>.Instance.QuitGameSession();
				return;
			}
			if (TransitionShadow != null)
			{
				TransitionShadow.SetActive(true);
			}
			NavigationRequest aRequest = new NavigationRequest("Prefabs/Screens/Conversations/ConversationsScreen", new TransitionSlideRight());
			MonoSingleton<NavigationManager>.Instance.AddRequest(aRequest);
			int num = (int)Time.time;
			Analytics.LogLeaveChatPage(chatThread, chatMessagesSent);
			Analytics.LogLeaveChatPageTimingAction(num - timeChatStarted, chatThread);
		}

		public void OnReportButtonClicked()
		{
			if (chatThread is IOneOnOneChatThread && (chatThread as IOneOnOneChatThread).GetOtherUser() == null)
			{
				ReportButton.GetComponent<Button>().interactable = false;
				return;
			}
			mReportPanel = (ReportAPlayerPanel)Singleton<PanelManager>.Instance.ShowPanel(Panels.REPORT, false);
			if (!(mReportPanel == null))
			{
				if (ScrollView != null)
				{
					ScrollView.Hide();
				}
				MonoSingleton<GagManager>.Instance.ClearGags();
				MonoSingleton<GameManager>.Instance.PauseGameSession();
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
				if (chatThread is IOneOnOneChatThread)
				{
					IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
					mReportPanel.Init(thread.GetOtherUser(), "chat", this);
					ReportButton.GetComponent<Button>().interactable = false;
					mReportPanel.PanelClosingEvent += OnReportPanelClosing;
				}
			}
		}

		public void OnSafeChatButtonClicked()
		{
			if (LoadingController.HideManagePC)
			{
				GenericPanel genericPanel = (GenericPanel)Singleton<PanelManager>.Instance.ShowPanel(Panels.GENERIC, false);
				if (!(genericPanel == null))
				{
					MonoSingleton<GameManager>.Instance.PauseGameSession();
					string value = ((chatThread is IOneOnOneChatThread) ? "customtokens.panels.safe_chat_text" : ((!chatThread.IsFakeOrLocal()) ? "customtokens.panels.safe_chat_text_group" : "customtokens.panels.safe_chat_text_new_group"));
					Singleton<PanelManager>.Instance.SetupTokens(new Dictionary<Text, string>
					{
						{ genericPanel.TitleText, null },
						{ genericPanel.MessageText, value },
						{ genericPanel.ButtonOneText, "customtokens.panels.button_ok" }
					});
					Analytics.LogUntrustedChatPageView();
					MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
					UpdateUIState(UIState.Default);
				}
				return;
			}
			MonoSingleton<GameManager>.Instance.PauseGameSession();
			ILocalUser me = MixSession.User;
			LocalLinkedUser localLinkedUser = new LocalLinkedUser();
			localLinkedUser.Swid = me.Id;
			localLinkedUser.AgeBand = me.AgeBandType;
			MixSession.User.GetChildTrustPermission(localLinkedUser, delegate(IPermissionResult permissionResult)
			{
				if (permissionResult.Success)
				{
					me.GetAdultVerificationRequirements(delegate(IGetAdultVerificationRequirementsResult getAdultVerificationRequirements)
					{
						if (getAdultVerificationRequirements.Success)
						{
							if (!getAdultVerificationRequirements.IsRequired)
							{
								if (permissionResult.Status != ActivityApprovalStatus.Approved)
								{
								}
							}
							else
							{
								me.GetLinkedGuardians(delegate(IGetLinkedUsersResult getLinkedUsersResult)
								{
									if (getLinkedUsersResult.Success)
									{
										IEnumerable<ILinkedUser> linkedUsers = getLinkedUsersResult.LinkedUsers;
										IEnumerator<ILinkedUser> enumerator = linkedUsers.GetEnumerator();
										if (enumerator.MoveNext())
										{
											if (permissionResult.Status == ActivityApprovalStatus.Approved)
											{
												GenericPanel genericPanel2 = (GenericPanel)Singleton<PanelManager>.Instance.ShowPanel(Panels.GENERIC, false);
												if (!(genericPanel2 == null))
												{
													MonoSingleton<GameManager>.Instance.PauseGameSession();
													string value2 = ((chatThread is IOneOnOneChatThread) ? "customtokens.panels.safe_chat_text" : ((!chatThread.IsFakeOrLocal()) ? "customtokens.panels.safe_chat_text_group" : "customtokens.panels.safe_chat_text_new_group"));
													Singleton<PanelManager>.Instance.SetupTokens(new Dictionary<Text, string>
													{
														{ genericPanel2.TitleText, null },
														{ genericPanel2.MessageText, value2 },
														{ genericPanel2.ButtonOneText, "customtokens.panels.button_ok" }
													});
												}
											}
											else if (permissionResult.Status == ActivityApprovalStatus.Denied)
											{
												Singleton<PanelManager>.Instance.ShowPanel(Panels.SAFECHAT_SET_POPUP, false);
											}
											else
											{
												Singleton<PanelManager>.Instance.ShowPanel(Panels.REQUEST_OPENCHAT_POPUP, false);
											}
										}
										else
										{
											Singleton<PanelManager>.Instance.ShowPanel(Panels.SETUP_PC_POPUP, false);
										}
									}
								});
							}
							Analytics.LogUntrustedChatPageView();
							MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
							UpdateUIState(UIState.Default);
						}
					});
				}
			});
		}

		public void OnGroupOptionsClicked()
		{
			if (GroupOptions.isActiveAndEnabled)
			{
				return;
			}
			MonoSingleton<GagManager>.Instance.ClearGags();
			MonoSingleton<GameManager>.Instance.PauseGameSession();
			MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
			Analytics.LogGroupChatInfoAction(chatThread);
			if (chatThread is IOfficialAccountChatThread)
			{
				if (Singleton<PanelManager>.Instance.FindPanel(typeof(OfficialAccountsProfilePanel)) == null)
				{
					OfficialAccountsProfilePanel officialAccountsProfilePanel = (OfficialAccountsProfilePanel)Singleton<PanelManager>.Instance.ShowPanel(Panels.OFFICIAL_ACCOUNT_PROFILE);
					officialAccountsProfilePanel.Show(((IOfficialAccountChatThread)chatThread).OfficialAccount, (IOfficialAccountChatThread)chatThread);
				}
			}
			else
			{
				GroupOptions.Toggle(chatThread, this);
				ScrollView.Hide();
			}
		}

		public void OnForceUpdateBannerClicked()
		{
			ForceUpdate.GoToAppStore();
		}

		private void CheckAndAddTimestampForMessage(IChatMessage aMessage)
		{
			IChatMessage chatMessage = null;
			TimeSpan timeSpan = TimeSpan.MaxValue;
			if (this.IsNullOrDisposed() || chatThread == null)
			{
				return;
			}
			foreach (IChatMessage chatMessage2 in chatThread.ChatMessages)
			{
				if (chatMessage2 != aMessage && chatMessage2.Sent && chatMessage2.TimeSent < aMessage.TimeSent)
				{
					TimeSpan timeSpan2 = aMessage.TimeSent - chatMessage2.TimeSent;
					if (timeSpan2.TotalSeconds < timeSpan.TotalSeconds && timeSpan2 != timeSpan)
					{
						chatMessage = chatMessage2;
						timeSpan = timeSpan2;
					}
				}
			}
			TimeSpan timeSpan3 = aMessage.TimeSent - ((chatMessage != null) ? chatMessage.TimeSent : DateTime.MinValue);
			bool flag = ScrollView.IsAtBottom();
			if (timeSpan3.TotalSeconds > 300.0 || chatMessage == null)
			{
				DateTimeChatItem aGenerator = new DateTimeChatItem(DateStampText, chatThread, aMessage, ScrollView);
				long aId = ScrollView.Add(aGenerator, false);
				ScrollView.ResortItemInList(aId, ChatHelper.CompareScrollItems);
				ScrollView.Reposition();
				if (flag)
				{
					StartCoroutine(DelayScrollToBottom());
				}
			}
		}

		private long AddMessageToScrollView(IChatMessage aMessage, IScrollItem item, bool aIsNewMessage)
		{
			bool flag = ScrollView.IsAtBottom();
			if (aMessage.Sent)
			{
				CheckAndAddTimestampForMessage(aMessage);
			}
			BaseChatItem baseChatItem = (BaseChatItem)item;
			baseChatItem.FailedToSend = !aIsNewMessage && !aMessage.Sent && !MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread);
			long num;
			if (aMessage.Sent || !aIsNewMessage)
			{
				num = ScrollView.Add(item, false);
				ScrollView.ResortItemInList(num, ChatHelper.CompareScrollItems);
			}
			else
			{
				num = ScrollView.Add(item, false, false);
			}
			if (chatThread is LocalThread && !localReferences.Contains(aMessage))
			{
				localReferences.Add(aMessage);
			}
			clientMessages[aMessage] = num;
			if (num != -1 && flag && aIsNewMessage)
			{
				ScrollView.ScrollNewItemInFromBottom(num);
			}
			else if (flag)
			{
				StartCoroutine(DelayScrollToBottom());
			}
			if (aIsNewMessage)
			{
				if (!(aMessage is IGagMessage))
				{
					PlayAvatarTrigger((!aMessage.IsMine()) ? AvatarLeft : AvatarRight, "UserPostedChat");
					PlayAvatarTrigger((!aMessage.IsMine()) ? AvatarRight : AvatarLeft, "UserRecievedChat");
				}
				if (!aMessage.IsMine())
				{
					Singleton<SoundManager>.Instance.PlaySoundEvent((!aMessage.IsMediaMessage()) ? "MainApp/UI/SFX_5_ReceiveChat" : "MainApp/UI/SFX_3_MediaMessageReceive");
					chatsReceivedInThisThread++;
				}
				else
				{
					chatsSentInThisThread++;
				}
			}
			if (chatThread is IOfficialAccountChatThread && aIsNewMessage && !aMessage.IsMine())
			{
				Analytics.LogOAMessageRecieved(chatThread, aMessage);
			}
			return num;
		}

		private void UpdateState()
		{
			if (chatThread == null)
			{
				return;
			}
			if (chatThread is IOfficialAccountChatThread)
			{
				bool aState = MixSession.User.IsFollowingOfficialAccount(((IOfficialAccountChatThread)chatThread).OfficialAccount.AccountId);
				MediaBar.ToggleState(aState);
				ChatBar.ToggleState(aState);
			}
			else if (chatThread is IGroupChatThread)
			{
				if (!chatUISet)
				{
					chatUISet = true;
					GroupChatBevel.SetActive(true);
					SetSafeChatIcon();
				}
			}
			else
			{
				if (!(chatThread is IOneOnOneChatThread) || MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
				{
					return;
				}
				IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
				bool flag = thread.IsOtherUserFriend();
				if (!isFriendSet || isFriend != flag)
				{
					isFriendSet = true;
					isFriend = flag;
					if (!isFriend)
					{
						UpdateUIState(UIState.Default);
					}
					MediaBar.ToggleState(isFriend);
					ChatBar.ToggleState(isFriend);
					ToggleReportButton();
					SetSafeChatIcon();
				}
			}
		}

		private void UpdateHeader(string aTitle)
		{
			SubHeaderText.gameObject.SetActive(false);
			HeaderText.text = ((aTitle.Length <= 72) ? aTitle : (aTitle.Substring(0, 68) + "..."));
			if (chatThread is IOneOnOneChatThread && !MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
			{
				IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
				IRemoteChatMember otherUser = thread.GetOtherUser();
				if (otherUser != null && !string.IsNullOrEmpty(otherUser.NickOrFirstName()))
				{
					SubHeaderText.text = MixChat.FormatDisplayName(otherUser.DisplayName.Text);
					SubHeaderText.gameObject.SetActive(true);
				}
			}
		}

		private void UpdateScrollView()
		{
			RectTransform component = ScrollView.GetComponent<RectTransform>();
			float y = ChatBar.gameObject.transform.parent.GetComponent<RectTransform>().sizeDelta.y;
			float y2 = MixConstants.CANVAS_HEIGHT - y - Header.GetComponent<RectTransform>().rect.height;
			component.anchoredPosition = new Vector2(0f, y);
			gagStage.anchoredPosition = new Vector2(0f, y);
			component.sizeDelta = new Vector2(component.sizeDelta.x, y2);
		}

		private void UpdateAvatars(object o, AbstractAvatarChangedEventArgs args)
		{
			SkinAvatarHeads();
		}

		private void SkinningCallback(bool aSuccess, string aDnaSha)
		{
			if (chatThread is IOneOnOneChatThread)
			{
				if (isFriend || MonoSingleton<FakeFriendManager>.Instance.IsFake(chatThread))
				{
				}
				Singleton<ChatHelper>.Instance.UpdateGagSkins();
			}
		}

		private void SkinAvatarHeads()
		{
			if (!(chatThread is IOneOnOneChatThread) || !(AvatarRight != null) || !(AvatarLeft != null))
			{
				return;
			}
			IOneOnOneChatThread thread = chatThread as IOneOnOneChatThread;
			AvatarRight.SetActive(true);
			GameObject gameObject = AvatarRight.transform.Find("cube_rig").gameObject;
			AvatarRight.SetActive(false);
			if (gameObject != null)
			{
				MonoSingleton<AvatarManager>.Instance.SkinAvatar(gameObject, MixSession.User.Avatar, (AvatarFlags)0, delegate
				{
					if (!this.IsNullOrDisposed() && AvatarRight != null)
					{
						AvatarRight.SetActive(true);
						FlipAvatarGeo(AvatarRight);
					}
				});
			}
			AvatarLeft.SetActive(true);
			GameObject gameObject2 = AvatarLeft.transform.Find("cube_rig").gameObject;
			AvatarLeft.SetActive(false);
			IAvatarHolder otherAvatarHolder = thread.GetOtherAvatarHolder();
			if (!(gameObject2 != null) || otherAvatarHolder == null)
			{
				return;
			}
			MonoSingleton<AvatarManager>.Instance.SkinAvatar(gameObject2, otherAvatarHolder.Avatar, (AvatarFlags)0, delegate(bool success, string sha)
			{
				if (!this.IsNullOrDisposed() && AvatarLeft != null)
				{
					AvatarLeft.SetActive(true);
					SkinningCallback(success, sha);
				}
			});
		}

		private void SetupAvatarAnimationHelper(GameObject avatarObj, bool isMixbot)
		{
			AvatarAnimationHelper avatarAnimationHelper = avatarObj.GetComponent<AvatarAnimationHelper>();
			if (avatarAnimationHelper.IsNullOrDisposed())
			{
				avatarAnimationHelper = avatarObj.AddComponent<AvatarAnimationHelper>();
				avatarAnimationHelper.SetRandomDelay(5f, 10f, 50f);
			}
			Animator componentInChildren = avatarObj.GetComponentInChildren<Animator>();
			if (!componentInChildren.IsNullOrDisposed())
			{
				if (isMixbot)
				{
					componentInChildren.runtimeAnimatorController = mixbotAnimController;
				}
				avatarAnimationHelper.SetAnimator(componentInChildren);
			}
		}

		private void PlayAvatarTrigger(GameObject avatarObj, string eventName)
		{
			if (chatThread is IOneOnOneChatThread && !avatarObj.IsNullOrDisposed())
			{
				AvatarAnimationHelper component = avatarObj.GetComponent<AvatarAnimationHelper>();
				if (!component.IsNullOrDisposed())
				{
					component.SetTrigger(eventName);
				}
			}
		}

		private void SetAvatarAnimBool(GameObject avatarObj, string eventName, bool value)
		{
			if (chatThread is IOneOnOneChatThread && !avatarObj.IsNullOrDisposed())
			{
				AvatarAnimationHelper component = avatarObj.GetComponent<AvatarAnimationHelper>();
				if (!component.IsNullOrDisposed())
				{
					component.SetBool(eventName, value);
				}
			}
		}

		private void SetSafeChatIcon()
		{
			if (chatThread is IOfficialAccountChatThread)
			{
				SafeChatIcon.SetActive(false);
				return;
			}
			bool flag = true;
			if (chatThread is IOneOnOneChatThread)
			{
				IFriend friend = MixChat.FindFriend(((IOneOnOneChatThread)chatThread).GetOtherUser().Id);
				flag = !((IOneOnOneChatThread)chatThread).IsOtherUserFriend() || !friend.IsTrusted;
			}
			else
			{
				flag = !chatThread.TrustLevel.AllMembersTrustEachOther;
			}
			SafeChatIcon.SetActive(flag);
		}

		private void SetGroupOptionsButton()
		{
			if (chatThread != null && chatThread is IOfficialAccountChatThread && !((IOfficialAccountChatThread)chatThread).OfficialAccount.IsAvailable)
			{
				GroupOptionsButton.SetActive(false);
			}
			else if (chatThread != null && !(chatThread is IOneOnOneChatThread) && !chatThread.IsFakeOrLocal())
			{
				GroupOptionsButton.SetActive(true);
				GroupOptionsButton.GetComponent<Button>().interactable = MixSession.connection == MixSession.ConnectionState.ONLINE;
			}
			else
			{
				GroupOptionsButton.SetActive(false);
			}
		}

		private void OnReportPanelClosing(BasePanel aPanelClosing)
		{
			ToggleReportButton();
			mReportPanel.PanelClosedEvent -= OnReportPanelClosing;
			if (ScrollView != null)
			{
				ScrollView.Show();
			}
		}

		private void OnAddedToGroupChatThread(object sender, AbstractAddedToGroupChatThreadEventArgs e)
		{
			OnAddedToChatThread(e.ChatThread);
		}

		private void OnAddedToOfficialAccountThread(object sender, AbstractAddedToOfficialAccountThreadEventArgs e)
		{
		}

		private void OnAddedToOneOnOneChatThread(object sender, AbstractAddedToOneOnOneChatThreadEventArgs e)
		{
			OnAddedToChatThread(e.ChatThread);
		}

		private void OnAddedToChatThread(IChatThread aChatThread)
		{
			if (chatThread is LocalThread && aChatThread.MemberListMatchesThread(chatThread.RemoteMembers.Select((IRemoteChatMember fr) => fr.Id)))
			{
				(chatThread as LocalThread).SetRealThread(aChatThread);
			}
			else if (!chatThread.Equals(aChatThread))
			{
				AddThreadListeners(aChatThread);
			}
		}

		private void AddThreadListeners(IChatThread thread)
		{
			if (thread is IOneOnOneChatThread)
			{
				((IOneOnOneChatThread)thread).OnGagMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadGagMessageAddedEventArgs>(thread, OnGagMessageReceived);
			}
			if (thread is IGroupChatThread)
			{
				((IGroupChatThread)thread).OnGagMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadGagMessageAddedEventArgs>(thread, OnGagMessageReceived);
								}
			thread.OnTextMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadTextMessageAddedEventArgs>(thread, OnTextMessageReceived);
			thread.OnStickerMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadStickerMessageAddedEventArgs>(thread, OnStickerMessageReceived);
			thread.OnPhotoMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadPhotoMessageAddedEventArgs>(thread, OnPhotoMessageReceived);
			thread.OnVideoMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadVideoMessageAddedEventArgs>(thread, OnVideoMessageReceived);
			thread.OnMemberAddedMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadMemberAddedMessageAddedEventArgs>(thread, OnMemberAddedMessageReceived);
			thread.OnMemberRemovedMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadMemberRemovedMessageAddedEventArgs>(thread, OnMemberRemovedMessageReceived);
			thread.OnGameEventMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadGameEventMessageAddedEventArgs>(thread, OnGameEventMessageReceived);
			thread.OnGameStateMessageAdded += eventGenerator.AddEventHandler<AbstractChatThreadGameStateMessageAddedEventArgs>(thread, OnGameStateMessageReceived);
		}

		private void RemoveThreadListeners(IChatThread thread)
		{
			if (thread is IOneOnOneChatThread)
			{
				((IOneOnOneChatThread)thread).OnGagMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadGagMessageAddedEventArgs>(thread, OnGagMessageReceived);
			}
			if (thread is IGroupChatThread)
			{
				((IGroupChatThread)thread).OnGagMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadGagMessageAddedEventArgs>(thread, OnGagMessageReceived);
			}
			thread.OnTextMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadTextMessageAddedEventArgs>(thread, OnTextMessageReceived);
			thread.OnStickerMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadStickerMessageAddedEventArgs>(thread, OnStickerMessageReceived);
			thread.OnPhotoMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadPhotoMessageAddedEventArgs>(thread, OnPhotoMessageReceived);
			thread.OnVideoMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadVideoMessageAddedEventArgs>(thread, OnVideoMessageReceived);
			thread.OnMemberAddedMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadMemberAddedMessageAddedEventArgs>(thread, OnMemberAddedMessageReceived);
			thread.OnMemberRemovedMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadMemberRemovedMessageAddedEventArgs>(thread, OnMemberRemovedMessageReceived);
			thread.OnGameStateMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadGameStateMessageAddedEventArgs>(thread, OnGameStateMessageReceived);
			thread.OnGameEventMessageAdded -= eventGenerator.GetEventHandler<AbstractChatThreadGameEventMessageAddedEventArgs>(thread, OnGameEventMessageReceived);
		}

		private bool ShouldAddMessage(IChatThread aChatThread, IChatMessage aChatMessage)
		{
			if (!ChatHelper.ValidateMessage(aChatThread, aChatMessage))
			{
				return false;
			}
			if (!chatThread.Equals(aChatThread))
			{
				if (aChatThread is IOfficialAccountChatThread && ((IOfficialAccountChatThread)aChatThread).OfficialAccount.AccountId.Equals(FakeFriendManager.OAID))
				{
					return false;
				}
				if (!string.IsNullOrEmpty(aChatMessage.SenderId) && MixSession.User.Id != aChatMessage.SenderId)
				{
					MonoSingleton<LocalNotificationManager>.Instance.AddChatMessageNotification(chatThread, aChatThread, aChatMessage, Singleton<ChatHelper>.Instance.OnChatNotificationClicked);
				}
				if (unreadCountFromOtherThreads < 99 && MixSession.User.Id != aChatMessage.SenderId)
				{
					unreadCountFromOtherThreads++;
				}
				if (PendingCounterText != null)
				{
					PendingCounterText.gameObject.SetActive(true);
					PendingCounterText.text = "(" + unreadCountFromOtherThreads + ")";
				}
				return false;
			}
			IChatMessage chatMessage = aChatMessage;
			if (chatThread is LocalThread)
			{
				IChatMessage chatMessage2 = localReferences.FirstOrDefault((IChatMessage icm) => icm.GetSDKRef() == aChatMessage);
				if (chatMessage2 != null)
				{
					chatMessage = chatMessage2;
				}
			}
			if (chatMessage != null && clientMessages.ContainsKey(chatMessage))
			{
				if (!string.IsNullOrEmpty(aChatMessage.Id) && !addedMessages.ContainsKey(aChatMessage.Id))
				{
					addedMessages.Add(aChatMessage.Id, aChatMessage);
				}
				MessageUpdateInternal(aChatMessage);
				return false;
			}
			if (!(chatThread is LocalThread) && !string.IsNullOrEmpty(aChatMessage.Id) && addedMessages.ContainsKey(aChatMessage.Id))
			{
				return false;
			}
			if (!(chatThread is LocalThread) && !string.IsNullOrEmpty(aChatMessage.Id))
			{
				addedMessages.Add(aChatMessage.Id, aChatMessage);
			}
			return true;
		}

		private void SendEntitlement(BaseContentData aEntitlement, string targetId)
		{
			if (aEntitlement is Sticker)
			{
				IStickerMessage localMessage = null;
				localMessage = chatThread.SendStickerMessage(aEntitlement.GetUid(), actionGenerator.CreateAction(delegate(ISendStickerMessageResult result)
				{
					if (!this.IsNullOrDisposed() && chatThread != null)
					{
						if (result == null || !result.Success)
						{
							MessageUpdateInternal(localMessage);
						}
						else if (result != null)
						{
							CheckAndAddTimestampForMessage(localMessage);
						}
					}
				}));
				ScrollView.SetScrollToBottom();
				AddStickerMessage(chatThread, localMessage, true);
				Analytics.LogSendSticker(chatThread, aEntitlement.GetName());
				Singleton<SoundManager>.Instance.PlaySoundEvent("MainApp/UI/SFX_3_MediaMessage");
				chatMessagesSent++;
			}
			else
			{
				if (!(aEntitlement is Gag))
				{
					return;
				}
				IGagMessage localMessage2 = null;
				localMessage2 = chatThread.SendGagMessage(aEntitlement.GetUid(), targetId, actionGenerator.CreateAction(delegate(ISendGagMessageResult result)
				{
					if (!this.IsNullOrDisposed() && chatThread != null && localMessage2 != null)
					{
						if (result == null || !result.Success)
						{
							MessageUpdateInternal(localMessage2);
						}
						else if (result != null)
						{
							CheckAndAddTimestampForMessage(localMessage2);
						}
					}
				}));
				if (localMessage2 != null)
				{
					ScrollView.SetScrollToBottom();
					AddGagMessage(chatThread, localMessage2, true);
					Analytics.LogSendGag(chatThread, aEntitlement.GetName());
					Singleton<SoundManager>.Instance.PlaySoundEvent("MainApp/UI/SFX_3_MediaMessage");
					chatMessagesSent++;
				}
			}
		}

		private void AddChatMessage(IChatMessage message)
		{
			if (message is ITextMessage)
			{
				AddTextMessage(chatThread, message as ITextMessage, false);
			}
			else if (message is IStickerMessage)
			{
				AddStickerMessage(chatThread, message as IStickerMessage, false);
			}
			else if (message is IGagMessage)
			{
				AddGagMessage(chatThread, message as IGagMessage, false);
			}
			else if (message is IPhotoMessage)
			{
				AddPhotoMessage(chatThread, message as IPhotoMessage, false);
			}
			else if (message is IVideoMessage)
			{
				AddVideoMessage(chatThread, message as IVideoMessage, false);
			}
			else if (message is IGameStateMessage)
			{
				AddGameMessage(chatThread, message as IGameStateMessage, false);
			}
			else if (message is IGameEventMessage)
			{
				AddGameEventMessage(chatThread, message as IGameEventMessage, false);
			}
			else if (message is IChatMemberAddedMessage)
			{
				AddMemberAddedMessage(chatThread, message as IChatMemberAddedMessage, false);
			}
			else if (message is IChatMemberRemovedMessage)
			{
				AddMemberRemovedMessage(chatThread, message as IChatMemberRemovedMessage, false);
			}
		}

		private void OnTextMessageReceived(object sender, AbstractChatThreadTextMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddTextMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddTextMessage(IChatThread aChatThread, ITextMessage aTextMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aTextMessage))
			{
				IScrollItem item = new MixChatItem(aChatThread, aTextMessage, ScrollView);
				if (aIsNewMessage && !aTextMessage.IsMine())
				{
					if (AccessibilityManager.Instance != null)
					{
						AccessibilityManager.Instance.Speak(aTextMessage.Text);
					}
				}
				AddMessageToScrollView(aTextMessage, item, aIsNewMessage);
			}
		}

		private void OnStickerMessageReceived(object sender, AbstractChatThreadStickerMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddStickerMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddStickerMessage(IChatThread aChatThread, IStickerMessage aStickerMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aStickerMessage))
			{
				IScrollItem item = new MixStickerItem(aChatThread, aStickerMessage, ScrollView, aIsNewMessage);
				AddMessageToScrollView(aStickerMessage, item, aIsNewMessage);
			}
		}

		private void OnGagMessageReceived(object sender, AbstractChatThreadGagMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddGagMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddGagMessage(IChatThread aChatThread, IGagMessage aGagMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aGagMessage))
			{
				IScrollItem item = new MixGagItem(aChatThread, aGagMessage, ScrollView, aIsNewMessage);
				AddMessageToScrollView(aGagMessage, item, aIsNewMessage);
			}
		}

		private void OnGameStateMessageReceived(object sender, AbstractChatThreadGameStateMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddGameMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddGameMessage(IChatThread aChatThread, IGameStateMessage aGameStateMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aGameStateMessage))
			{
				IScrollItem item = new MixGamePostItem(aChatThread, aGameStateMessage, ScrollView, this);
				AddMessageToScrollView(aGameStateMessage, item, aIsNewMessage);
			}
		}

		private void OnGameEventMessageReceived(object sender, AbstractChatThreadGameEventMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				MixGameData mixGameData = JsonMapper.ToObject<MixGameData>((string)e.Message.GameStateMessage.State["GameData"]);
				BaseGameData gameData = Singleton<EntitlementsManager>.Instance.GetGameData(mixGameData.Entitlement);
				if (!string.Equals("fireworks", gameData.GetName()))
				{
					AddGameEventMessage(sender as IChatThread, e.Message, true);
				}
			}
		}

		private void AddGameEventMessage(IChatThread aChatThread, IGameEventMessage aGameEventMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aGameEventMessage))
			{
				IScrollItem item = new MixGameResponseItem(aChatThread, aGameEventMessage, ScrollView, this);
				AddMessageToScrollView(aGameEventMessage, item, aIsNewMessage);
			}
		}

		private void OnPhotoMessageReceived(object sender, AbstractChatThreadPhotoMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddPhotoMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddPhotoMessage(IChatThread aChatThread, IPhotoMessage aPhotoMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aPhotoMessage))
			{
				IScrollItem scrollItem = null;
				scrollItem = ((!aPhotoMessage.IsMessageSentByBot(aChatThread)) ? new MixBasePhotoItem(aChatThread, aPhotoMessage, ScrollView, this) : new MixOAPhotoItem(aChatThread, aPhotoMessage, ScrollView, this));
				AddMessageToScrollView(aPhotoMessage, scrollItem, aIsNewMessage);
			}
		}

		private void OnVideoMessageReceived(object sender, AbstractChatThreadVideoMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddVideoMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddVideoMessage(IChatThread aChatThread, IVideoMessage aVideoMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aVideoMessage))
			{
				IScrollItem scrollItem = null;
				scrollItem = ((!aVideoMessage.IsMessageSentByBot(aChatThread)) ? new MixBaseVideoItem(aChatThread, aVideoMessage, ScrollView) : new MixOAVideoItem(aChatThread, aVideoMessage, ScrollView));
				AddMessageToScrollView(aVideoMessage, scrollItem, aIsNewMessage);
			}
		}

		private void OnMemberAddedMessageReceived(object sender, AbstractChatThreadMemberAddedMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddMemberAddedMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddMemberAddedMessage(IChatThread aChatThread, IChatMemberAddedMessage aMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aMessage) && chatThread is IGroupChatThread)
			{
				IScrollItem item = new InformationalChatItem(DateStampText, aChatThread, aMessage, ScrollView);
				AddMessageToScrollView(aMessage, item, aIsNewMessage);
			}
		}

		private void OnMemberRemovedMessageReceived(object sender, AbstractChatThreadMemberRemovedMessageAddedEventArgs e)
		{
			if (!this.IsNullOrDisposed() && !comingFromPushNote)
			{
				AddMemberRemovedMessage(sender as IChatThread, e.Message, true);
			}
		}

		private void AddMemberRemovedMessage(IChatThread aChatThread, IChatMemberRemovedMessage aMessage, bool aIsNewMessage)
		{
			if (ShouldAddMessage(aChatThread, aMessage))
			{
				IScrollItem item = new InformationalChatItem(DateStampText, aChatThread, aMessage, ScrollView);
				AddMessageToScrollView(aMessage, item, aIsNewMessage);
			}
		}

		private void OnMemberAdded(object sender, AbstractChatThreadMemberAddedEventArgs e)
		{
			SetSafeChatIcon();
			UpdateHeader(chatThread.GetThreadTitle());
		}

		private void OnMemberRemoved(object sender, AbstractChatThreadMemberRemovedEventArgs e)
		{
			SetSafeChatIcon();
			UpdateHeader(chatThread.GetThreadTitle());
		}

		private void OnThreadTrustChanged(object sender, AbstractChatThreadTrustStatusChangedEventArgs e)
		{
			if (chatThread.Id == ((IChatThread)sender).Id)
			{
				SetSafeChatIcon();
			}
			if (((IChatThread)sender).TrustLevel.AllMembersTrustEachOther == e.TrustLevel.AllMembersTrustEachOther)
			{
			}
		}

		private void MessageUpdateInternal(IChatMessage aChatMessage)
		{
			if (this.IsNullOrDisposed() || base.gameObject == null)
			{
				return;
			}
			IChatMessage chatMessage = aChatMessage;
			if (chatThread is LocalThread)
			{
				IChatMessage chatMessage2 = localReferences.FirstOrDefault((IChatMessage icm) => icm.GetSDKRef() == aChatMessage);
				if (chatMessage2 != null)
				{
					chatMessage = chatMessage2;
				}
			}
			IScrollItem scrollItem = null;
			if (chatMessage != null && clientMessages != null && clientMessages.ContainsKey(chatMessage) && ScrollView != null)
			{
				scrollItem = ScrollView.Get(clientMessages[chatMessage]);
			}
			if (scrollItem != null)
			{
				((BaseChatItem)scrollItem).BaseUpdateClientMessage(aChatMessage);
				if (aChatMessage is ITextMessage)
				{
					ITextMessage textMessage = (ITextMessage)aChatMessage;
					if (AccessibilityManager.Instance != null)
					{
						AccessibilityManager.Instance.Speak(textMessage.Text);
					}
				}
				if (ScrollView != null && Singleton<ChatHelper>.Instance != null)
				{
					ScrollView.ResortItemInList(clientMessages[chatMessage], ChatHelper.CompareScrollItems);
					ScrollView.Reposition();
				}
			}
		}

		public void OnScrollGotPointerDown(PointerEventData aEventData)
		{
			if (MonoSingleton<GameManager>.Instance.PauseGameSession())
			{
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
			}
			else
			{
				UpdateUIState(UIState.Default);
			}
		}

		public void OnUserScrollToTop()
		{
			if (doneRetrieving && areMoreMessagesToLoad && chatRetriever != null)
			{
				LoadingMessageBar.SetActive(true);
				StartCoroutine(DelayedRetrieve(true));
			}
		}

		private IEnumerator DelayedRetrieve(bool aIsLoadedMoreMessages)
		{
			yield return new WaitForSeconds(0.25f);
			chatRetriever.RetrieveMessages(actionGenerator.CreateAction<IRetrieveChatThreadMessagesResult, bool>(DBRetrieverCallback, aIsLoadedMoreMessages));
		}

		private void UpdateUIState(UIState aState, string aSelection = null)
		{
			if (this.IsNullOrDisposed() || ChatBar == null || MediaBar == null || GagSendTray == null || KeyboardTray == null || MediaTray == null || CameraTray == null || GameTray == null)
			{
				return;
			}
			if (aState.Equals(UIState.AnimateDown))
			{
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
				animatingTray = true;
				return;
			}
			if (aState.Equals(UIState.Default))
			{
				MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
			}
			ChatBar.UpdateState(aState);
			MediaBar.UpdateState(aState);
			GagSendTray.UpdateState(aState == UIState.GagSendTray);
			KeyboardTray.UpdateState(aState == UIState.KeyboardTray);
			MediaTray.UpdateState(aState == UIState.MediaTray, aSelection);
			CameraTray.UpdateState(aState == UIState.CameraTray);
			GameTray.UpdateState(aState);
			if (chatFloor != null)
			{
				chatFloor.SetActive(false);
				chatFloor.SetActive(true);
			}
			else
			{
				chatFloor = GameObject.Find("ChatFloor");
			}
		}

		private IEnumerator WaitForStateChange(string aSelection)
		{
			IsStateChange = true;
			yield return new WaitForSeconds(0.1f);
			MediaBar.SetSelectedVisualStateOfToggle(aSelection, true);
			IsStateChange = false;
		}

		public void OnHidePreviewPanel()
		{
			MediaPreview.Hide();
		}

		public void OnSendEntitlement(BaseContentData aEntitlement)
		{
			MonoSingleton<NativeKeyboardManager>.Instance.Keyboard.Hide();
			string id = MixSession.User.Id;
			if (chatThread is IOneOnOneChatThread && (chatThread as IOneOnOneChatThread).GetOtherUser() != null)
			{
				id = (chatThread as IOneOnOneChatThread).GetOtherUser().Id;
			}
			SendEntitlement(aEntitlement, id);
		}

		public void OnShowNotification(string aString)
		{
			if ((bool)MonoSingleton<ConnectionManager>.Instance && !MonoSingleton<ConnectionManager>.Instance.IsBannerShowing)
			{
				NotificationBar.transform.Find("NotificationText").GetComponent<Text>().text = aString;
				NotificationBar.SetActive(true);
				return;
			}
			GenericPanel genericPanel = (GenericPanel)Singleton<PanelManager>.Instance.ShowPanel(Panels.GENERIC);
			Singleton<PanelManager>.Instance.SetupTokens(new Dictionary<Text, string>
			{
				{
					genericPanel.TitleText,
					string.Empty
				},
				{ genericPanel.MessageText, aString },
				{ genericPanel.ButtonOneText, "customtokens.panels.button_ok" }
			});
		}
	}
}
