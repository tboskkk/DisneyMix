using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds data/Meme_Generator_Data/Meme_Generator_Data.gz, the list of meme
/// images the Meme Generator game shows. Disney hosted this file on its CDN
/// (the client only reads it: MemeGeneratorController.LoadData), and the
/// original generator is lost, so this script rebuilds it from the bundles
/// that RebuildAssetBundles produced.
///
/// A meme is every "<name>_hd.unity3d" that has a "<name>_thumb.unity3d"
/// next to it in StreamingAssets/AssetBundles/<platform>/hd/gogs/meme_generator/game_assets/en_US/.
/// To add a custom meme: put the image under the meme game_assets bundle
/// source, run Tools > Mix > Rebuild Asset Bundles, then run this script.
///
/// File format (MemeGeneratorController.ParseJsonData -> ContentObj):
///   gzip( {"content":{"objects":{"meme":[{"uid","url","thumb","order",...}]}}} )
/// url/thumb are appended to the catalog's base_url for the game
/// (gogs/meme_generator/game_assets/en_US/), so they are bare file names.
/// </summary>
public static class MemeGeneratorDataBuilder
{
	private const string AssetsRelative = "hd/gogs/meme_generator/game_assets/en_US";
	private static readonly string[] Platforms = { "windows64", "standalonelinux64", "android", "iphone", "standalonemacos" };

	private static string BundlesRoot { get { return Path.Combine(Application.streamingAssetsPath, "AssetBundles"); } }
	private static string OutputPath { get { return Path.Combine(Application.streamingAssetsPath, "data/Meme_Generator_Data/Meme_Generator_Data.gz"); } }

	[MenuItem("Tools/Mix/Build Meme Generator Data")]
	public static void Build()
	{
		string message;
		bool ok = TryBuild(out message);
		if (ok)
		{
			Debug.Log(message);
		}
		else
		{
			Debug.LogError(message);
		}
		EditorUtility.DisplayDialog("Meme Generator Data", message, "OK");
	}

	/// <summary>For -batchmode: -executeMethod MemeGeneratorDataBuilder.BuildBatch (exit 1 on failure).</summary>
	public static void BuildBatch()
	{
		string message;
		bool ok = TryBuild(out message);
		Console.WriteLine(message);
		EditorApplication.Exit(ok ? 0 : 1);
	}

	public static bool TryBuild(out string message)
	{
		string dir = null;
		string platform = null;
		foreach (string p in Platforms)
		{
			string candidate = Path.Combine(BundlesRoot, p + "/" + AssetsRelative);
			if (Directory.Exists(candidate))
			{
				dir = candidate;
				platform = p;
				break;
			}
		}
		if (dir == null)
		{
			message = "No meme bundles found under " + BundlesRoot + "/<platform>/" + AssetsRelative + ". Build the asset bundles first.";
			return false;
		}

		List<string> names = new List<string>();
		List<string> skipped = new List<string>();
		foreach (string file in Directory.GetFiles(dir, "*_hd.unity3d").OrderBy(f => f, StringComparer.Ordinal))
		{
			string name = Path.GetFileName(file);
			string stem = name.Substring(0, name.Length - "_hd.unity3d".Length);
			if (File.Exists(Path.Combine(dir, stem + "_thumb.unity3d")))
			{
				names.Add(stem);
			}
			else
			{
				skipped.Add(stem);
			}
		}
		if (names.Count == 0)
		{
			message = "No <name>_hd.unity3d + <name>_thumb.unity3d pairs in " + dir;
			return false;
		}

		StringBuilder json = new StringBuilder();
		json.Append("{\"content\":{\"objects\":{\"meme\":[");
		for (int i = 0; i < names.Count; i++)
		{
			if (i > 0)
			{
				json.Append(',');
			}
			json.Append("{\"uid\":\"").Append(Esc(names[i]))
				.Append("\",\"url\":\"").Append(Esc(names[i] + "_hd.unity3d"))
				.Append("\",\"thumb\":\"").Append(Esc(names[i] + "_thumb.unity3d"))
				.Append("\",\"order\":").Append(i + 1)
				.Append(",\"hidden\":false,\"preview\":false,\"isNew\":false,\"tags\":\"\",\"pushNote\":\"\"}");
		}
		json.Append("]}}}");

		Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
		using (FileStream fs = File.Create(OutputPath))
		using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
		{
			byte[] bytes = new UTF8Encoding(false).GetBytes(json.ToString());
			gz.Write(bytes, 0, bytes.Length);
		}
		AssetDatabase.Refresh();

		message = "Wrote " + OutputPath + ": " + names.Count + " memes (from " + platform + ")"
			+ (skipped.Count > 0 ? ". Skipped without a _thumb: " + string.Join(", ", skipped.ToArray()) : ".");
		return true;
	}

	private static string Esc(string s)
	{
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
