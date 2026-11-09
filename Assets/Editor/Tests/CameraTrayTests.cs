using System.Linq;
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

	private const string AtlasPath = "Assets/Texture2D/SpriteAtlasTexture-ui_common (Group 0)-1024x2048-fmt4.png";

	// The atlas meta shipped a corrupt custom outline for icon_camera, so Unity built a tiny
	// partial mesh and the Photo button showed a broken fragment of the icon.
	[Test]
	public void IconCamera_SpriteMesh_CoversTheWholeIcon()
	{
		Sprite[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(AtlasPath).OfType<Sprite>().ToArray();
		foreach (string name in new string[] { "icon_back", "ui_slider_circle", "icon_camera", "icon_upgradetrust", "icon_play_clear", "icon_officialaccount", "icon_stickers", "icon_save" })
		{
			Sprite other = sprites.First((Sprite s) => s.name == name);
			Debug.Log("SPRITEMESH " + name + " " + MeshCoverage(other));
		}
		Sprite sprite = sprites.Single((Sprite s) => s.name == "icon_camera");
		Vector2 coverage = MeshCoverage(sprite);
		Assert.GreaterOrEqual(coverage.x, 0.9f, "mesh width / sprite width");
		Assert.GreaterOrEqual(coverage.y, 0.9f, "mesh height / sprite height");
	}

	private static Vector2 MeshCoverage(Sprite sprite)
	{
		Vector2[] v = sprite.vertices;
		float width = (v.Max((Vector2 p) => p.x) - v.Min((Vector2 p) => p.x)) * sprite.pixelsPerUnit;
		float height = (v.Max((Vector2 p) => p.y) - v.Min((Vector2 p) => p.y)) * sprite.pixelsPerUnit;
		return new Vector2(width / sprite.rect.width, height / sprite.rect.height);
	}
}
