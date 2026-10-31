using System;
using System.Runtime.InteropServices;

namespace RepoBots;

internal static class BotMicCheck
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct WaveInCaps
	{
		public ushort wMid;

		public ushort wPid;

		public uint vDriverVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szPname;

		public uint dwFormats;

		public ushort wChannels;

		public ushort wReserved1;
	}

	private const uint DrvmMapperPreferredGet = 8213u;

	private static bool _done;

	private static string _result;

	public static bool Done => _done;

	public static string Result => _result;

	[DllImport("winmm.dll")]
	private static extern int waveInMessage(IntPtr hwi, uint msg, out int dw1, out int dw2);

	[DllImport("winmm.dll", CharSet = CharSet.Unicode)]
	private static extern int waveInGetDevCaps(UIntPtr deviceId, ref WaveInCaps caps, int size);

	public static void Start()
	{
		if (_done)
		{
			return;
		}
		_done = true;
		try
		{
			if (waveInMessage(new IntPtr(-1), 8213u, out var dw, out var _) == 0)
			{
				WaveInCaps caps = default;
				if (waveInGetDevCaps(new UIntPtr((uint)dw), ref caps, Marshal.SizeOf(typeof(WaveInCaps))) == 0 && !string.IsNullOrEmpty(caps.szPname))
				{
					_result = caps.szPname.Trim();
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogInfo((object)("Voice commands: couldn't read the Windows default mic (" + ex.GetType().Name + ")"));
		}
	}

	public static bool SameMic(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
		{
			return false;
		}
		a = a.Trim();
		b = b.Trim();
		if (!a.StartsWith(b, StringComparison.OrdinalIgnoreCase))
		{
			return b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static bool LooksVirtual(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		string text = name.ToLowerInvariant();
		if (!text.Contains("voicemod") && !text.Contains("steam streaming") && !text.Contains("virtual") && !text.Contains("vb-audio") && !text.Contains("cable output"))
		{
			return text.Contains("oculus");
		}
		return true;
	}
}
