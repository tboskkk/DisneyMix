using System;
using System.Collections.Generic;
using System.Linq;
using Disney.Mix.SDK;
using Mix;
using Mix.Data;
using Mix.Entitlements;
using Mix.Localization;
using Mix.Session.Extensions;

public static class MessageUtils
{
	public static string GetDisplayTextForMessage(IChatThread aChatThread, IChatMessage aChatMessage)
	{
		if (aChatMessage == null || Singleton<Localizer>.Instance == null)
		{
			return string.Empty;
		}
		try
		{
			string newValue = string.Empty;
			if (aChatMessage.SenderId != null)
			{
				newValue = aChatThread.GetNickOrDisplayById(aChatMessage.SenderId);
			}
			if (aChatMessage is ITextMessage)
			{
				return (aChatMessage as ITextMessage).Text;
			}
			if (aChatMessage is IStickerMessage)
			{
				if (aChatMessage.IsMine())
				{
					return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_sticker");
				}
				string contentId = ((IStickerMessage)aChatMessage).ContentId;
				Sticker stickerData = Singleton<EntitlementsManager>.Instance.GetStickerData(contentId);
				if (stickerData == null)
				{
					// Sticker not in this build's catalog or its bundle is unavailable.
					return string.Empty;
				}
				string uid = stickerData.GetUid();
				string aToken = "ContentData.Sticker." + uid + ".PUSH_NOTE";
				string text = Singleton<Localizer>.Instance.getString(aToken);
				if (text == Localizer.NO_TOKEN)
				{
					text = string.Empty;
				}
				return text;
			}
			if (aChatMessage is IGagMessage)
			{
				if (aChatMessage.IsMine())
				{
					return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_gag");
				}
				return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_received_gag").Replace("#avatarName#", newValue);
			}
			if (aChatMessage is IPhotoMessage)
			{
				if (aChatMessage.IsMessageSentByBot(aChatThread))
				{
					return (aChatMessage as IPhotoMessage).Caption;
				}
				if (aChatMessage.IsMine())
				{
					return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_photo");
				}
				return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_received_photo").Replace("#avatarName#", newValue);
			}
			if (aChatMessage is IVideoMessage)
			{
				if (aChatMessage.IsMessageSentByBot(aChatThread))
				{
					return (aChatMessage as IVideoMessage).Caption;
				}
				if (aChatMessage.IsMine())
				{
					return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_video");
				}
				return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_received_video").Replace("#avatarName#", newValue);
			}
			if (aChatMessage is IGameStateMessage)
			{
				if (aChatMessage.IsMine())
				{
					return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_game_invite");
				}
				return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_received_game_invite").Replace("#avatarName#", newValue);
			}
			if (aChatMessage is IGameEventMessage)
			{
				if (aChatMessage.IsMine())
				{
					return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_game_response");
				}
				return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_received_game_response").Replace("#avatarName#", newValue);
			}
			if (aChatMessage is IChatMemberAddedMessage)
			{
				IEnumerable<string> memberIds = ((IChatMemberAddedMessage)aChatMessage).MemberIds;
				string newValue2 = BuildDisplayableMemberList(aChatThread, memberIds);
				string aToken2 = ((memberIds.Count() < 2) ? "customtokens.conversations.avatar_sent_group_join" : "customtokens.conversations.avatar_sent_group_join_plural");
				return Singleton<Localizer>.Instance.getString(aToken2).Replace("#avatarName#", newValue2);
			}
			if (aChatMessage is IChatMemberRemovedMessage)
			{
				newValue = aChatThread.GetNickOrDisplayById(((IChatMemberRemovedMessage)aChatMessage).MemberId);
				return Singleton<Localizer>.Instance.getString("customtokens.conversations.avatar_sent_group_left").Replace("#avatarName#", newValue);
			}
			if (aChatMessage.GetType() != null)
			{
			}
			return string.Empty;
		}
		catch (Exception exception)
		{
			Log.Exception(exception);
			return string.Empty;
		}
	}

	public static string BuildDisplayableMemberList(IChatThread chatThread, IEnumerable<string> memberIds)
	{
		string text = string.Empty;
		for (int i = 0; i < memberIds.Count(); i++)
		{
			string nickOrDisplayById = chatThread.GetNickOrDisplayById(memberIds.ElementAt(i));
			if (!string.IsNullOrEmpty(nickOrDisplayById))
			{
				text += ((i != memberIds.Count() - 1) ? (nickOrDisplayById + ", ") : nickOrDisplayById);
			}
		}
		return text;
	}
}
