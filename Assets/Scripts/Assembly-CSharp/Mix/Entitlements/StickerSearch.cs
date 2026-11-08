using System.Collections.Generic;
using System.Text;
using Mix.Data;
using UnityEngine;

namespace Mix.Entitlements
{
	public class StickerSearch
	{
		private struct StickerTagInfo
		{
			private string mTagUID;

			private string mStickerUIDs;

			public string TagUID
			{
				get
				{
					return mTagUID;
				}
			}

			public string StickerUIDs
			{
				get
				{
					return mStickerUIDs;
				}
			}

			public StickerTagInfo(string aTagUID, string aStickerUIDs)
			{
				mTagUID = aTagUID;
				mStickerUIDs = aStickerUIDs;
			}
		}

		private const int UID_LENGTH = 32;

		private int mSubsetStart;

		private List<int> mSubsetStartCache;

		private int mSubsetEnd;

		private List<int> mSubsetEndCache;

		private StringBuilder mCurrentSearchTerm;

		private List<StickerTagInfo> mStickerTags;

		public StringBuilder CurrentSearchTerm
		{
			get
			{
				return mCurrentSearchTerm;
			}
		}

		public StickerSearch(List<TagMap> aList)
		{
			mStickerTags = new List<StickerTagInfo>();
			foreach (TagMap a in aList)
			{
				mStickerTags.Add(new StickerTagInfo(a.text, a.uids));
			}
		}

		public StickerSearch(List<Sticker_Tag> aStickerTags)
		{
			mStickerTags = new List<StickerTagInfo>();
			for (int i = 0; i < aStickerTags.Count; i++)
			{
				mStickerTags.Add(new StickerTagInfo(aStickerTags[i].GetUid(), aStickerTags[i].GetTagMap()));
			}
		}

		public StickerSearch(int aCount, List<string> aTagUIDs, List<string> aStickerUIDs)
		{
			mStickerTags = new List<StickerTagInfo>();
			for (int i = 0; i < aCount; i++)
			{
				mStickerTags.Add(new StickerTagInfo(aTagUIDs[i], aStickerUIDs[i]));
			}
		}

		public void StartNewSearch()
		{
			mSubsetStart = 0;
			mSubsetStartCache = new List<int>();
			mSubsetEnd = mStickerTags.Count;
			mSubsetEndCache = new List<int>();
			mCurrentSearchTerm = new StringBuilder();
		}

		public List<Sticker> AddCharToCurrentEntry(char aLastChar)
		{
			List<Sticker> result = new List<Sticker>();
			if (mStickerTags.Count > 0)
			{
				if (mSubsetEnd != -1)
				{
					int length = mCurrentSearchTerm.Length;
					mCurrentSearchTerm.Append(aLastChar);
					int num = BinarySearch(aLastChar, length, mSubsetStart, (mSubsetEnd + mSubsetStart) / 2, mSubsetEnd);
					if (num != -1)
					{
						FindRange(num, length);
					}
					else
					{
						mSubsetStart = -1;
						mSubsetEnd = -1;
					}
				}
				if (mSubsetEnd != -1)
				{
					List<string> resultUIDs = GetResultUIDs();
					result = GetStickersFromUIDList(resultUIDs);
				}
			}
			return result;
		}

		public List<Sticker> AddStringToCurrentEntry(string aLastString)
		{
			for (int i = 0; i < aLastString.Length; i++)
			{
				AddCharToCurrentEntry(aLastString[i]);
			}
			return GetStickersFromUIDList(GetResultUIDs());
		}

		public List<Sticker> RemoveCharFromCurrentEntry()
		{
			List<Sticker> result = null;
			if (mCurrentSearchTerm.Length > 0)
			{
				mCurrentSearchTerm.Remove(mCurrentSearchTerm.Length - 1, 1);
				int length = mCurrentSearchTerm.Length;
				if (length >= 0)
				{
					mSubsetStart = mSubsetStartCache[length];
					mSubsetEnd = mSubsetEndCache[length];
				}
				else
				{
					mSubsetStart = -1;
					mSubsetEnd = -1;
				}
				result = GetStickersFromUIDList(GetResultUIDs());
			}
			return result;
		}

