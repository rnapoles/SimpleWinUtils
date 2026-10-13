// Usage: program.exe <input-folder> <output-folder>
// Compile (C# 5): csc /out:Mp3YearSorter.exe Mp3YearSorter.cs
//
// Recursively scans <input-folder> for .mp3 files, reads the release year
// from the ID3 tag (ID3v2.2/2.3/2.4, falling back to ID3v1) and moves each
// file into <output-folder>\<year>\. If the tags have no year, the oldest of
// the file's creation date and modified date is used instead.
// Creation, modified and last-access times and file attributes are preserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Mp3YearSorter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: program.exe <input-folder> <output-folder>");
                return 1;
            }

            string inputFolder = Path.GetFullPath(args[0]);
            string outputFolder = Path.GetFullPath(args[1]);

            if (!Directory.Exists(inputFolder))
            {
                Console.WriteLine("Input folder not found: " + inputFolder);
                return 1;
            }

            try
            {
                Directory.CreateDirectory(outputFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot create output folder: " + ex.Message);
                return 1;
            }

            List<string> files = new List<string>();
            CollectMp3Files(inputFolder, outputFolder, files);
            Console.WriteLine("Found " + files.Count + " MP3 file(s).");

            int moved = 0;
            int failed = 0;

            foreach (string file in files)
            {
                try
                {
                    string source;
                    string year = GetYear(file);
                    if (year != null)
                    {
                        source = "tag";
                    }
                    else
                    {
                        // No year in the tags: use the oldest of creation / modified date.
                        year = GetOldestFileDateYear(file);
                        source = "file date";
                    }

                    string targetDir = Path.Combine(outputFolder, year);
                    Directory.CreateDirectory(targetDir);

                    string targetPath = GetUniquePath(targetDir, Path.GetFileName(file));
                    MoveWithAttributes(file, targetPath);

                    Console.WriteLine("[" + year + " - " + source + "] " + file);
                    moved++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FAILED: " + file + " (" + ex.Message + ")");
                    failed++;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Done. Moved: " + moved + ", failed: " + failed);
            return failed == 0 ? 0 : 2;
        }

        // ---------------------------------------------------------------
        // File-date fallback and attribute-preserving move
        // ---------------------------------------------------------------
        private static string GetOldestFileDateYear(string path)
        {
            DateTime created = File.GetCreationTimeUtc(path);
            DateTime modified = File.GetLastWriteTimeUtc(path);
            DateTime oldest = created < modified ? created : modified;
            return oldest.ToLocalTime().Year.ToString();
        }

        private static void MoveWithAttributes(string source, string target)
        {
            // Capture everything before the move.
            DateTime created = File.GetCreationTimeUtc(source);
            DateTime modified = File.GetLastWriteTimeUtc(source);
            DateTime accessed = File.GetLastAccessTimeUtc(source);
            FileAttributes attributes = File.GetAttributes(source);

            File.Move(source, target);

            // A move across volumes is a copy + delete, which can reset the
            // timestamps, so restore them explicitly (harmless on same volume).
            // Clear read-only first, otherwise setting the times can fail.
            File.SetAttributes(target, FileAttributes.Normal);
            File.SetCreationTimeUtc(target, created);
            File.SetLastWriteTimeUtc(target, modified);
            File.SetLastAccessTimeUtc(target, accessed);
            File.SetAttributes(target, attributes);
        }

        // ---------------------------------------------------------------
        // Recursive scan (manual, so one inaccessible folder doesn't abort)
        // ---------------------------------------------------------------
        private static void CollectMp3Files(string folder, string outputFolder, List<string> result)
        {
            // Skip the output folder if it lives inside the input folder.
            if (IsSamePath(folder, outputFolder))
            {
                return;
            }

            try
            {
                foreach (string file in Directory.GetFiles(folder, "*.mp3"))
                {
                    if (file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(file);
                    }
                }

                foreach (string sub in Directory.GetDirectories(folder))
                {
                    CollectMp3Files(sub, outputFolder, result);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Access denied, skipping: " + folder);
            }
            catch (IOException ex)
            {
                Console.WriteLine("I/O error in " + folder + ": " + ex.Message);
            }
        }

        private static bool IsSamePath(string a, string b)
        {
            char[] trim = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            return string.Equals(a.TrimEnd(trim), b.TrimEnd(trim), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetUniquePath(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int counter = 2;
            while (true)
            {
                path = Path.Combine(directory, name + " (" + counter + ")" + ext);
                if (!File.Exists(path))
                {
                    return path;
                }
                counter++;
            }
        }

        // ---------------------------------------------------------------
        // Year extraction
        // ---------------------------------------------------------------
        private static string GetYear(string path)
        {
            string year = null;
            try
            {
                year = ReadId3v2Year(path);
            }
            catch (Exception)
            {
                // Corrupt tag: fall through to ID3v1.
            }

            if (year == null)
            {
                try
                {
                    year = ReadId3v1Year(path);
                }
                catch (Exception)
                {
                }
            }

            return year;
        }

        private static string ReadId3v1Year(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Length < 128)
                {
                    return null;
                }

                fs.Seek(-128, SeekOrigin.End);
                byte[] block = new byte[128];
                if (ReadFully(fs, block, 128) != 128)
                {
                    return null;
                }

                if (block[0] != 'T' || block[1] != 'A' || block[2] != 'G')
                {
                    return null;
                }

                string text = Encoding.ASCII.GetString(block, 93, 4);
                return ExtractYear(text);
            }
        }

        private static string ReadId3v2Year(string path)
        {
            byte[] tag;
            int version;
            byte flags;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] header = new byte[10];
                if (ReadFully(fs, header, 10) != 10)
                {
                    return null;
                }

                if (header[0] != 'I' || header[1] != 'D' || header[2] != '3')
                {
                    return null;
                }

                version = header[3]; // 2, 3 or 4
                flags = header[5];
                int tagSize = SyncSafe(header, 6);

                if (version < 2 || version > 4 || tagSize <= 0 || tagSize > 32 * 1024 * 1024)
                {
                    return null;
                }

                tag = new byte[tagSize];
                int read = ReadFully(fs, tag, tagSize);
                if (read < tagSize)
                {
                    Array.Resize(ref tag, read);
                }
            }

            int pos = 0;

            // Skip the extended header if present (v2.3 / v2.4 only).
            if (version >= 3 && (flags & 0x40) != 0 && tag.Length >= 4)
            {
                if (version == 4)
                {
                    pos = SyncSafe(tag, 0);
                }
                else
                {
                    pos = 4 + BigEndianInt(tag, 0);
                }
            }

            int idLength = version == 2 ? 3 : 4;
            int frameHeaderLength = version == 2 ? 6 : 10;

            // Candidate year frames, in order of preference.
            string[] wanted;
            if (version == 2)
            {
                wanted = new string[] { "TYE", "TOR" };
            }
            else if (version == 3)
            {
                wanted = new string[] { "TYER", "TORY" };
            }
            else
            {
                wanted = new string[] { "TDRC", "TDRL", "TDOR", "TYER" };
            }

            Dictionary<string, string> found = new Dictionary<string, string>();

            while (pos + frameHeaderLength <= tag.Length)
            {
                if (tag[pos] == 0)
                {
                    break; // padding
                }

                string id = Encoding.ASCII.GetString(tag, pos, idLength);
                int size;
                if (version == 2)
                {
                    size = (tag[pos + 3] << 16) | (tag[pos + 4] << 8) | tag[pos + 5];
                }
                else if (version == 3)
                {
                    size = BigEndianInt(tag, pos + 4);
                }
                else
                {
                    size = SyncSafe(tag, pos + 4);
                }

                int dataStart = pos + frameHeaderLength;
                if (size <= 0 || dataStart + size > tag.Length)
                {
                    break;
                }

                if (Array.IndexOf(wanted, id) >= 0 && !found.ContainsKey(id))
                {
                    string text = DecodeText(tag, dataStart, size);
                    string year = ExtractYear(text);
                    if (year != null)
                    {
                        found[id] = year;
                    }
                }

                pos = dataStart + size;
            }

            foreach (string id in wanted)
            {
                string year;
                if (found.TryGetValue(id, out year))
                {
                    return year;
                }
            }

            return null;
        }

        private static string DecodeText(byte[] data, int start, int length)
        {
            if (length < 2)
            {
                return string.Empty;
            }

            byte encodingByte = data[start];
            int textStart = start + 1;
            int textLength = length - 1;

            Encoding encoding;
            switch (encodingByte)
            {
                case 0:
                    encoding = Encoding.GetEncoding("ISO-8859-1");
                    break;
                case 1:
                    if (textLength >= 2 && data[textStart] == 0xFE && data[textStart + 1] == 0xFF)
                    {
                        encoding = Encoding.BigEndianUnicode;
                    }
                    else
                    {
                        encoding = Encoding.Unicode; // little endian (BOM FF FE or none)
                    }
                    break;
                case 2:
                    encoding = Encoding.BigEndianUnicode;
                    break;
                case 3:
                    encoding = Encoding.UTF8;
                    break;
                default:
                    encoding = Encoding.GetEncoding("ISO-8859-1");
                    break;
            }

            return encoding.GetString(data, textStart, textLength).Trim('\0', ' ', '\uFEFF');
        }

        private static string ExtractYear(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            Match m = Regex.Match(text, @"(?<!\d)(\d{4})(?!\d)");
            if (!m.Success)
            {
                return null;
            }

            int year = int.Parse(m.Groups[1].Value);
            if (year < 1800 || year > 2100)
            {
                return null;
            }

            return m.Groups[1].Value;
        }

        // ---------------------------------------------------------------
        // Low-level helpers
        // ---------------------------------------------------------------
        private static int SyncSafe(byte[] b, int offset)
        {
            return ((b[offset] & 0x7F) << 21)
                 | ((b[offset + 1] & 0x7F) << 14)
                 | ((b[offset + 2] & 0x7F) << 7)
                 | (b[offset + 3] & 0x7F);
        }

        private static int BigEndianInt(byte[] b, int offset)
        {
            return (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
        }

        private static int ReadFully(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n <= 0)
                {
                    break;
                }
                total += n;
            }
            return total;
        }
    }
}