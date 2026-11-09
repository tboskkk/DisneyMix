using System;
using Disney.Native;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mix.Ui
{
	public class CameraTray : MonoBehaviour
	{
		public GameObject statusMessage;

		private INativeCamera listener;

		private Func<bool> canSendMedia;

		private Action onMediaUnavailable;

		private bool isActive;

		// aCanSendMedia is checked on every tap, before the camera opens, so a user is never
		// sent to take a photo that the current conversation cannot send.
		public void Init(INativeCamera aListener, Func<bool> aCanSendMedia, Action aOnMediaUnavailable)
		{
			listener = aListener;
			canSendMedia = aCanSendMedia;
			onMediaUnavailable = aOnMediaUnavailable;
			// `var`: a global-namespace NativeCamera (the plugin) shadows Disney.Native.NativeCamera by name here.
			var camera = MonoSingleton<NativeCameraManager>.Instance.Native;
			if (camera == null || !camera.IsSupported)
			{
				return;
			}
			Transform root = base.transform.Find("ComingSoon");
			if (root == null)
			{
				Debug.LogWarning("CameraTray: missing ComingSoon");
				return;
			}
			SetActive(root, "ComingSoonText", false);
			Wire(root, "PhotoBtn", delegate
			{
				if (MediaAvailable())
				{
					camera.ShowCameraOverlay(listener);
					camera.TakePhoto();
				}
			});
			// Photos only for now: video playback has no working player (see iceberg/video-playback-wip).
			SetActive(root, "VideoBtn", false);
			Wire(root, "LibraryBtn", delegate
			{
				if (MediaAvailable())
				{
					camera.ShowCameraOverlay(listener);
					camera.PickFromLibrary();
				}
			});
			if (!global::NativeCamera.DeviceHasCamera())
			{
				SetActive(root, "PhotoBtn", false);
				SetActive(root, "VideoBtn", false);
			}
		}

		public void UpdateState(bool aState)
		{
			base.gameObject.SetActive(aState);
		}

		// The shipped prefab has these buttons disabled and faded for the "coming soon"
		// look; a disabled Button never fires onClick.
		public static void MakeButtonLive(Transform aButton)
		{
			Button button = aButton.GetComponent<Button>();
			if (button != null)
			{
				button.interactable = true;
			}
			Graphic[] graphics = aButton.GetComponentsInChildren<Graphic>(true);
			foreach (Graphic graphic in graphics)
			{
				Color color = graphic.color;
				color.a = 1f;
				graphic.color = color;
			}
		}

		private bool MediaAvailable()
		{
			if (canSendMedia == null || canSendMedia())
			{
				return true;
			}
			if (onMediaUnavailable != null)
			{
				onMediaUnavailable();
			}
			return false;
		}

		private static void SetActive(Transform root, string childName, bool active)
		{
			Transform child = root.Find(childName);
			if (child == null)
			{
				Debug.LogWarning("CameraTray: missing " + root.name + "/" + childName);
				return;
			}
			child.gameObject.SetActive(active);
		}

		private static void Wire(Transform root, string childName, UnityAction action)
		{
			Transform child = root.Find(childName);
			Button button = ((child != null) ? child.GetComponent<Button>() : null);
			if (button == null)
			{
				Debug.LogWarning("CameraTray: missing button " + root.name + "/" + childName);
				return;
			}
			MakeButtonLive(child);
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(action);
		}
	}
}
