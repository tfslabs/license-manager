using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.Vlmcs
{
    public delegate int KmsServerCallback(IntPtr responsePtr, IntPtr requestPtr, IntPtr hwId, IntPtr clientIpAddressPtr);

    public class KmsException : Exception
    {
        public KmsException(string message) : base(message) { }
        public KmsException(string message, Exception inner) : base(message, inner) { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct CharBuffer64
    {
        public fixed char UnsafeText[64];

        public string Text
        {
            get
            {
                fixed (char* c = UnsafeText)
                {
                    c[63] = (char)0;
                    return Marshal.PtrToStringUni((IntPtr)c);
                }
            }
            set
            {
                if (value.Length > 63) throw new ArgumentException("Maximum is 63 chars.", nameof(UnsafeText));

                fixed (char* c = UnsafeText)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        c[i] = value[i];
                    }

                    c[value.Length] = (char)0;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public struct ProtocolVersion : IComparable<ProtocolVersion>, IEquatable<ProtocolVersion>
    {
        public uint Full;

        public ushort Major
        {
            get { return (ushort)(Full >> 16); }
            set { Full = ((uint)value) << 16 | Full & 0xffff; }
        }

        public ushort Minor
        {
            get { return (ushort)(Full & 0xffff); }
            set { Full = value | (Full & 0xffff0000); }
        }

        public override string ToString() => $"{Major}.{Minor}";

        public static explicit operator ProtocolVersion(string versionString)
        {
            ProtocolVersion result = new ProtocolVersion();
            string[] split = versionString.Split('.');
            if (split.Length != 2) throw new FormatException("KMS protocol must contain exactly one period.");
            result.Major = ushort.Parse(split[0], CultureInfo.InvariantCulture);
            result.Minor = ushort.Parse(split[1], CultureInfo.InvariantCulture);
            return result;
        }

        public override bool Equals(object obj) => obj is ProtocolVersion && (ProtocolVersion)obj == this;
        public static bool operator ==(ProtocolVersion a, ProtocolVersion b) => a.Full == b.Full;
        public static bool operator !=(ProtocolVersion a, ProtocolVersion b) => a.Full != b.Full;
        public static bool operator >(ProtocolVersion a, ProtocolVersion b) => a.Full > b.Full;
        public static bool operator <(ProtocolVersion a, ProtocolVersion b) => a.Full < b.Full;
        public static bool operator >=(ProtocolVersion a, ProtocolVersion b) => a.Full >= b.Full;
        public static bool operator <=(ProtocolVersion a, ProtocolVersion b) => a.Full <= b.Full;
        public static explicit operator uint(ProtocolVersion a) => a.Full;
        public static explicit operator ProtocolVersion(uint a) => new ProtocolVersion { Full = a };
        public override int GetHashCode() => unchecked((int)Full);
        public int CompareTo(ProtocolVersion other) => this == other ? 0 : this > other ? 1 : -1;
        public bool Equals(ProtocolVersion other) => this == other;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct KmsRequest
    {
        public ProtocolVersion Version;
        public uint IsClientVM;                       // probably BOOL in M$ definition
        public LicenseStatus LicenseStatus;           // original M$ name
        public uint GracePeriodRemaining;             // original M$ name misleading. Also used for remaining licensed time in KMS. Switches to OOT from Licensed after 180 days
        public KmsGuid ApplicationID;                 // original M$ name (is Windows, Office2010 or Office 2013)
        public KmsGuid ID;                            // original M$ name for SKU ID
        public KmsGuid KmsID;                         // M$ name unknown (Vista, Win7, Win8, Win2008, Win2008R2, Win2012, Office2010 or Office2013)
        public KmsGuid ClientMachineID;
        public uint RequiredClientCount;              // original M$ name
        public long TimeStamp;                        // original M$ name
        public KmsGuid PreviousClientMachineID;
        public CharBuffer64 WorkstationName;          // 63 chars (maximum allowed in a DNS name according to RFC)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct KmsResponse
    {
        public ProtocolVersion Version;
        public uint KmsPIDLen;
        public CharBuffer64 KmsPid;
        public KmsGuid ClientMachineID;               // original M$ name
        public long TimeStamp;                        // original M$ name
        public uint KMSCurrentCount;                  // M$ name KeyManagementServiceCurrentCount but too long for me
        public uint VLActivationInterval;             // original M$ name
        public uint VLRenewalInterval;                // original M$ name
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RpcDiag
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool HasRpcDiag;
        [MarshalAs(UnmanagedType.I1)]
        public bool HasBTFN;
        [MarshalAs(UnmanagedType.I1)]
        public bool HasNDR64;
    }

    public enum LicenseStatus
    {
        Unknown = -1,
        Unlicensed = 0,
        Licensed = 1,
        GraceOob = 2,
        GraceOot = 3,
        GraceNonGenuine = 4,
        Notification = 5,
        GraceExtended = 6,
    };

    [Flags]
    public enum ResultCode
    {
        IsValidHash = 1,                        // Hash of response is correct
        IsValidTimeStamp = 2,                   // Time stamp of response matches request
        IsValidClientMachineId = 4,             // Client ID of response matched request
        IsValidProtocolVersion = 8,             // response version matches request version
        IsValidInitializationVector = 16,       // IVs (Salts) of response and request are correct (always set in V4, must be identical in V5, must be Xored in V6)
        DecryptSuccess = 32,                    // CryptoApi reported successful Decryption (always set in V4)
        IsValidHmac = 64,                       // HMAC_SHA256 is correct (always set in V4 and V5)
        IsValidPidLength = 128,                 // Incorrect PID length
        IsRpcStatusSuccess = 256,               // RPC Error
        IsRandomInitializationVector = 512,     // IV uses v5 semantics in v6 response
    }

    public unsafe struct HwId
    {
        public fixed byte Data[8];
        public byte[] ByteArray
        {
            get
            {
                byte[] result = new byte[8];

                fixed (byte* b = Data)
                {
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = *(b + i);
                    }
                }

                return result;
            }
            set
            {
                if (value.Length != 8) throw new ArgumentException("Must be exactly 8 bytes.", nameof(ByteArray));

                fixed (byte* b = Data)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        *(b + i) = value[i];
                    }
                }
            }
        }

        public string Text
        {
            get
            {
                string result = "";

                foreach (byte b in ByteArray)
                {
                    result += $"{b:X02} ";
                }

                result = result.TrimEnd();
                return result;
            }
            set
            {
                string cleanhex = value.ToUpperInvariant().Where(c => c >= '0' && c <= 'F').Where(c => c <= '9' || c >= 'A').Aggregate("", (current, c) => current + c);

                if (cleanhex.Length != 16) throw new ArgumentException("Hardware ID must be exactly 8 hex bytes.", nameof(HwId));

                byte[] hwId = new byte[8];

                for (int i = 0; i < hwId.Length; i++)
                {
                    hwId[i] = byte.Parse(cleanhex.Substring(i << 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }

                ByteArray = hwId;
            }
        }
    }

    public struct KmsResult
    {
        private readonly uint result;
        public readonly ProtocolVersion Version;

        internal KmsResult(uint result, ProtocolVersion version)
        {
            this.result = result;
            Version = version;
        }

        public uint CorrectResponseSize => result >> 23;
        public uint EffectiveResponseSize => (result >> 14) & 0x1ff;

        public bool? IsValidResponseSize => CorrectResponseSize == EffectiveResponseSize;
        public bool? IsDecryptSuccess => Version.Major < 5 ? null : (bool?)((result & (int)ResultCode.DecryptSuccess) != 0);
        public bool? IsValidHash => (result & (int)ResultCode.IsValidHash) != 0;
        public bool? IsValidTimeStamp => (result & (int)ResultCode.IsValidTimeStamp) != 0;
        public bool? IsValidClientMachineId => (result & (int)ResultCode.IsValidClientMachineId) != 0;
        public bool? IsValidProtocolVersion => (result & (int)ResultCode.IsValidProtocolVersion) != 0;
        public bool? IsValidInitializationVector => Version.Major < 5 ? null : (bool?)((result & (int)ResultCode.IsValidInitializationVector) != 0);
        public bool? IsValidHmac => Version.Major < 6 ? null : (bool?)((result & (int)ResultCode.IsValidHmac) != 0);
        public bool? IsValidPidLength => (result & (int)ResultCode.IsValidPidLength) != 0;
        public bool? IsRpcStatusSuccess => (result & (int)ResultCode.IsRpcStatusSuccess) != 0;
        public bool? IsSuspiciousInitializationVector => Version.Major < 6 ? null : (bool?)((result & (int)ResultCode.IsRandomInitializationVector) == 0);
    }

    public class KmsClient : IDisposable
    {
        public string HostnameUnicode { get; private set; }
        public readonly ushort Port;
        public bool Connected => ctx != invalidCtx && IsDisconnected(ctx) == 0;

        private static readonly IntPtr invalidCtx = IntPtr.Subtract(IntPtr.Zero, 1);
        private IntPtr ctx = invalidCtx;

        /// <summary>
        /// Creates a new KMS client from hostname and port
        /// </summary>
        /// <param name="hostname">Name or IP address of the remote host. IDN domains like "bücher.com", "мойдомен.рф" or "παράδειγμα.δοκιμή" are allowed</param>
        /// <param name="port">TCP port number</param>
        public KmsClient(string hostname, ushort port)
        {
            SetHostname(hostname);
            Port = port;
        }

        /// <summary>
        /// Creates a new KMS client from a KMS address in the form &lt;hostname&gt;|&lt;IP address&gt;[:&lt;port&gt;].
        /// If the hostname contains colons, it must be put in brackets.
        /// </summary>
        /// <param name="kmsAddress">KMS address in the form &lt;hostname&gt;|&lt;IP address&gt;[:&lt;port&gt;].
        /// IDN domains are allowed. Examples 192.168.1.1, 10.0.0.1:1688, [::1]:1688, мойдомен.рф and bücher.com:1234</param>
        public KmsClient(string kmsAddress)
        {
            SplitKmsAddress(kmsAddress, out string hostname, out Port);
            SetHostname(hostname);
        }

        public string ConnectTcp(AddressFamily addressFamily)
        {
            if (addressFamily != AddressFamily.InterNetwork && addressFamily != AddressFamily.InterNetworkV6 && addressFamily != AddressFamily.Unspecified)
            {
                throw new ArgumentException("Must be InterNetwork (IPv4), InterNetworkV6 (IPv6) or Unspecified (IPv4 and IPv6)", nameof(addressFamily));
            }

            ctx = ConnectToServer(HostnamePunycode, Port.ToString(CultureInfo.InvariantCulture), (int)addressFamily);

            if (ctx == invalidCtx)
            {
                throw new KmsException(LibKmsMessage);
            }

            return LibKmsMessage;
        }

        public RpcDiag ConnectRpc(bool useMultiplexedRpc, bool useNdr64, bool useBtfn)
        {
            RpcDiag rpcDiag = default(RpcDiag);
            int rpcStatus = BindRpc(ctx, useMultiplexedRpc, useNdr64, useBtfn, ref rpcDiag);

            if (rpcStatus == 0) return rpcDiag;
            Win32Exception exception = new Win32Exception(rpcStatus);
            throw new KmsException(LibKmsMessage, exception);
        }

        public string Connect(AddressFamily addressFamily, out RpcDiag rpcDiag, bool useMultiplexedRpc = true, bool useNdr64 = true, bool useBtfn = true)
        {
            string warnings = ConnectTcp(addressFamily);
            rpcDiag = ConnectRpc(useMultiplexedRpc, useNdr64, useBtfn);
            warnings += LibKmsMessage;
            return warnings;
        }

        public string HostnamePunycode
        {
            get
            {
                try
                {
                    return Kms.Idn.GetAscii(HostnameUnicode);
                }
                catch
                {
                    return HostnameUnicode;
                }
            }
        }

        public KmsResult SendRequest(out string errors, out string warnings, out KmsResponse baseResponse, KmsRequest baseRequest, out byte[] hwId, bool throwOnInsufficientClients = false, bool throwOnBadResult = true)
        {
            if (!Connected)
            {
                Win32Exception innerException = ctx == invalidCtx ? null : new Win32Exception(10057);
                Close();
                throw new KmsException("The TCP connection is closed" + (innerException != null ? $"\n{innerException.Message}" : ""), innerException);
            }

            //if (baseRequest.Version.Full < 0x50000 && baseRequest.Version.Full > (uint)0x6ffff) throw new ArgumentOutOfRangeException(
            //  "KmsHgm.KmsRequest", string.Format("{0}.{1}", baseRequest.Version.Major, baseRequest.Version.Minor), "Protocol version must be 4.0, 5.0 or 6.0."
            //);

            IntPtr baseRequestPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(KmsRequest)));
            try
            {
                IntPtr baseResponsePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(KmsResponse)));
                try
                {
                    IntPtr hwIdPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HwId)));
                    try
                    {
                        StringBuilder errorMessage = new StringBuilder(256, 16384);
                        Marshal.StructureToPtr(baseRequest, baseRequestPtr, false);
                        uint status = SendKmsRequest(ctx, baseResponsePtr, baseRequestPtr, out uint result, hwIdPtr);
                        warnings = LibKmsMessage;

                        if (status != 0)
                        {
                            if (!string.IsNullOrWhiteSpace(LibKmsMessage)) errorMessage.AppendLine(LibKmsMessage);
                            if (status == 87 || status == 0x8007000D) errorMessage.AppendLine("The server did not understand the KMS request.");
                            if (status == ~0U) errorMessage.AppendLine("The KMS server has declined the activation request.");
                            if (status == 0x6b5) errorMessage.AppendLine("The RPC server does not support KMS.");
                            if (status == 1820) Close();

                            if (status != ~0U)
                            {
                                errorMessage.AppendLine(Kms.StatusMessage(status));
                            }

                            throw new KmsException(errorMessage.ToString(), status != ~0U ? new Win32Exception(unchecked((int)status)) : null);
                        }

                        baseResponse = (KmsResponse)Marshal.PtrToStructure(baseResponsePtr, typeof(KmsResponse));
                        hwId = ((HwId)Marshal.PtrToStructure(hwIdPtr, typeof(HwId))).ByteArray;
                        KmsResult kmsResult = new KmsResult(result, baseResponse.Version);

                        if ((result & (int)ResultCode.DecryptSuccess) == 0) errorMessage.AppendLine("AES Decryption of KMS response failed.");
                        if ((result & (int)ResultCode.IsValidPidLength) == 0) errorMessage.AppendLine("The length field of the KMS PID is not valid.");
                        if ((result & (int)ResultCode.IsValidInitializationVector) == 0) errorMessage.AppendLine("IVs (salts) of KMS request and response do not match.");
                        if ((result & (int)ResultCode.IsValidProtocolVersion) == 0) errorMessage.AppendLine("KMS response version does not match request.");
                        if ((result & (int)ResultCode.IsValidClientMachineId) == 0) errorMessage.AppendLine("Client Machine ID of request and response do not match.");
                        if ((result & (int)ResultCode.IsValidTimeStamp) == 0) errorMessage.AppendLine("Time stamp of KMS request and response do not match.");
                        if ((result & (int)ResultCode.IsValidHash) == 0) errorMessage.AppendLine("Hash of KMS response is not valid.");
                        if ((result & (int)ResultCode.IsValidHmac) == 0) errorMessage.AppendLine("HMAC is not correct.");
                        if ((result & (int)ResultCode.IsRpcStatusSuccess) == 0) errorMessage.AppendLine("RPC returned non-zero result code.");
                        if ((result & (int)ResultCode.IsRandomInitializationVector) == 0) warnings += "Non-random initialization vector (salt) used in KMSv6 protocol.\n";

                        uint correctResponseSize = result >> 23;
                        uint effectiveResponseSize = (result >> 14) & 0x1ff;

                        if (correctResponseSize != effectiveResponseSize)
                        {
                            errorMessage.AppendFormat("KMS server reponse has an incorrect size of {0} bytes. Should be {1}.{2}", effectiveResponseSize, correctResponseSize, Environment.NewLine);
                        }

                        errors = errorMessage.ToString();
                        if (errorMessage.Length != 0 && throwOnBadResult) throw new KmsException(errorMessage.ToString());
                        if (throwOnInsufficientClients && baseResponse.KMSCurrentCount < baseRequest.RequiredClientCount) throw new KmsException("The required count is not sufficient.");

                        return kmsResult;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(hwIdPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(baseResponsePtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(baseRequestPtr);
            }
        }

        private static string LibKmsMessage
        {
            get
            {
                IntPtr resultPtr = GetErrorMessage();
                string result = Marshal.PtrToStringAnsi(resultPtr);
                return result?.TrimStart();
            }
        }

        private static void SplitKmsAddress(string address, out string host, out ushort port)
        {
            port = 1688;
            if (string.IsNullOrEmpty(address))
            {
                host = address;
                return;
            }

            if (address[0] == '[')
            {
                int closingBracketPosition = address.LastIndexOf(']');
                host = address.Substring(1, closingBracketPosition - 1);

                if (address.Length > closingBracketPosition + 2)
                {
                    string portString = address.Substring(closingBracketPosition + 2);
                    port = ushort.Parse(portString, CultureInfo.InvariantCulture);
                }
            }
            else
            {
                if (address.Count(c => c == ':') != 1)
                {
                    host = address;
                }
                else
                {
                    string[] split = address.Split(':');
                    host = split[0];
                    port = ushort.Parse(split[1], CultureInfo.InvariantCulture);
                }
            }
        }

        private void SetHostname(string hostname)
        {
            Kms.CheckDllVersion();
            HostnameUnicode = hostname ?? throw new ArgumentNullException(nameof(hostname));
        }

        [DllImport("libkms32.dll", EntryPoint = "ConnectToServer", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false)]
        private static extern IntPtr ConnectToServer32([MarshalAs(UnmanagedType.LPStr)] string host, string port, int addressFamily);

        [DllImport("libkms64.dll", EntryPoint = "ConnectToServer", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false)]
        private static extern IntPtr ConnectToServer64([MarshalAs(UnmanagedType.LPStr)] string host, string port, int addressFamily);

        private static IntPtr ConnectToServer(string host, string port, int addressFamily)
        {
            return IntPtr.Size == 8 ? ConnectToServer64(host, port, addressFamily) : ConnectToServer32(host, port, addressFamily);
        }

        [DllImport("libkms32.dll", EntryPoint = "BindRpc", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int BindRpc32
        (
          IntPtr socket,
          [MarshalAs(UnmanagedType.I1)] bool useMultiplexedRpc,
          [MarshalAs(UnmanagedType.I1)] bool useNdr64,
          [MarshalAs(UnmanagedType.I1)] bool useBtfn,
          ref RpcDiag rpcDiag
        );

        [DllImport("libkms64.dll", EntryPoint = "BindRpc", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern int BindRpc64
        (
          IntPtr socket,
          [MarshalAs(UnmanagedType.I1)] bool useMultiplexedRpc,
          [MarshalAs(UnmanagedType.I1)] bool useNdr64,
          [MarshalAs(UnmanagedType.I1)] bool useBtfn,
          ref RpcDiag rpcDiag
        );

        private static int BindRpc(IntPtr socket, bool useMultiplexedRpc, bool useNdr64, bool useBtfn, ref RpcDiag rpcDiag)
        {
            return IntPtr.Size == 8 ?
              BindRpc64(socket, useMultiplexedRpc, useNdr64, useBtfn, ref rpcDiag) :
              BindRpc32(socket, useMultiplexedRpc, useNdr64, useBtfn, ref rpcDiag);
        }

        [DllImport("libkms32.dll", EntryPoint = "GetErrorMessage", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetErrorMessage32();

        [DllImport("libkms64.dll", EntryPoint = "GetErrorMessage", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetErrorMessage64();

        private static IntPtr GetErrorMessage()
        {
            return IntPtr.Size == 8 ? GetErrorMessage64() : GetErrorMessage32();
        }

        [DllImport("libkms32.dll", EntryPoint = "CloseConnection", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern void CloseConnection32(IntPtr ctx);

        [DllImport("libkms64.dll", EntryPoint = "CloseConnection", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern void CloseConnection64(IntPtr ctx);

        private static void CloseConnection(IntPtr ctx)
        {
            if (IntPtr.Size == 8)
            {
                CloseConnection64(ctx);
            }
            else
            {
                CloseConnection32(ctx);
            }
        }

        [DllImport("libkms32.dll", EntryPoint = "SendKMSRequest", CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, SetLastError = false, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true, ExactSpelling = true)]
        private static extern uint SendKMSRequest32(IntPtr ctx, IntPtr baseResponse, IntPtr baseRequest, out uint result, IntPtr hwId);

        [DllImport("libkms64.dll", EntryPoint = "SendKMSRequest", CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, SetLastError = false, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true, ExactSpelling = true)]
        private static extern uint SendKMSRequest64(IntPtr ctx, IntPtr baseResponse, IntPtr baseRequest, out uint result, IntPtr hwId);

        private static uint SendKmsRequest(IntPtr ctx, IntPtr baseResponse, IntPtr baseRequest, out uint result, IntPtr hwId)
        {
            return IntPtr.Size == 8
              ? SendKMSRequest64(ctx, baseResponse, baseRequest, out result, hwId)
              : SendKMSRequest32(ctx, baseResponse, baseRequest, out result, hwId);
        }

        [DllImport("libkms32.dll", EntryPoint = "IsDisconnected", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern byte IsDisconnected32(IntPtr ctx);

        [DllImport("libkms64.dll", EntryPoint = "IsDisconnected", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern byte IsDisconnected64(IntPtr ctx);

        private static byte IsDisconnected(IntPtr ctx)
        {
            return IntPtr.Size == 8 ? IsDisconnected64(ctx) : IsDisconnected32(ctx);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) { /* No managed objects to dispose */ }

            if (ctx != invalidCtx)
            {
                CloseConnection(ctx);
                ctx = invalidCtx;
            }
        }

        ~KmsClient()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Close() => Dispose(false);
    }

    public static class Kms
    {
        public static readonly ProtocolVersion RequiredDllVersion = new ProtocolVersion { Major = 4, Minor = 0 };
        public static IdnMapping Idn = new IdnMapping();

        public static readonly KmsGuid O2010Guid = new KmsGuid("59a52881-a989-479d-af46-f275c6370663");
        public static readonly KmsGuid O2013Guid = new KmsGuid("0ff1ce15-a989-479d-af46-f275c6370663");
        public static readonly KmsGuid WinGuid = new KmsGuid("55c92734-d682-4d71-983e-d6ec3f16059f");
        //private static readonly IDictionary<uint, string> platformId = new Dictionary<uint, string>();
        //public static IReadOnlyDictionary<uint, string> PlatformId => (IReadOnlyDictionary<uint, string>)platformId;


        static Kms()
        {
            Idn.AllowUnassigned = true;
            Idn.UseStd3AsciiRules = false;
        }

        public static string EmulatorVersion => Marshal.PtrToStringAnsi(GetEmulatorVersion());

        public static ProtocolVersion ApiVersion => (ProtocolVersion)GetLibKmsVersion();

        public static void StopServer()
        {
            CheckDllVersion();
            int status = StopKmsServer();
            if (status != 0) throw new KmsException("KMS Service is already stopped");
        }

        public static void StartServer(int port, KmsServerCallback callback)
        {
            CheckDllVersion();
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");

            int status = StartKmsServer(port, callback);
            if (status == 0) return;
            Win32Exception winException = new Win32Exception(status);
            throw new KmsException($"Could not start a KMS server on port {port}: {winException.Message}", winException);
        }

        internal static void CheckDllVersion()
        {
            if (ApiVersion < RequiredDllVersion) throw new DllNotFoundException(
              $"libkms32.dll Version {RequiredDllVersion.Major}.{RequiredDllVersion.Minor} or greater required."
            );
        }

        public static string StatusMessage(uint hResult)  //BUGBUG: Better read %SystemDrive%\System32\slmgr\<localization dir>\slmgr.ini to get error messages (even though this file is awfully buggy).
        {
            switch (hResult)
            {
                case 0x4004F00C: return "The application is running within the valid grace period.";
                case 0x4004F00D: return "Application is within valid OOT Grace";
                case 0:
                case 0x4004F040: return "The product was activated.";
                case 0x4004F401: return "The product has a store license.";
                case 0x4004FC04: return "The application is running within the time-based validity period.";
                case 0x8007000D: return "The KMS host you are using is unable to handle your product. It only supports legacy versions.";
                case 0xC004C001: return "The specified product key is invalid.";
                case 0xC004C003: return "The specified product key is blocked.";
                case 0xC004C004: return "The activation server determined the specified product key is invalid.";
                case 0xC004C017: return "The specified product key has been blocked for this geographic location.";
                case 0xC004B100: return "The computer could not be activated.";
                case 0xC004C008: return "The specified product key could not be used.";
                case 0xC004C020: return "The Multiple Activation Key has exceeded its limit.";
                case 0xC004C021: return "The Multiple Activation Key extension limit has been exceeded.";
                case 0xC004D104: return "The security processor reported that invalid data was used.";
                case 0xC004D302: return "The trusted data store was rearmed.";
                case 0xC004D307: return "The maximum allowed number of re-arms has been exceeded. You must re-install the OS before trying to re-arm again.";
                case 0xC004E016: return "The product for that key is not installed.";
                case 0xC004F005: return "The product key is not valid for the corresponding license file.";
                case 0xC004F009: return "The grace period expired.";
                case 0xC004F00F: return "The hardware ID binding is beyond level of tolerance.";
                case 0xC004F012: return "The value for the input key was not found.";
                case 0xC004F014: return "The product key is not available.";
                case 0xC004F015:
                case 0xC004F017: return "The required license is not installed.";
                case 0xc004F01D: return "The verification of the license failed.";
                case 0xC004F01F: return "The license data is invalid.";
                case 0xC004F025: return "Access denied: the requested action requires elevated privileges.";
                case 0xC004F02C: return "The format for the offline activation data is incorrect";
                case 0xC004F035: return "The computer could not be activated with a Volume license product key. Volume licensed systems require upgrading from a qualified operating system. Please contact your system administrator or use a different type of key.";
                case 0xC004F038: return "The computer could not be activated. The count reported by your Key Management Service (KMS) is insufficient. Please contact your system administrator.";
                case 0xC004F039: return "The computer could not be activated. The Key Management Service (KMS) is not enabled.";
                case 0xC004F041: return "The Key Management Server (KMS) is not activated. KMS needs to be activated";
                case 0xC004F042: return "The KMS server has declined to activate the requested product.";
                case 0xC004F050: return "The product key is invalid.";
                case 0xC004F051: return "The product key is blocked.";
                case 0xC004F056: return "Key Management Server (KMS) Activation failed.";
                case 0xC004F064: return "The non-Genuine grace period has expired.";
                case 0xC004F065: return "The application is running within the valid non-genuine period.";
                case 0xC004F066: return "The dependent SKU is not installed. You cannot install an add-on without the base product.";
                case 0xC004F069: return "The product SKU is not found (product for that key not installed)."; //slmgr.ini says this is error 0xC004F066, but obviously it is 0xC004F069
                case 0xC004F06B: return "The software Licensing Service is running in a virtual machine. The Key Management Service (KMS) is not supported in this mode.";
                case 0xC004F06C: return "The time stamp differs too much from the KMS server time.";
                case 0xC004F074: return "The computer could not be activated. No Key Management Service (KMS) could be contacted. Please see the Application Event Log for additional information.";
                case 0xC004F075: return "The operation cannot be completed because the service is stopping.";
                case 0xC004F304: return "The required license could not be found.";
                case 0xC004F305: return "There are no certificates found in the system that could activate the product.";
                case 0xC004F30A: return "The computer could not be activated. The certificate does not match the conditions in the license.";
                case 0xC004F30D: return "The computer could not be activated. The thumbprint is invalid.";
                case 0xC004F30E: return "The computer could not be activated. A certificate for the thumbprint could not be found.";
                case 0xC004F30F: return "The computer could not be activated. The certificate does not match the criteria specified in the issuance license.";
                case 0xC004F310: return "The computer could not be activated. The certificate does not match the trust point identifier (TPID) specified in the issuance license.";
                case 0xC004F311: return "The computer could not be activated. A soft token cannot be used for activation.";
                case 0xC004F312: return "The computer could not be activated. The certificate cannot be used because its private key is exportable.";
                case 0xC004FE00: return "The License Data Store has been tampered with. Reactivation required.";
                case 0x8A010101: return "They is not recognized by the pkeyconfig file.";
                /*case 0x80070057: return "The parameter is incorrect";
                        case 0x8007232A: return "DNS server failure";
                        case 0x8007232B: return "DNS name does not exist";
                        case 0x800706BA: return "The RPC server is unavailable";
                        case 0x8007251D: return "No records found for DNS query";*/
                case 0x80072ee7: return "Microsoft activation server could not be reached.";

                default:
                    try
                    {
                        return new Win32Exception(unchecked((int)hResult)).Message;
                    }
                    catch
                    {
                        return "Unknown error. Try running \"slui 42 0x" + hResult.ToString("X") + "\" in a command prompt (CMD.EXE) to display a more descriptive error text.";
                    }
            }
        }

        [DllImport("libkms32.dll", EntryPoint = "StartKmsServer", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int StartKmsServer32(int port, KmsServerCallback callback);

        [DllImport("libkms32.dll", EntryPoint = "StopKmsServer", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int StopKmsServer32();

        [DllImport("libkms32.dll", EntryPoint = "GetLibKmsVersion", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern uint GetLibKmsVersion32();

        [DllImport("libkms32.dll", EntryPoint = "GetEmulatorVersion", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetEmulatorVersion32();
        [DllImport("libkms64.dll", EntryPoint = "StartKmsServer", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int StartKmsServer64(int port, KmsServerCallback callback);

        [DllImport("libkms64.dll", EntryPoint = "StopKmsServer", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int StopKmsServer64();

        [DllImport("libkms64.dll", EntryPoint = "GetLibKmsVersion", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern uint GetLibKmsVersion64();

        [DllImport("libkms64.dll", EntryPoint = "GetEmulatorVersion", CallingConvention = CallingConvention.Cdecl, SetLastError = false, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetEmulatorVersion64();

        private static int StartKmsServer(int port, KmsServerCallback callback)
        {
            return IntPtr.Size == 8 ? StartKmsServer64(port, callback) : StartKmsServer32(port, callback);
        }

        private static int StopKmsServer()
        {
            return IntPtr.Size == 8 ? StopKmsServer64() : StopKmsServer32();
        }

        private static uint GetLibKmsVersion()
        {
            return IntPtr.Size == 8 ? GetLibKmsVersion64() : GetLibKmsVersion32();
        }

        private static IntPtr GetEmulatorVersion()
        {
            return IntPtr.Size == 8 ? GetEmulatorVersion64() : GetEmulatorVersion32();
        }
    }
}
