using System.Runtime.InteropServices;

namespace AWS.Services.HCNetSDK;

public static class HCNetSDKApi
{
    private const string Dll = "HCNetSDK";

    [DllImport(Dll)] public static extern bool NET_DVR_Init();
    [DllImport(Dll)] public static extern bool NET_DVR_Cleanup();

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern bool NET_DVR_SetLogToFile(int nLogLevel, string strLogDir, bool bAutoDel);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int NET_DVR_Login_V30(
        string sDVRIP, ushort wDVRPort, string sUserName, string sPassword,
        ref NET_DVR_DEVICEINFO_V30 lpDeviceInfo);

    [DllImport(Dll)] public static extern bool NET_DVR_Logout(int lUserID);

    [DllImport(Dll)]
    public static extern int NET_DVR_RealPlay_V40(
        int lUserID, ref NET_DVR_PREVIEWINFO lpPreviewInfo,
        IntPtr fRealDataCallBack_V30, IntPtr pUser);

    [DllImport(Dll)] public static extern bool NET_DVR_StopRealPlay(int lRealHandle);

    [DllImport(Dll)]
    public static extern bool NET_DVR_CaptureJPEGPicture_NEW(
        int lUserID, int lChannel, ref NET_DVR_JPEGPARA lpJpegPara,
        byte[] pJpegBuffer, uint dwPicSize, ref uint pdwPicLen);

    [DllImport(Dll)] public static extern uint NET_DVR_GetLastError();
}

[StructLayout(LayoutKind.Sequential)]
public struct NET_DVR_DEVICEINFO_V30
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] sSerialNumber;
    public byte byAlarmInPortNum;
    public byte byAlarmOutPortNum;
    public byte byDiskNum;
    public byte byDVRType;
    public byte byChanNum;         // 模拟通道数
    public byte byStartChan;       // 模拟通道起始号（通常为1）
    public byte byAudioChanNum;
    public byte byIPChanNum;       // IP通道数低8位
    public byte byZeroChanNum;
    public byte byMainProto;
    public byte bySubProto;
    public byte bySupport;
    public byte bySupport1;
    public byte bySupport2;
    public ushort wDevType;
    public byte bySupport3;
    public byte byMultiStreamProto;
    public byte byStartDChan;      // IP通道起始号（NVR通常为33）
    public byte byStartDTalkChan;
    public byte byHighDChanNum;    // IP通道数高8位
    public byte bySupport4;
    public byte byLanguageType;
    public byte byVoiceInChanNum;
    public byte byStartVoiceInChanNo;
    public byte bySupport5;
    public byte bySupport6;
    public byte byMirrorChanNum;
    public ushort wStartMirrorChanNo;
    public byte byReserve2;
    public byte byReserve;
}

[StructLayout(LayoutKind.Sequential)]
public struct NET_DVR_PREVIEWINFO
{
    public int    lChannel;
    public uint   dwStreamType;     // 0=主码流
    public uint   dwLinkMode;       // 0=TCP
    public IntPtr hPlayWnd;         // 渲染目标 HWND（IntPtr.Zero = 回调模式）
    [MarshalAs(UnmanagedType.Bool)]
    public bool   bBlocked;
    [MarshalAs(UnmanagedType.Bool)]
    public bool   bPassbackRecord;
    public byte   byPreviewMode;
    public byte   byProtoType;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public byte[] byRes;
}

[StructLayout(LayoutKind.Sequential)]
public struct NET_DVR_JPEGPARA
{
    public ushort wPicSize;    // 0xFF = 原始分辨率
    public ushort wPicQuality; // 0=最好, 1=一般, 2=较差
}
