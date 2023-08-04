// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace KOTORModSync.Core.Utility
{
    public class FileChecksumValidator
    {
        private readonly string _destinationPath;
        private readonly Dictionary<FileInfo, SHA1> _expectedChecksums;
        private readonly Dictionary<FileInfo, SHA1> _originalChecksums;

        public FileChecksumValidator(
            [CanBeNull] string destinationPath,
            [CanBeNull] Dictionary<FileInfo, SHA1> expectedChecksums,
            [CanBeNull] Dictionary<FileInfo, SHA1> originalChecksums
        )
        {
            _destinationPath = destinationPath;
            _expectedChecksums = expectedChecksums;
            _originalChecksums = originalChecksums;
        }

        [NotNull]
        public static string Sha1ToString( [NotNull] SHA1 sha1 ) =>
            sha1 == null
                ? throw new ArgumentNullException( nameof( sha1 ) )
                : string.Concat( sha1.Hash.Select( b => b.ToString( format: "x2" ) ) );

        [NotNull]
        public static string StringToSha1( [NotNull] string s ) => s == null
            ? throw new ArgumentNullException( nameof( s ) )
            : string.Concat(
                SHA1.Create()
                    .ComputeHash(
                        Enumerable.Range(
                                start: 0,
                                s.Length
                            )
                            .Where( x => x % 2 == 0 )
                            .Select(
                                x => Convert.ToByte(
                                    s.Substring(
                                        x,
                                        length: 2
                                    ),
                                    fromBase: 16
                                )
                            )
                            .ToArray()
                    )
                    .Select( b => b.ToString( "x2" ) )
            );

        public async Task<bool> ValidateChecksumsAsync()
        {
            var actualChecksums = new Dictionary<string, string>();

            foreach ( KeyValuePair<FileInfo, SHA1> expectedChecksum in _expectedChecksums )
            {
                FileInfo fileInfo = expectedChecksum.Key;
                if ( fileInfo?.Exists != true )
                    continue;

                SHA1 sha1 = await CalculateSha1Async( fileInfo );
                actualChecksums[fileInfo.Name] = BitConverter.ToString( sha1.Hash )
                    .Replace(
                        oldValue: "-",
                        newValue: ""
                    );
            }

            bool allChecksumsMatch = actualChecksums.Count == _expectedChecksums.Count
                && actualChecksums.All(
                    x => _expectedChecksums.TryGetValue(
                            new FileInfo( Path.Combine( _destinationPath, x.Key ) ),
                            out SHA1 expectedSha1
                        )
                        // ReSharper disable once PossibleNullReferenceException
                        && BitConverter.ToString( expectedSha1.Hash )
                            .Replace( oldValue: "-", newValue: "" )
                            .Equals( x.Value, StringComparison.OrdinalIgnoreCase )
                );

            if ( allChecksumsMatch )
                return true;

            await Logger.LogAsync( "Checksum validation failed for the following files:" );
            foreach ( KeyValuePair<FileInfo, SHA1> expectedChecksum in _expectedChecksums )
            {
                FileInfo expectedFileInfo = expectedChecksum.Key;
                SHA1 expectedSha1 = expectedChecksum.Value;
                string expectedSha1String = BitConverter.ToString( expectedSha1.Hash )
                    .Replace( oldValue: "-", newValue: "" );

                if ( !actualChecksums.TryGetValue( expectedFileInfo.Name, out string actualSha1 ) )
                {
                    await Logger.LogAsync(
                        $"Problem looking up sha1 of {expectedFileInfo.FullName} - expected: {expectedSha1String}"
                    );
                    continue;
                }

                if ( actualSha1.Equals( expectedSha1String, StringComparison.OrdinalIgnoreCase ) )
                    continue;

                await Logger.LogAsync(
                    $"  {expectedFileInfo.FullName} - expected: {expectedSha1String}, actual: {actualSha1}"
                );
            }

            return false;
        }

        [ItemNotNull]
        public static async Task<SHA1> CalculateSha1Async( [NotNull] FileInfo filePath )
        {
            var sha1 = SHA1.Create();
            using ( FileStream stream = File.OpenRead( filePath.FullName ) )
            {
                byte[] buffer = new byte[81920];
                var tasks = new List<Task>();

                int bytesRead;

                while ( ( bytesRead = await stream.ReadAsync( buffer, offset: 0, buffer.Length ) ) > 0 )
                {
                    byte[] data = new byte[bytesRead];
                    Buffer.BlockCopy( buffer, srcOffset: 0, data, dstOffset: 0, bytesRead );

                    int read = bytesRead;

                    tasks.Add(
                        Task.Run(
                            () =>
                                _ = sha1.TransformBlock(
                                    data,
                                    inputOffset: 0,
                                    read,
                                    outputBuffer: null,
                                    outputOffset: 0
                                )
                        )
                    );

                    if ( tasks.Count < Environment.ProcessorCount * 2 )
                        continue;

                    await Task.WhenAll( tasks );
                    tasks.Clear();
                }

                await Task.WhenAll( tasks );

                _ = sha1.TransformFinalBlock( buffer, inputOffset: 0, inputCount: 0 );

                return sha1;
            }
        }

        public static async Task SaveChecksumsToFileAsync(
            [NotNull] string filePath,
            [CanBeNull] Dictionary<DirectoryInfo, SHA1> checksums
        )
        {
            if ( filePath == null )
                throw new ArgumentNullException( nameof( filePath ) );

            string json = JsonConvert.SerializeObject( checksums );
            using ( var writer = new StreamWriter( filePath ) )
            {
                await writer.WriteAsync( json );
            }
        }

        [ItemNotNull]
        public static async Task<Dictionary<FileInfo, SHA1>> LoadChecksumsFromFileAsync( [NotNull] FileInfo filePath )
        {
            if ( filePath == null )
                throw new ArgumentNullException( nameof( filePath ) );

            if ( !File.Exists( filePath.FullName ) )
                return new Dictionary<FileInfo, SHA1>();

            var checksums = new Dictionary<FileInfo, SHA1>();

            using ( var reader = new StreamReader( filePath.FullName ) )
            {
                string line;
                while ( ( line = await reader.ReadLineAsync() ) != null )
                {
                    string[] parts = line.Split( ',' );
                    if ( parts.Length != 2 )
                        continue;

                    string file = parts[0];
                    string hash = parts[1];

                    var fileInfo = new FileInfo( file );
                    if ( !fileInfo.Exists )
                    {
                        await Logger.LogAsync( $"File does not exist: {fileInfo.FullName}" );
                        continue;
                    }

                    await Logger.LogAsync( $"Reading file: {fileInfo.FullName}" );

                    using ( FileStream fileStream = fileInfo.OpenRead() )
                    {
                        byte[] fileBytes = new byte[fileStream.Length];
                        _ = await fileStream.ReadAsync( fileBytes, offset: 0, fileBytes.Length );

                        if ( !TryConvertHexStringToBytes( hash, out byte[] hashBytes ) )
                        {
                            await Logger.LogAsync( $"Failed to convert hash string: {hash}" );
                            continue;
                        }

                        await Logger.LogAsync( $"Hash for {fileInfo.FullName}: {BitConverter.ToString( hashBytes )}" );

                        var sha1 = SHA1.Create();
                        byte[] computedHash = sha1?.ComputeHash( fileBytes );
                        await Logger.LogAsync(
                            $"Computed hash for {fileInfo.FullName}: {BitConverter.ToString( computedHash )}"
                        );

                        if ( computedHash.SequenceEqual( hashBytes ) )
                        {
                            checksums[fileInfo] = sha1;
                        }
                    }
                }
            }

            return checksums;
        }

        private static bool TryConvertHexStringToBytes( [NotNull] string hexString, [CanBeNull] out byte[] bytes )
        {
            if ( hexString == null )
                throw new ArgumentNullException( nameof( hexString ) );

            int numberChars = hexString.Length;
            if ( numberChars % 2 != 0 )
            {
                bytes = null;
                return false;
            }

            bytes = new byte[numberChars / 2];
            for ( int i = 0; i < numberChars; i += 2 )
            {
                if (
                    byte.TryParse(
                        hexString.Substring( i, length: 2 ),
                        NumberStyles.HexNumber,
                        provider: null,
                        out bytes[i / 2]
                    )
                )
                {
                    continue;
                }

                bytes = null;
                return false;
            }

            return true;
        }
    }
}
