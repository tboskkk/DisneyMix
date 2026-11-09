using System;
using System.IO;
using Disney.Native;
using LitJson;
using NUnit.Framework;
using UnityEngine;

public class MediaPrepTests
{
	[Test]
	public void OrientedSize_Cases()
	{
		int w, h;
		MediaPrep.OrientedSize(1920, 1080, 0f, out w, out h);
		Assert.AreEqual((1920, 1080), (w, h));
		MediaPrep.OrientedSize(1920, 1080, 90f, out w, out h);
		Assert.AreEqual((1080, 1920), (w, h));
		MediaPrep.OrientedSize(1920, 1080, 270.0f, out w, out h);
		Assert.AreEqual((1080, 1920), (w, h));
		MediaPrep.OrientedSize(1920, 1080, -90f, out w, out h);
		Assert.AreEqual((1080, 1920), (w, h));
		MediaPrep.OrientedSize(1920, 1080, 180f, out w, out h);
		Assert.AreEqual((1920, 1080), (w, h));
	}

	[Test]
	public void CheckVideo_Boundaries()
	{
		Assert.AreEqual(VideoCheck.Ok, MediaPrep.CheckVideo(MediaPrep.MaxVideoBytes, 30000));
		Assert.AreEqual(VideoCheck.TooLong, MediaPrep.CheckVideo(1, 30001));
		Assert.AreEqual(VideoCheck.TooLarge, MediaPrep.CheckVideo(MediaPrep.MaxVideoBytes + 1, 1000));
	}

	[Test]
	public void Bitrate_Normal_And_ZeroDuration()
	{
		Assert.AreEqual(800000, MediaPrep.Bitrate(1000000, 10000));
		Assert.AreEqual(0, MediaPrep.Bitrate(1000, 0));
	}

	[Test]
	public void PhotoJson_RoundTripsAsChatControllerReadsIt()
	{
		JsonData j = JsonMapper.ToObject(MediaPrep.PhotoJson("/c/a.jpg", 1280, 720));
		Assert.AreEqual("/c/a.jpg", (string)j["photoPath"]);
		Assert.AreEqual(1280, (int)j["photoWidth"]);
		Assert.AreEqual(720, (int)j["photoHeight"]);
	}

	[Test]
	public void VideoJson_RoundTripsAsChatControllerReadsIt()
	{
		JsonData j = JsonMapper.ToObject(MediaPrep.VideoJson("/c/v.mp4", 800000, 12345, 1080, 1920, "/c/t.jpg", 259, 460));
		Assert.AreEqual("/c/v.mp4", (string)j["videoPath"]);
		Assert.AreEqual(800000, (int)j["bitrate"]);
		Assert.AreEqual(12345, (int)j["duration"]);
		Assert.AreEqual(1080, (int)j["videoWidth"]);
		Assert.AreEqual(1920, (int)j["videoHeight"]);
		Assert.AreEqual("/c/t.jpg", (string)j["thumbPath"]);
		Assert.AreEqual(259, (int)j["thumbWidth"]);
		Assert.AreEqual(460, (int)j["thumbHeight"]);
	}

	[Test]
	public void VideoJson_HugeBitrate_ClampsToIntMax()
	{
		// 20 MB reported as lasting 2 ms (bad metadata) -> ~83.9 Gbit/s, beyond int.
		int b = MediaPrep.Bitrate(20L * 1024 * 1024, 2);
		Assert.AreEqual(int.MaxValue, b);
		JsonData j = JsonMapper.ToObject(MediaPrep.VideoJson("/v", b, long.MaxValue, 1, 1, "/t", 1, 1));
		Assert.AreEqual(int.MaxValue, (int)j["bitrate"]);
		Assert.AreEqual(int.MaxValue, (int)j["duration"]);
	}

	[Test]
	public void ErrorToken_Cancel_IsNull()
	{
		Assert.IsNull(MediaPrep.ErrorToken(null));
	}

	[Test]
	public void ErrorToken_Mapping()
	{
		Assert.AreEqual("chatscreen.medialimitpanel.toolongtext", MediaPrep.ErrorToken(CameraError.VIDEO_TOO_LONG));
		Assert.AreEqual("customtokens.media.file_to_large", MediaPrep.ErrorToken(CameraError.FILE_TOO_LARGE));
		Assert.AreEqual("customtokens.media.camera", MediaPrep.ErrorToken(CameraError.PERMISSION_DENIED));
		Assert.AreEqual("customtokens.global.generic_error", MediaPrep.ErrorToken(CameraError.DECODE_FAILED));
		Assert.AreEqual("customtokens.global.generic_error", MediaPrep.ErrorToken(CameraError.UNKNOWN));
	}

