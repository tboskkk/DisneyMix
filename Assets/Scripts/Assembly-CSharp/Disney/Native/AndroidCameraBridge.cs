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
		public override bool IsSupported
		{
			get
			{
				return true;
			}
		}

		public override void TakePhoto()
		{
			if (IsBusy())
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
			if (IsBusy())
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
			if (IsBusy())
			{
				return;
			}
			NativeGallery.GetMixedMediaFromGallery(delegate(string path)
			{
				if (string.IsNullOrEmpty(path))
				{
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

		private static bool IsBusy()
		{
			return global::NativeCamera.IsCameraBusy() || NativeGallery.IsMediaPickerBusy();
		}

		private void Fail(CameraError error)
		{
			if (Listener != null)
			{
				Listener.CameraError(error);
			}
		}

		// A null path from the camera is either a cancel (silent) or a denied
		// permission (tell the user); the picker needs no permission.
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
			if (HandleNullPath(path, fromCamera, true))
			{
				return;
			}
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
				OnSendPhoto(MediaPrep.PhotoJson(jpegPath, width, height));
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Fail(CameraError.DECODE_FAILED);
			}
			finally
			{
				if (tex != null)
				{
					Destroy(tex);
				}
			}
		}

		private void OnVideoPath(string path, bool fromCamera)
		{
			if (HandleNullPath(path, fromCamera, false))
			{
				return;
			}
			Texture2D thumb = null;
			try
			{
				long bytes = new FileInfo(path).Length;
				global::NativeCamera.VideoProperties props = global::NativeCamera.GetVideoProperties(path);
				switch (MediaPrep.CheckVideo(bytes, props.duration))
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
				OnSendVideo(MediaPrep.VideoJson(path, MediaPrep.Bitrate(bytes, props.duration), props.duration, videoWidth, videoHeight, thumbPath, thumbWidth, thumbHeight));
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Fail(CameraError.DECODE_FAILED);
			}
			finally
			{
				if (thumb != null)
				{
					Destroy(thumb);
				}
			}
		}
	}
}
