using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryRhythm.Thart
{
    /// <summary>
    /// 极简 ZIP 读写（只用 store，不压缩）。
    ///
    /// 为什么不用 System.IO.Compression：Unity 的 .NET Standard 2.0 里 ZipFile 静态类
    /// 并不保证存在，而 Android(IL2CPP) 上行为更要小心。谱面包里的音频本身已经是
    /// 压缩格式，再压一遍收益极小，所以自己写一份 store-only 实现最稳。
    /// </summary>
    public static class ThartZip
    {
        private const uint LocalSig = 0x04034b50;
        private const uint CentralSig = 0x02014b50;
        private const uint EndSig = 0x06054b50;
        private const int EocdMinSize = 22;
        private const int MaxComment = 0xFFFF;

        public sealed class Entry
        {
            public readonly string Name;
            public readonly byte[] Data;

            public Entry(string name, byte[] data)
            {
                Name = name;
                Data = data ?? new byte[0];
            }
        }

        public static void Write(string path, IList<Entry> entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            ushort dosDate, dosTime;
            ToDosDateTime(DateTime.Now, out dosDate, out dosTime);

            var offsets = new List<long>(entries.Count);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                foreach (var e in entries)
                {
                    offsets.Add(fs.Position);

                    byte[] name = Encoding.UTF8.GetBytes(e.Name);
                    int size = e.Data.Length;
                    uint crc = Crc32(e.Data, 0, size);

                    w.Write(LocalSig);
                    w.Write((ushort)20);      // version needed
                    w.Write((ushort)0x0800);  // 文件名是 UTF-8
                    w.Write((ushort)0);       // store
                    w.Write(dosTime);
                    w.Write(dosDate);
                    w.Write(crc);
                    w.Write((uint)size);      // compressed size
                    w.Write((uint)size);      // uncompressed size
                    w.Write((ushort)name.Length);
                    w.Write((ushort)0);       // extra length
                    w.Write(name);
                    if (size > 0) w.Write(e.Data);
                }

                long cdStart = fs.Position;

                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    byte[] name = Encoding.UTF8.GetBytes(e.Name);
                    int size = e.Data.Length;
                    uint crc = Crc32(e.Data, 0, size);

                    w.Write(CentralSig);
                    w.Write((ushort)20);      // version made by
                    w.Write((ushort)20);      // version needed
                    w.Write((ushort)0x0800);
                    w.Write((ushort)0);       // store
                    w.Write(dosTime);
                    w.Write(dosDate);
                    w.Write(crc);
                    w.Write((uint)size);
                    w.Write((uint)size);
                    w.Write((ushort)name.Length);
                    w.Write((ushort)0);       // extra
                    w.Write((ushort)0);       // comment
                    w.Write((ushort)0);       // disk number
                    w.Write((ushort)0);       // internal attrs
                    w.Write((uint)0);         // external attrs
                    w.Write((uint)offsets[i]);
                    w.Write(name);
                }

                long cdSize = fs.Position - cdStart;

                w.Write(EndSig);
                w.Write((ushort)0);           // this disk
                w.Write((ushort)0);           // disk with central dir
                w.Write((ushort)entries.Count);
                w.Write((ushort)entries.Count);
                w.Write((uint)cdSize);
                w.Write((uint)cdStart);
                w.Write((ushort)0);           // comment length
            }
        }

        /// <summary>读取整个包，返回「条目名 → 字节」。条目名大小写不敏感。</summary>
        public static Dictionary<string, byte[]> Read(string path)
        {
            byte[] all = File.ReadAllBytes(path);
            return Read(all);
        }

        public static Dictionary<string, byte[]> Read(byte[] all)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            int eocd = FindEndOfCentralDirectory(all);
            if (eocd < 0) throw new InvalidDataException("不是有效的 .thr 文件包（找不到 ZIP 目录）");

            int count = ReadU16(all, eocd + 10);
            long cdSize = ReadU32(all, eocd + 12);
            long cdOffset = ReadU32(all, eocd + 16);
            if (cdOffset < 0 || cdOffset + cdSize > all.Length)
                throw new InvalidDataException(".thr 文件包已损坏（目录越界）");

            int p = (int)cdOffset;
            for (int i = 0; i < count && p + 46 <= all.Length; i++)
            {
                if (ReadU32(all, p) != CentralSig) break;

                int method = ReadU16(all, p + 10);
                long csize = ReadU32(all, p + 20);
                int nameLen = ReadU16(all, p + 28);
                int extraLen = ReadU16(all, p + 30);
                int commentLen = ReadU16(all, p + 32);
                long localOffset = ReadU32(all, p + 42);

                if (p + 46 + nameLen > all.Length) break;
                string name = Encoding.UTF8.GetString(all, p + 46, nameLen);
                p += 46 + nameLen + extraLen + commentLen;

                if (localOffset < 0 || localOffset + 30 > all.Length) continue;
                if (ReadU32(all, (int)localOffset) != LocalSig) continue;

                int localNameLen = ReadU16(all, (int)localOffset + 26);
                int localExtraLen = ReadU16(all, (int)localOffset + 28);
                long dataStart = localOffset + 30 + localNameLen + localExtraLen;
                if (dataStart < 0 || dataStart + csize > all.Length) continue;

                if (method != 0)
                    throw new NotSupportedException(".thr 里的「" + name + "」用了压缩方式，当前只支持 store");

                var data = new byte[(int)csize];
                if (csize > 0) Array.Copy(all, (int)dataStart, data, 0, (int)csize);
                result[name] = data;
            }

            return result;
        }

        private static int FindEndOfCentralDirectory(byte[] all)
        {
            if (all.Length < EocdMinSize) return -1;

            int minPos = Math.Max(0, all.Length - EocdMinSize - MaxComment);
            for (int i = all.Length - EocdMinSize; i >= minPos; i--)
            {
                if (ReadU32(all, i) == EndSig) return i;
            }
            return -1;
        }

        private static ushort ReadU16(byte[] b, int i)
        {
            return (ushort)(b[i] | (b[i + 1] << 8));
        }

        private static uint ReadU32(byte[] b, int i)
        {
            return (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
        }

        private static void ToDosDateTime(DateTime t, out ushort dosDate, out ushort dosTime)
        {
            if (t.Year < 1980) t = new DateTime(1980, 1, 1);
            dosDate = (ushort)(((t.Year - 1980) << 9) | (t.Month << 5) | t.Day);
            dosTime = (ushort)((t.Hour << 11) | (t.Minute << 5) | (t.Second / 2));
        }

        #region CRC32

        private static uint[] crcTable;

        private static uint Crc32(byte[] data, int offset, int count)
        {
            if (crcTable == null) BuildCrcTable();

            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++)
                crc = (crc >> 8) ^ crcTable[(crc ^ data[offset + i]) & 0xFF];

            return crc ^ 0xFFFFFFFFu;
        }

        private static void BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            crcTable = table;
        }

        #endregion
    }
}