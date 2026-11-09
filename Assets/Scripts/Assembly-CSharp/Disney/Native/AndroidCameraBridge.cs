using System;
using System.IO;
using UnityEngine;

namespace Disney.Native
{
	// Replaces the missing com.disney.nativecamera Java plugin: hands off to the
	// system camera / photo picker (yasirkula NativeCamera + NativeGallery) and
	// feeds the result to the listener in the JSON shape ChatController expects.
	// The yasirkula camera plugin class is global::NativeCamera; plain NativeCamera
	// here is the Disney.Native base class.
	public class AndroidCameraBridge : NativeCamera
	{
		private readonly MediaGate gate = new MediaGate();

		public override bool IsSupported
		{
			get
			{
				return true;
			}
		}

		public override void TakePhoto()
		{
			if (!TryBegin())
			{
				return;
			}
			global::NativeCamera.TakePicture(delegate(string path)
			{
				OnPhotoPath(path, true);
			}, MediaPrep.MaxPhotoSize);
		}

		public override void RecordVideo()
		{
			if (!TryBegin())
			{
				return;
			}
			global::NativeCamera.RecordVideo(delegate(string path)
			{
				OnVideoPath(path, true);
			}, global::NativeCamera.Quality.Medium, MediaPrep.MaxVideoSeconds, MediaPrep.MaxVideoBytes);
		}

		public override void PickFromLibrary()
		{
			if (!TryBegin())
			{
				return;
			}
			NativeGallery.GetMixedMediaFromGallery(delegate(string path)
			{
				gate.Exit();
				if (string.IsNullOrEmpty(path))
				{
					// A cancel is silent, but on Android 6-12 a denied storage permission also gives null.
					if (!NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image | NativeGallery.MediaType.Video))
					{
						Fail(CameraError.PERMISSION_DENIED);
					}
					return;
				}
				NativeGallery.MediaType type = NativeGallery.GetMediaTypeOfFile(path);
				if ((type & NativeGallery.MediaType.Video) != 0)
				{
					OnVideoPath(path, false);
				}
				else if ((type & NativeGallery.MediaType.Image) != 0)
				{
					OnPhotoPath(path, false);
				}
				else
				{
					Fail(CameraError.DECODE_FAILED);
				}
			}, NativeGallery.MediaType.Image | NativeGallery.MediaType.Video);
		}

		// The plugins' own IsBusy() is hard-wired to false on Android, so this keeps a
		// second tap (or Photo then Library) from launching a second activity.
		private bool TryBegin()
		{
			if (global::NativeCamera.IsCameraBusy() || NativeGallery.IsMediaPickerBusy())
			{
				return false;
			}
			return gate.TryEnter(Time.realtimeSinceStartup);
		}

		private void Fail(CameraError error)
		{
			if (Listener != null)
			{
				Listener.CameraError(error);
			}
		}

		// Delivery is separate from decoding so a failure inside the chat (which already
		// logs) is not mis-reported to the user as a decode error.
		private static void Deliver(Action send)
		{
			try
			{
				send();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		// A null path from the camera is either a cancel (silent) or a denied
		// permission (tell the user).
		private bool HandleNullPath(string path, bool fromCamera, bool isPicture)
		{
			if (!string.IsNullOrEmpty(path))
			{
				return false;
			}
			if (fromCamera && !global::NativeCamera.CheckPermission(isPicture))
			{
				Fail(CameraError.PERMISSION_DENIED);
			}
			return true;
		}

		private void OnPhotoPath(string path, bool fromCamera)
		{
			gate.Exit();
			if (HandleNullPath(path, fromCamera, true))
			{
				return;
			}
			string json;
			Texture2D tex = null;
			try
			{
				tex = global::NativeCamera.LoadImageAtPath(path, MediaPrep.MaxPhotoSize, false, false);
				if (tex == null)
				{
					Fail(CameraError.DECODE_FAILED);
					return;
				}
				int width = tex.width;
				int height = tex.height;
				string jpegPath = MediaPrep.WriteJpeg(tex, MediaPrep.CacheDir);
				json = MediaPrep.PhotoJson(jpegPath, width, height);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Fail(CameraError.DECODE_FAILED);
				return;
			}
			finally
			{
				if (tex != null)
				{
					Destroy(tex);
				}
			}
			Deliver(delegate
			{
				OnSendPhoto(json);
			});
		}

		private void OnVideoPath(string path, bool fromCamera)
		{
			gate.Exit();
			if (HandleNullPath(path, fromCamera, false))
			{
				return;
			}
			string json;
			Texture2D thumb = null;
			try
			{
				long bytes = new FileInfo(path).Length;
				global::NativeCamera.VideoProperties props = global::NativeCamera.GetVideoProperties(path);
				switch (MediaPrep.CheckVideo(bytes, props.duration, fromCamera))
				{
				case VideoCheck.TooLong:
					Fail(CameraError.VIDEO_TOO_LONG);
					return;
				case VideoCheck.TooLarge:
					Fail(CameraError.FILE_TOO_LARGE);
					return;
				}
				if (props.width <= 0 || props.height <= 0)
				{
					Fail(CameraError.DECODE_FAILED);
					return;
				}
				int videoWidth;
				int videoHeight;
				MediaPrep.OrientedSize(props.width, props.height, props.rotation, out videoWidth, out videoHeight);
				thumb = global::NativeCamera.GetVideoThumbnail(path, MediaPrep.ThumbSize, -1.0, false, false);
				if (thumb == null)
				{
					Fail(CameraError.DECODE_FAILED);
					return;
				}
				int thumbWidth = thumb.width;
				int thumbHeight = thumb.height;
				string thumbPath = MediaPrep.WriteJpeg(thumb, MediaPrep.CacheDir);
				// The plugins reuse fixed file names (VID_camera.mp4, pickedMedia.*), so keep our own copy.
				string extension = Path.GetExtension(path);
				string videoPath = MediaPrep.CopyToCache(path, MediaPrep.CacheDir, string.IsNullOrEmpty(extension) ? ".mp4" : extension);
				json = MediaPrep.VideoJson(videoPath, MediaPrep.Bitrate(bytes, props.duration), props.duration, videoWidth, videoHeight, thumbPath, thumbWidth, thumbHeight);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Fail(CameraError.DECODE_FAILED);
				return;
			}
			finally
			{
				if (thumb != null)
				{
					Destroy(thumb);
				}
			}
			// PRIVACY: unlike photos (re-encoded, so EXIF/GPS is dropped), the video file is sent on
			// as-is and its container metadata can include location. Whatever uploads it MUST strip
			// that metadata (remux/transcode, e.g. MediaMuxer or server-side) before it leaves the device.
			Deliver(delegate
			{
				OnSendVideo(json);
			});
		}
	}
}
