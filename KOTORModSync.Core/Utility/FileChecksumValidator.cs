// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KOTORModSync.Core.Utility
{
    public class FileChecksumValidator
    {
        private readonly string _destinationPath;
        private readonly Dictionary<FileInfo, SHA1> _expectedChecksums;
        private readonly Dictionary<FileInfo, SHA1> _originalChecksums;

        public FileChecksumValidator(string destinationPath, Dictionary<FileInfo, SHA1> expectedChecksums, Dictionary<FileInfo, SHA1> originalChecksums)
        {
            _destinationPath = destinationPath;
            _expectedChecksums = expectedChecksums;
            _originalChecksums = originalChecksums;
        }

        public static string SHA1ToString(SHA1 sha1) => string.Concat(sha1.Hash.Select(b => b.ToString("x2")));
        public static string StringToSHA1(string s) => string.Concat(SHA1.Create().ComputeHash(Enumerable.Range(0, s.Length)
                                        .Where(x => x % 2 == 0)
                                        .Select(x => Convert.ToByte(s.Substring(x, 2), 16))
                                        .ToArray()).Select(b => b.ToString("x2")));

        public async Task<bool> ValidateChecksumsAsync()
        {
            var actualChecksums = new Dictionary<string, string>();

            foreach (KeyValuePair<FileInfo, SHA1> expectedChecksum in _expectedChecksums)
            {
                FileInfo fileInfo = expectedChecksum.Key;
                if (!fileInfo.Exists)
                {
                    continue;
                }

                SHA1 sha1 = await CalculateSHA1Async(fileInfo);
                actualChecksums[fileInfo.Name] = BitConverter.ToString(sha1.Hash).Replace("-", "");
            }

            bool allChecksumsMatch = actualChecksums.Count == _expectedChecksums.Count
                && actualChecksums.All(x =>
                    _expectedChecksums.TryGetValue(new FileInfo(Path.Combine(_destinationPath, x.Key)), out SHA1 expectedSha1)
                    && BitConverter.ToString(expectedSha1.Hash).Replace("-", "").Equals(x.Value, StringComparison.OrdinalIgnoreCase));

            if (!allChecksumsMatch)
            {
                Logger.Log("Checksum validation failed for the following files:");
                foreach (KeyValuePair<FileInfo, SHA1> expectedChecksum in _expectedChecksums)
                {
                    FileInfo expectedFileInfo = expectedChecksum.Key;
                    SHA1 expectedSha1 = expectedChecksum.Value;
                    string expectedSha1String = BitConverter.ToString(expectedSha1.Hash).Replace("-", "");

                    string actualSha1String = "";
                    if (actualChecksums.TryGetValue(expectedFileInfo.Name, out string actualSha1))
                    {
                        actualSha1String = actualSha1;
                    }

                    if (!actualSha1String.Equals(expectedSha1String, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log($"  {expectedFileInfo.FullName} - expected: {expectedSha1String}, actual: {actualSha1String}");
                    }
                }
            }

            return allChecksumsMatch;
        }

        public static async Task<SHA1> CalculateSHA1Async(FileInfo filePath)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                using (BufferedStream stream = new BufferedStream(File.OpenRead(filePath.FullName), 1200000))
                {
                    byte[] buffer = new byte[81920];
                    List<Task> tasks = new List<Task>();

                    int bytesRead;
                    long totalBytesRead = 0;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytesRead += bytesRead;

                        tasks.Add(
                            Task.Run(
                                () =>
                                {
                                    _ = sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                                }
                            )
                        );

                        if (tasks.Count >= 8)
                        {
                            await Task.WhenAll(tasks);
                            tasks.Clear();
                        }
                    }

                    _ = sha1.TransformFinalBlock(buffer, 0, 0);

                    await Task.WhenAll(tasks);

                    return sha1;
                }
            }
        }

        public static async Task SaveChecksumsToFileAsync(string filePath, Dictionary<DirectoryInfo, SHA1> checksums)
        {
            string json = JsonConvert.SerializeObject(checksums);
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                await writer.WriteAsync(json);
            }
        }

        public static Task<Dictionary<FileInfo, SHA1>> LoadChecksumsFromFileAsync(FileInfo filePath)
        {
            if (!File.Exists(filePath.FullName))
            {
                return Task.FromResult(new Dictionary<FileInfo, SHA1>());
            }

            async Task<Dictionary<FileInfo, SHA1>> LocalFunction()
            {
                using (StreamReader reader = new StreamReader(filePath.FullName))
                {
                    string json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<Dictionary<FileInfo, SHA1>>(json);
                }
            }

            return LocalFunction();
        }
    }
}
