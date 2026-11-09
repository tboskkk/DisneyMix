using System;
using System.IO;
using System.Text;
using LitJson;
using UnityEngine;

namespace Disney.Native
{
	public enum VideoCheck
	{
		Ok,
		TooLong,
		TooLarge
	}

	// Pure helpers for the Android photo/video capture bridge: limits, size and
	// bitrate maths, the JSON ChatController reads, and the JPEG cache.
	public static class MediaPrep
	{
		public const int MaxPhotoSize = 1280;

		public const int JpegQuality = 85;

		public const int MaxVideoSeconds = 30;

		public const long MaxVideoBytes = 25L * 1024 * 1024;

		public const int ThumbSize = 460;

		// Camera apps stop a recording slightly past the limit we ask for, so a camera
		// clip is allowed this much extra before it is rejected. Gallery picks stay exact.
		public const long CameraDurationSlackMs = 2000L;

		public const long CameraSizeSlackBytes = 2L * 1024 * 1024;

		// Fake-friend chat messages keep these paths in the local database, so the media
		// lives somewhere that survives a restart and is pruned by age instead of wiped.
		public static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(7.0);

		public static string CacheDir
		{
			get
			{
				return Path.Combine(Application.persistentDataPath, "mixmedia");
			}
		}

		public static void OrientedSize(int width, int height, float rotation, out int outW, out int outH)
		{
			int r = ((int)Mathf.Round(rotation) % 360 + 360) % 360;
			if (r == 90 || r == 270)
			{
				outW = height;
				outH = width;
			}
			else
			{
				outW = width;
				outH = height;
			}
		}

		public static VideoCheck CheckVideo(long fileBytes, long durationMs, bool fromCamera = false)
		{
			long maxMs = MaxVideoSeconds * 1000L + (fromCamera ? CameraDurationSlackMs : 0L);
			long maxBytes = MaxVideoBytes + (fromCamera ? CameraSizeSlackBytes : 0L);
			if (durationMs > maxMs)
			{
				return VideoCheck.TooLong;
			}
			if (fileBytes > maxBytes)
			{
				return VideoCheck.TooLarge;
			}
			return VideoCheck.Ok;
		}

		public static int Bitrate(long fileBytes, long durationMs)
		{
			if (durationMs <= 0 || fileBytes <= 0)
			{
				return 0;
			}
			double bitsPerSecond = (double)fileBytes * 8000.0 / durationMs;
			return (int)Math.Min(bitsPerSecond, int.MaxValue);
		}

		public static string PhotoJson(string path, int width, int height)
		{
			StringBuilder sb = new StringBuilder();
			JsonWriter w = new JsonWriter(sb);
			w.WriteObjectStart();
			w.WritePropertyName("photoPath");
			w.Write(path);
			w.WritePropertyName("photoWidth");
			w.Write(width);
			w.WritePropertyName("photoHeight");
			w.Write(height);
			w.WriteObjectEnd();
			return sb.ToString();
		}

		public static string VideoJson(string videoPath, int bitrate, long durationMs, int width, int height, string thumbPath, int thumbWidth, int thumbHeight)
		{
			StringBuilder sb = new StringBuilder();
			JsonWriter w = new JsonWriter(sb);
			w.WriteObjectStart();
			w.WritePropertyName("videoPath");
			w.Write(videoPath);
			w.WritePropertyName("bitrate");
			w.Write(bitrate);
			w.WritePropertyName("duration");
			w.Write((int)Math.Max(0L, Math.Min(durationMs, (long)int.MaxValue)));
			w.WritePropertyName("videoWidth");
			w.Write(width);
			w.WritePropertyName("videoHeight");
			w.Write(height);
			w.WritePropertyName("thumbPath");
			w.Write(thumbPath);
			w.WritePropertyName("thumbWidth");
			w.Write(thumbWidth);
			w.WritePropertyName("thumbHeight");
			w.Write(thumbHeight);
			w.WriteObjectEnd();
			return sb.ToString();
		}

		// Returns the localization token for an error, or null when the user just
		// cancelled and nothing should be shown.
		public static string ErrorToken(CameraError? error)
		{
			if (!error.HasValue)
			{
				return null;
			}
			switch (error.Value)
			{
			case CameraError.VIDEO_TOO_LONG:
				return "chatscreen.medialimitpanel.toolongtext";
			case CameraError.FILE_TOO_LARGE:
				return "customtokens.media.file_to_large";
			case CameraError.PERMISSION_DENIED:
				return "customtokens.media.camera";
			default:
				return "customtokens.global.generic_error";
			}
		}

		// Re-encoding through EncodeToJPG drops all EXIF metadata (including GPS).
		public static string WriteJpeg(Texture2D tex, string dir)
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".jpg");
			File.WriteAllBytes(path, tex.EncodeToJPG(JpegQuality));
			return path;
		}

		public static string CopyToCache(string sourcePath, string dir, string extension)
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + extension);
			File.Copy(sourcePath, path);
			return path;
		}

		public static void PruneCache(string dir, TimeSpan maxAge)
		{
			try
			{
				if (!Directory.Exists(dir))
				{
					return;
				}
				foreach (string file in Directory.GetFiles(dir))
				{
					if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > maxAge)
					{
						File.Delete(file);
					}
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		public static void ClearCache(string dir)
		{
			try
			{
				if (Directory.Exists(dir))
				{
					Directory.Delete(dir, true);
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}

	// The camera/picker plugins' IsBusy() checks are hard-wired to false on Android, so the
	// bridge keeps its own re-entry guard. A timeout recovers from a callback that never comes.
	public class MediaGate
	{
		public const float TimeoutSeconds = 10f;

		private bool inFlight;

		private float enteredAt;

		public bool TryEnter(float now)
		{
			if (inFlight && now - enteredAt < TimeoutSeconds)
			{
				return false;
			}
			inFlight = true;
			enteredAt = now;
			return true;
		}

		public void Exit()
		{
			inFlight = false;
		}
	}
}
