// ReSharper disable RedundantUsingDirective
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.Vlmcs
{
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public unsafe struct KmsGuid : IEquatable<Guid>, IEquatable<string>, IEquatable<KmsGuid>
    {
        public fixed byte Data[16];
        public static KmsGuid InvalidGuid = new([0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,]);
        public static KmsGuid Empty = new(Guid.Empty);

        public bool Equals(string guidString) => guidString != null && ((Guid)this).Equals(new Guid(guidString));
        public bool Equals(Guid guid) => ((Guid)this).Equals(guid);
        public bool Equals(KmsGuid kmsGuid) => ((Guid)this).Equals(kmsGuid);
        public static bool operator ==(KmsGuid kmsGuid1, KmsGuid kmsGuid2) => kmsGuid1.GetHashCode() == kmsGuid2.GetHashCode() && kmsGuid1.Equals(kmsGuid2);
        public static bool operator !=(KmsGuid kmsGuid1, KmsGuid kmsGuid2) => !(kmsGuid1 == kmsGuid2);
        public static bool operator ==(KmsGuid kmsGuid, string guidString) => kmsGuid.Equals(guidString);
        public static bool operator !=(KmsGuid kmsGuid, string guidString) => !kmsGuid.Equals(guidString);
        public static bool operator ==(string guidString, KmsGuid kmsGuid) => kmsGuid.Equals(guidString);
        public static bool operator !=(string guidString, KmsGuid kmsGuid) => !(guidString == kmsGuid);
        public static bool operator ==(KmsGuid kmsGuid, Guid guid) => kmsGuid.Equals(guid);
        public static bool operator !=(KmsGuid kmsGuid, Guid guid) => !kmsGuid.Equals(guid);
        public static bool operator ==(Guid guid, KmsGuid kmsGuid) => kmsGuid.Equals(guid);
        public static bool operator !=(Guid guid, KmsGuid kmsGuid) => !kmsGuid.Equals(guid);
        public static implicit operator KmsGuid(Guid guid) => *(KmsGuid*)&guid;
        public static implicit operator Guid(KmsGuid kmsGuid) => *(Guid*)&kmsGuid;
        public override bool Equals(object obj) => obj != null && ToString().ToUpperInvariant() == obj.ToString().ToUpperInvariant();

        public override int GetHashCode()
        {
            fixed (byte* b = Data)
            {
                return *(int*)(b + 12);
            }
        }

        private void FromByteArray(IReadOnlyList<byte> guidBytes)
        {
            if (guidBytes.Count != 16) throw new ArgumentException("GUIDs must have a length of 16 bytes.");

            for (int i = 0; i < 16; i++)
            {
                Data[i] = guidBytes[i];
            }
        }

        public uint Part1
        {
            get
            {
                fixed (byte* b = Data)
                {
                    return *(uint*)b;
                }
            }
        }

        public ushort Part2
        {
            get
            {
                fixed (byte* b = Data)
                {
                    return *(ushort*)(b + 4);
                }
            }
        }

        public ushort Part3
        {
            get
            {
                fixed (byte* b = Data)
                {
                    return *(ushort*)(b + 6);
                }
            }
        }

        public byte[] Part4 => new[] { Data[8], Data[9], Data[10], Data[11], Data[12], Data[13], Data[14], Data[15], };

        public KmsGuid(IReadOnlyList<byte> guidBytes)
        {
            FromByteArray(guidBytes);
        }

        public KmsGuid(string guidString)
        {
            Guid guid = new Guid(guidString);
            FromByteArray(guid.ToByteArray());
        }

        public KmsGuid(Guid guid)
        {
            FromByteArray(guid.ToByteArray());
        }

        public override string ToString() => ((Guid)this).ToString();

        public static KmsGuid NewGuid() => Guid.NewGuid();
    }

}
