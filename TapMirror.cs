using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable once CheckNamespace

namespace HGM.Hotbird64.LicenseManager
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.None)]
    public struct IpHeader // All fields in big-endian (network) order
    {
        private readonly byte ipHlV;
        public byte Tos;
        public short Length;
        public ushort Id;
        public short FragmentOffset;
        public byte Ttl;
        public byte Protocol;
        public ushort Checksum;
        public uint SourceIP;
        public uint DestinationIP;

#if DEBUG
        public byte HeaderLength => unchecked((byte)(ipHlV & 15));
        public byte Version => unchecked((byte)(ipHlV >> 4));
#endif
    }

    public static class TapMirror
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private const uint METHOD_BUFFERED = 0;
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private const uint FILE_ANY_ACCESS = 0;
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private const uint FILE_DEVICE_UNKNOWN = 0x00000022;
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private const int FILE_ATTRIBUTE_SYSTEM = 0x4;
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private const int FILE_FLAG_OVERLAPPED = 0x40000000;

        public enum TapIoctl
        {
            GetMacAddress = 1,
            GetVersion = 2,
            GetMtu = 3,
            GetInfo = 4, // Not implemented in NDIS6 versions
            ConfigurePointToPoint = 5, // Superseeded by ConfigureIPv4Tunnel
            SetMediaStatus = 6,
            ConfigDhcpMasquerade = 7,
            GetLogLine = 8, // Only enabled in DEBUG builds
            SetDhcpOption = 9,
            ConfigureIPv4Tunnel = 10, // Requires 8.2 or greater
            SetArpSourceCheck = 11, // Requires 9.??? or greater
        }

        public class TapDeviceVariant
        {
            public string Class;
            public string Suffix;
        }

        public class TapDeviceVariants : HashSet<TapDeviceVariant>
        {
            public string this[string Class] => this.FirstOrDefault(l => l.Class == Class)?.Suffix;
        }

        public class TapDevice
        {
            public string Guid;
            public string Name;
            public string ClassName;
            public string DeviceSuffix;
            public SafeFileHandle Handle;
            public override string ToString() => $"{Name} ({ClassName})";
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.None)]
        public struct TapConfigTun
        {
            private int address;
            private int network;
            private int mask;

            public int Address
            {
                get => IPAddress.NetworkToHostOrder(address);
                set => address = IPAddress.HostToNetworkOrder(value);
            }

            public int Network
            {
                get => IPAddress.NetworkToHostOrder(network);
                set => network = IPAddress.HostToNetworkOrder(value);
            }

            public int Mask
            {
                get => IPAddress.NetworkToHostOrder(mask);
                set => mask = IPAddress.HostToNetworkOrder(value);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.None)]
        public struct TapConfigDhcp
        {
            private int address;
            private int mask;
            private int dhcpServer;
            public int LeaseDuration;

            public int Address
            {
                get => IPAddress.NetworkToHostOrder(address);
                set => address = IPAddress.HostToNetworkOrder(value);
            }

            public int Mask
            {
                get => IPAddress.NetworkToHostOrder(mask);
                set => mask = IPAddress.HostToNetworkOrder(value);
            }

            public int DhcpServer
            {
                get => IPAddress.NetworkToHostOrder(dhcpServer);
                set => dhcpServer = IPAddress.HostToNetworkOrder(value);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.None)]
        public struct TapDriverVersion
        {
            public int Major;
            public int Minor;
            public int Build; //unused but reported as 0
            public int Revision; //unused and not reported
        }

        private static FileStream tap;
        private static TapDevice device;
        private static bool isVirtualCableConnected;

        private static readonly TapDeviceVariants tapDeviceVariants = new TapDeviceVariants
        {
            new TapDeviceVariant { Class = "tap0801", Suffix = "tap" },
            new TapDeviceVariant { Class = "tap0901", Suffix = "tap" },
            new TapDeviceVariant { Class = "TEAMVIEWERVPN", Suffix = "dgt" },
        };

        public static bool IsStarted => tap != null;

        public static Version Start(string subnet, string tapName = null)
        {
            ParseSubnet(subnet, out int address, out int network, out int mask);
            device = OpenTapHandle(tapName);
            Version version = DriverVersion;
            if (version.Major == 8 && version.Minor < 2) throw new NotSupportedException("TAP driver 8.x or 9.x greater than 8.2 required");

            SetSubnet(address, network, mask);
            EnableDhcp(address, mask);
            IsVirtualCableConnected = true;

            new Thread(Mirror).Start();
            return version;
        }

        public static void Stop()
        {
            tap?.Dispose();
            device?.Handle?.Dispose();
            device = null;
            tap = null;
        }

        private static unsafe void Mirror()
        {
            tap = new FileStream(device.Handle, FileAccess.ReadWrite, Mtu, true);
            byte[] buffer = new byte[Mtu];

            while (true)
            {
                try
                {
                    int bytesRead = tap.Read(buffer, 0, buffer.Length);

                    fixed (byte* b = buffer)
                    {
                        IpHeader* packet = (IpHeader*)b;
                        uint temp = packet->SourceIP;
                        packet->SourceIP = packet->DestinationIP;
                        packet->DestinationIP = temp;
                    }

                    tap.Write(buffer, 0, bytesRead);

                    if (Mtu > buffer.Length) buffer = new byte[Mtu];
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    Stop();
                    throw;
                }
            }
        }

        private static unsafe int Mtu
        {
            get
            {
                int tapMtu;
                DevCtl(TapIoctl.GetMtu, &tapMtu, sizeof(int));
                return tapMtu;
            }
        }

        public static unsafe Version DriverVersion
        {
            get
            {
                TapDriverVersion tapDriverVersion = new TapDriverVersion();
                int len = DevCtl(TapIoctl.GetVersion, &tapDriverVersion, sizeof(TapDriverVersion));

                switch (len)
                {
                    case sizeof(int) * 2:
                        return new Version(tapDriverVersion.Major, tapDriverVersion.Minor);
                    case sizeof(int) * 3:
                        return new Version(tapDriverVersion.Major, tapDriverVersion.Minor, tapDriverVersion.Build);
                    case sizeof(int) * 4:
                        return new Version(tapDriverVersion.Major, tapDriverVersion.Minor, tapDriverVersion.Build, tapDriverVersion.Revision);
                    default:
                        throw new InvalidOperationException("Cannot determine TAP driver version");
                }
            }
        }

        public static string IpAddressString(int address) => new IPAddress(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(address))).ToString();

        public static int IpAddressInt(string ipAddressString)
        {
            IPAddress ipAddress = IPAddress.Parse(ipAddressString);
            if (ipAddress.AddressFamily != AddressFamily.InterNetwork) throw new FormatException($"{ipAddressString} is not a valid IPv4 address");
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0));
        }

        private static void ParseSubnet(string subnet, out int address, out int network, out int mask)
        {
            int cidr;
            string[] split = subnet.Split('/');
            if (split.Length > 2) throw new FormatException("Subnet must be <IPv4 address>[/<CIDR mask>]");

            if (split.Length == 2)
            {
                cidr = int.Parse(split[1], NumberStyles.None, CultureInfo.InvariantCulture);
                if (cidr > 30 || cidr < 8) throw new ArgumentOutOfRangeException(nameof(cidr), "CIDR must be between 8 and 30");
                address = IpAddressInt(split[0]);
            }
            else
            {
                cidr = 30;
                address = IpAddressInt(subnet);
            }

            mask = unchecked((int)~(uint.MaxValue >> cidr));
            network = address & mask;
            int broadcast = address | ~mask;

            unchecked
            {
                if ((uint)address <= (uint)network || (uint)address + 1 >= (uint)broadcast)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(address),
                        $"For this subnet IPv4 address must be {((uint)network + 1 != (uint)broadcast - 2 ? $"between {IpAddressString((int)(uint)(network + 1))} and {IpAddressString((int)(uint)(broadcast - 2))}" : $"{IpAddressString((int)(uint)(network + 1))}")}"
                    );
                }
            }
        }

        public static bool IsValidSubnet(string subnet, out string reason)
        {
            reason = null;

            try
            {
                ParseSubnet(subnet, out _, out _, out _);
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        private static unsafe void SetSubnet(int address, int network, int mask)
        {
            TapConfigTun tapConfigTun = new TapConfigTun { Address = address, Network = network, Mask = mask };
            DevCtl(TapIoctl.ConfigureIPv4Tunnel, &tapConfigTun, sizeof(TapConfigTun));
        }

        private static unsafe void EnableDhcp(int address, int mask)
        {
            TapConfigDhcp tapConfigDhcp = new TapConfigDhcp
            {
                Address = address,
                Mask = mask,
                DhcpServer = unchecked((int)(uint)(address + 1)),
                LeaseDuration = 24 * 60 * 60
            };

            DevCtl(TapIoctl.ConfigDhcpMasquerade, &tapConfigDhcp, sizeof(TapConfigDhcp));
        }

        public static unsafe bool IsVirtualCableConnected
        {
            get => isVirtualCableConnected && IsStarted;
            set
            {
                int status = value ? 1 : 0;
                DevCtl(TapIoctl.SetMediaStatus, &status, sizeof(int));
                isVirtualCableConnected = value;
            }
        }

        public static IEnumerable<TapDevice> GetTapDevices()
        {
            const string adapterKey = @"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";

            using (RegistryKey regAdapters = Registry.LocalMachine.OpenSubKey(adapterKey, writable: false))
            {
                string[] keyNames = regAdapters?.GetSubKeyNames();
                if (keyNames == null) yield break;

                foreach (string keyName in keyNames)
                {
                    RegistryKey regAdapter;

                    try
                    {
                        regAdapter = regAdapters.OpenSubKey(keyName, writable: false);
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        string id = regAdapter?.GetValue("ComponentId")?.ToString();
                        if (!tapDeviceVariants.Select(v => v.Class).Contains(id) || id == null) continue;
                        string guid = regAdapter.GetValue("NetCfgInstanceId").ToString();

                        yield return new TapDevice
                        {
                            DeviceSuffix = tapDeviceVariants[id],
                            ClassName = id,
                            Guid = guid,
                            Name = GetDisplayName(guid)
                        };
                    }
                    finally
                    {
                        regAdapter?.Dispose();
                    }
                }
            }
        }

        private static TapDevice OpenTapHandle(string deviceName = null)
        {
            foreach (TapDevice tapDevice in GetTapDevices().Where(d => deviceName == null || d.Name == deviceName))
            {
                SafeFileHandle tapHandle = CreateFileW
                (
                    $@"\\.\Global\{tapDevice.Guid}.{tapDevice.DeviceSuffix}",
                    FileAccess.ReadWrite, FileShare.None, IntPtr.Zero, FileMode.Open,
                    FILE_ATTRIBUTE_SYSTEM | FILE_FLAG_OVERLAPPED, IntPtr.Zero
                );

                if (tapHandle.IsInvalid) continue;

                tapDevice.Handle = tapHandle;
                return tapDevice;
            }

            throw new ConfigurationErrorsException($"No VPN devices{(deviceName == null ? "" : $" named \"{deviceName}\"")} available for use");
        }

        private static string GetDisplayName(string tapGuid)
        {
            RegistryKey regConnection = Registry.LocalMachine.OpenSubKey
            (
                $@"SYSTEM\CurrentControlSet\Control\Network\{{4D36E972-E325-11CE-BFC1-08002BE10318}}\{tapGuid}\Connection",
                true
            );

            return regConnection?.GetValue("Name")?.ToString() ?? "";
        }

        private static unsafe int DevCtl(TapIoctl code, void* data, int len)
        {
            if
            (
                device?.Handle == null ||
                device.Handle.IsClosed ||
                device.Handle.IsInvalid
            )
            {
                throw new InvalidOperationException("Not connected to a TAP device");
            }

            if (!DeviceIoControl
            (
                device.Handle, (FILE_DEVICE_UNKNOWN << 16) | (FILE_ANY_ACCESS << 14) | ((uint)code << 2) | METHOD_BUFFERED,
                data, len, data, len, out len, IntPtr.Zero
            ))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return len;
        }

        [DllImport("Kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
        private static extern SafeFileHandle CreateFileW(
            string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccess fileaccess,
            [MarshalAs(UnmanagedType.U4)] FileShare fileshare,
            IntPtr securityattributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationdisposition,
            uint flags,
            IntPtr template);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.None, CallingConvention = CallingConvention.Winapi)]
        private static extern unsafe bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            void* lpInBuffer,
            int nInBufferSize,
            void* lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);
    }
}