		public Vector2 AddCharTest(char aLastChar)
		{
			if (mStickerTags.Count > 0 && mSubsetEnd != -1)
			{
				int length = mCurrentSearchTerm.Length;
				mCurrentSearchTerm.Append(aLastChar);
				int num = BinarySearch(aLastChar, length, mSubsetStart, (mSubsetEnd + mSubsetStart) / 2, mSubsetEnd);
				if (num != -1)
				{
					FindRange(num, length);
				}
				else
				{
					mSubsetStart = -1;
					mSubsetEnd = -1;
				}
			}
			return new Vector2(mSubsetStart, mSubsetEnd);
		}

		public Vector2 AddStringTest(string aLastString)
		{
			for (int i = 0; i < aLastString.Length; i++)
			{
				AddCharToCurrentEntry(aLastString[i]);
			}
			return new Vector2(mSubsetStart, mSubsetEnd);
		}

		public Vector2 RemoveCharTest()
		{
			int index = mCurrentSearchTerm.Length - 1;
			if (mCurrentSearchTerm.Length > 1)
			{
				mCurrentSearchTerm.Remove(mCurrentSearchTerm.Length - 1, 1);
				mSubsetStart = mSubsetStartCache[index];
				mSubsetEnd = mSubsetEndCache[index];
			}
			else
			{
				mSubsetStart = -1;
				mSubsetEnd = -1;
			}
			return new Vector2(mSubsetStart, mSubsetEnd);
		}

		private int BinarySearch(char aSearchChar, int aCharIndex, int aStart, int aMid, int aEnd)
		{
			int num = 0;
			if (aCharIndex < mStickerTags[aMid].TagUID.Length)
			{
				num = aSearchChar - mStickerTags[aMid].TagUID[aCharIndex];
				if (num == 0)
				{
					return aMid;
				}
			}
			int aMid2 = (aMid + aStart) / 2;
			if (aMid - aStart >= 1 && num <= 0)
			{
				int num2 = BinarySearch(aSearchChar, aCharIndex, aStart, aMid2, aMid);
				if (num2 != -1)
				{
					return num2;
				}
			}
			int aMid3 = (aEnd + aMid + 1) / 2;
			if (aEnd - aMid > 1 && num >= 0)
			{
				int num3 = BinarySearch(aSearchChar, aCharIndex, aMid + 1, aMid3, aEnd);
				if (num3 != -1)
				{
					return num3;
				}
			}
			return -1;
		}

		private void FindRange(int aInitialIndex, int aCharIndex)
		{
			UpdateSubsetCaches();
			int num = aInitialIndex;
			char c = mStickerTags[aInitialIndex].TagUID[aCharIndex];
			while (num >= mSubsetStart && aCharIndex < mStickerTags[num].TagUID.Length && mStickerTags[num].TagUID[aCharIndex] == c)
			{
				num--;
			}
			mSubsetStart = num + 1;
			for (num = aInitialIndex; num < mSubsetEnd && mStickerTags[num].TagUID[aCharIndex] == c; num++)
			{
			}
			mSubsetEnd = num;
		}

		private List<string> GetResultUIDs()
		{
			List<string> list = new List<string>();
			for (int i = mSubsetStart; i < mSubsetEnd; i++)
			{
				List<string> list2 = ParseTagMap(mStickerTags[i].StickerUIDs);
				for (int j = 0; j < list2.Count; j++)
				{
					if (list.IndexOf(list2[j]) == -1)
					{
						list.Add(list2[j]);
					}
				}
			}
			return list;
		}

		private List<string> ParseTagMap(string aTagMap)
		{
			int num = 0;
			int num2 = 32;
			List<string> list = new List<string>();
			while (num2 <= aTagMap.Length)
			{
				string item = aTagMap.Substring(num, 32);
				num = num2;
				num2 += 32;
				while (num < aTagMap.Length && (aTagMap[num] == ',' || aTagMap[num] == ' '))
				{
					num++;
					num2++;
				}
				list.Add(item);
			}
			return list;
		}

		private List<Sticker> GetStickersFromUIDList(List<string> aUIDList)
		{
			List<Sticker> list = new List<Sticker>();
			for (int i = 0; i < aUIDList.Count; i++)
			{
				Sticker stickerData = Singleton<EntitlementsManager>.Instance.GetStickerData(aUIDList[i]);
				if (stickerData != null)
				{
					list.Add(stickerData);
				}
			}
			return list;
		}

		private void UpdateSubsetCaches()
		{
			mSubsetStartCache.Add(mSubsetStart);
			mSubsetEndCache.Add(mSubsetEnd);
		}
	}
}
