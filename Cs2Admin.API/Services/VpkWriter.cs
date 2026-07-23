using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cs2Admin.API.Services
{
    public static class Crc32
    {
        private static readonly uint[] Table;

        static Crc32()
        {
            Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint entry = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) != 0)
                        entry = (entry >> 1) ^ 0xEDB88320;
                    else
                        entry >>= 1;
                }
                Table[i] = entry;
            }
        }

        public static uint Compute(byte[] buffer)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in buffer)
            {
                crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
            }
            return ~crc;
        }
    }

    public static class VpkWriter
    {
        private class VpkFileEntry
        {
            public string FullPath { get; set; } = "";
            public string Extension { get; set; } = "";
            public string DirectoryPath { get; set; } = "";
            public string FileNameWithoutExtension { get; set; } = "";
            public uint Crc32 { get; set; }
            public uint Length { get; set; }
            public uint Offset { get; set; }
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        /// <summary>
        /// Creates a Valve VPK v2 package from the specified source directory and saves it to outputVpkPath.
        /// </summary>
        public static void CreateFromDirectory(string sourceDir, string outputVpkPath)
        {
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException($"Source directory '{sourceDir}' not found.");

            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            if (allFiles.Length == 0) return;

            var entries = new List<VpkFileEntry>();

            uint currentOffset = 0;
            foreach (var filePath in allFiles)
            {
                var relativePath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
                var ext = Path.GetExtension(relativePath).TrimStart('.');
                var dir = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "";
                var fileName = Path.GetFileNameWithoutExtension(relativePath);

                byte[] fileData = File.ReadAllBytes(filePath);
                uint crc = Crc32.Compute(fileData);

                entries.Add(new VpkFileEntry
                {
                    FullPath = filePath,
                    Extension = ext.ToLowerInvariant(),
                    DirectoryPath = dir.ToLowerInvariant(),
                    FileNameWithoutExtension = fileName.ToLowerInvariant(),
                    Crc32 = crc,
                    Length = (uint)fileData.Length,
                    Offset = currentOffset,
                    Data = fileData
                });

                currentOffset += (uint)fileData.Length;
            }

            // Group by Extension -> DirectoryPath
            var grouped = entries
                .GroupBy(e => e.Extension)
                .OrderBy(g => g.Key)
                .ToList();

            using var treeStream = new MemoryStream();
            using var treeWriter = new BinaryWriter(treeStream, Encoding.ASCII);

            foreach (var extGroup in grouped)
            {
                treeWriter.Write(Encoding.ASCII.GetBytes(extGroup.Key));
                treeWriter.Write((byte)0); // Null terminator for extension

                var dirGroups = extGroup
                    .GroupBy(e => e.DirectoryPath)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var dirGroup in dirGroups)
                {
                    treeWriter.Write(Encoding.ASCII.GetBytes(dirGroup.Key));
                    treeWriter.Write((byte)0); // Null terminator for directory

                    foreach (var entry in dirGroup.OrderBy(e => e.FileNameWithoutExtension))
                    {
                        treeWriter.Write(Encoding.ASCII.GetBytes(entry.FileNameWithoutExtension));
                        treeWriter.Write((byte)0); // Null terminator for filename

                        treeWriter.Write(entry.Crc32);          // CRC32 (4 bytes)
                        treeWriter.Write((ushort)0);             // PreloadBytes (2 bytes)
                        treeWriter.Write((ushort)0x7FFF);         // ArchiveIndex = 0x7FFF for inline single file VPK (2 bytes)
                        treeWriter.Write(entry.Offset);          // EntryOffset (4 bytes)
                        treeWriter.Write(entry.Length);          // EntryLength (4 bytes)
                        treeWriter.Write((ushort)0xFFFF);         // Terminator 0xFFFF (2 bytes)
                    }

                    treeWriter.Write((byte)0); // End of directory block
                }

                treeWriter.Write((byte)0); // End of extension block
            }

            treeWriter.Write((byte)0); // End of tree

            byte[] treeBytes = treeStream.ToArray();

            var outputDir = Path.GetDirectoryName(outputVpkPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var outputStream = new FileStream(outputVpkPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(outputStream);

            // VPK v2 Header (28 bytes)
            writer.Write((uint)0x55AA1234);             // Magic
            writer.Write((uint)2);                      // Version = 2
            writer.Write((uint)treeBytes.Length);       // TreeSize
            writer.Write(currentOffset);               // FileDataSectionSize
            writer.Write((uint)0);                      // ArchiveMD5SectionSize
            writer.Write((uint)0);                      // OtherMD5SectionSize
            writer.Write((uint)0);                      // SignatureSectionSize

            // Write Tree
            writer.Write(treeBytes);

            // Write File Data
            foreach (var entry in entries)
            {
                writer.Write(entry.Data);
            }

            outputStream.Flush();
        }
    }
}
