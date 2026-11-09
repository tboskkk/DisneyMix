using Mix;

namespace Disney.Native
{
	public class NativeCameraManager : MonoSingleton<NativeCameraManager>
	{
		private NativeCamera native;

		public NativeCamera Native
		{
			get
			{
				return native;
			}
			private set
			{
				native = value;
			}
		}

		protected void Awake()
		{
#if UNITY_ANDROID || UNITY_EDITOR
			native = base.gameObject.AddComponent<AndroidCameraBridge>();
#else
			native = base.gameObject.AddComponent<NativeCamera>();
#endif
			MediaPrep.PruneCache(MediaPrep.CacheDir, MediaPrep.CacheMaxAge);
		}

		public void Init()
		{
			base.gameObject.name = "NativeCameraManager";
		}

		public void OnApplicationPause(bool aState)
		{
			if (aState)
			{
				Native.OnApplicationPaused();
			}
			else
			{
				Native.OnApplicationResumed();
			}
		}
	}
}