	[Test]
	public void WriteJpeg_WritesJpegWithoutExif_AndClearCacheRemovesIt()
	{
		string dir = Path.Combine(Path.GetTempPath(), "mixmedia-test-" + Guid.NewGuid());
		Texture2D tex = new Texture2D(64, 32, TextureFormat.RGB24, false);
		string path = MediaPrep.WriteJpeg(tex, dir);
		byte[] bytes = File.ReadAllBytes(path);
		Assert.AreEqual(0xFF, bytes[0]);
		Assert.AreEqual(0xD8, bytes[1]);
		StringAssert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(bytes));
		MediaPrep.ClearCache(dir);
		Assert.IsFalse(Directory.Exists(dir));
		MediaPrep.ClearCache(dir);
		UnityEngine.Object.DestroyImmediate(tex);
	}

	[Test]
	public void CheckVideo_Camera_ToleratesRecorderOvershoot()
	{
		Assert.AreEqual(VideoCheck.Ok, MediaPrep.CheckVideo(MediaPrep.MaxVideoBytes + MediaPrep.CameraSizeSlackBytes, 30000 + MediaPrep.CameraDurationSlackMs, true));
		Assert.AreEqual(VideoCheck.TooLong, MediaPrep.CheckVideo(1, 30000 + MediaPrep.CameraDurationSlackMs + 1, true));
		Assert.AreEqual(VideoCheck.TooLarge, MediaPrep.CheckVideo(MediaPrep.MaxVideoBytes + MediaPrep.CameraSizeSlackBytes + 1, 1000, true));
		Assert.AreEqual(VideoCheck.TooLong, MediaPrep.CheckVideo(1, 30001, false));
		Assert.AreEqual(VideoCheck.TooLarge, MediaPrep.CheckVideo(MediaPrep.MaxVideoBytes + 1, 1000));
	}

	[Test]
	public void MediaGate_BlocksReentry_UntilExitOrTimeout()
	{
		MediaGate gate = new MediaGate();
		Assert.IsTrue(gate.TryEnter(0f));
		Assert.IsFalse(gate.TryEnter(1f));
		gate.Exit();
		Assert.IsTrue(gate.TryEnter(1f));
		Assert.IsFalse(gate.TryEnter(2f));
		Assert.IsTrue(gate.TryEnter(1f + MediaGate.TimeoutSeconds + 0.1f));
	}

	[Test]
	public void CopyToCache_CopiesBytesToANewUniqueFile()
	{
		string src = Path.Combine(Path.GetTempPath(), "mixmedia-src-" + Guid.NewGuid() + ".mp4");
		string dir = Path.Combine(Path.GetTempPath(), "mixmedia-copy-" + Guid.NewGuid());
		try
		{
			File.WriteAllBytes(src, new byte[] { 1, 2, 3, 4 });
			string a = MediaPrep.CopyToCache(src, dir, ".mp4");
			string b = MediaPrep.CopyToCache(src, dir, ".mp4");
			Assert.AreNotEqual(src, a);
			Assert.AreNotEqual(a, b);
			Assert.IsTrue(a.EndsWith(".mp4"));
			CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(a));
		}
		finally
		{
			File.Delete(src);
			MediaPrep.ClearCache(dir);
		}
	}

	[Test]
	public void PruneCache_RemovesOldFiles_KeepsRecentOnes()
	{
		string dir = Path.Combine(Path.GetTempPath(), "mixmedia-prune-" + Guid.NewGuid());
		try
		{
			Directory.CreateDirectory(dir);
			string oldFile = Path.Combine(dir, "old.jpg");
			string newFile = Path.Combine(dir, "new.jpg");
			File.WriteAllBytes(oldFile, new byte[] { 1 });
			File.WriteAllBytes(newFile, new byte[] { 1 });
			File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow - TimeSpan.FromDays(8));
			MediaPrep.PruneCache(dir, TimeSpan.FromDays(7));
			Assert.IsFalse(File.Exists(oldFile));
			Assert.IsTrue(File.Exists(newFile));
			MediaPrep.PruneCache(Path.Combine(dir, "missing"), TimeSpan.FromDays(7));
		}
		finally
		{
			MediaPrep.ClearCache(dir);
		}
	}
}
