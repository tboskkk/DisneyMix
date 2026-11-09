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

		private bool isActive;

		public void Init(INativeCamera aListener)
		{
			listener = aListener;
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
				camera.ShowCameraOverlay(listener);
				camera.TakePhoto();
			});
			Wire(root, "VideoBtn", delegate
			{
				camera.ShowCameraOverlay(listener);
				camera.RecordVideo();
			});
			Wire(root, "LibraryBtn", delegate
			{
				camera.ShowCameraOverlay(listener);
				camera.PickFromLibrary();
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
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(action);
		}
	}
}
