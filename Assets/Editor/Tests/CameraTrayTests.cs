using Mix.Ui;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CameraTrayTests
{
	private const string PrefabPath = "Assets/Resources/prefabs/screens/chatmix/ChatMixScreen.prefab";

	private const string TrayPath = "UI_FG_Holder/UI_FG_ChatScreen/MediaInput/CameraTray/ComingSoon/";

	// The shipped prefab has these buttons disabled and faded ("coming soon"); a disabled
	// Button never fires onClick, so the tray must switch them on and make them readable.
	[TestCase("PhotoBtn")]
	[TestCase("VideoBtn")]
	[TestCase("LibraryBtn")]
	public void MakeButtonLive_OnTheRealPrefab_EnablesTheButtonAndRemovesTheFade(string buttonName)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
		try
		{
			Transform button = root.transform.Find(TrayPath + buttonName);
			Assert.IsNotNull(button, buttonName);
			Assert.IsFalse(button.GetComponent<Button>().interactable, "precondition: the prefab ships it disabled");
			CameraTray.MakeButtonLive(button);
			Assert.IsTrue(button.GetComponent<Button>().interactable);
			foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
			{
				Assert.AreEqual(1f, graphic.color.a, 0.001f, graphic.name);
			}
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// Disney shipped the Media (camera) tab switched off in the prefab and nothing in the
	// code turns it on, so the tray was unreachable.
	[Test]
	public void ShowMediaTab_OnTheRealPrefab_ActivatesTheHiddenMediaToggle()
	{
		GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
		try
		{
			Transform bar = root.transform.Find("UI_FG_Holder/UI_FG_ChatScreen/MediaInput/MediaBar");
			Assert.IsNotNull(bar);
			Assert.IsFalse(bar.Find("Media").gameObject.activeSelf, "precondition: the prefab ships the Media tab hidden");
			Assert.IsTrue(MediaBar.ShowMediaTab(bar));
			Assert.IsTrue(bar.Find("Media").gameObject.activeSelf);
			Assert.IsNotNull(bar.Find("Media").GetComponent<Toggle>());
			Assert.IsFalse(MediaBar.ShowMediaTab(bar.Find("Keyboard")), "no Media child: nothing to show");
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}
}
