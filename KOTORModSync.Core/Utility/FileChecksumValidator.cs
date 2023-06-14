// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
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

        public FileChecksumValidator( string destinationPath, Dictionary<FileInfo, SHA1> expectedChecksums, Dictionary<FileInfo, SHA1> originalChecksums )
        {
            _destinationPath = destinationPath;
            _expectedChecksums = expectedChecksums;
            _originalChecksums = originalChecksums;
        }

        public static string Sha1ToString( SHA1 sha1 ) => string.Concat( sha1.Hash.Select( b => b.ToString( "x2" ) ) );

        public static string StringToSha1( string s )
            => string.Concat( SHA1.Create().ComputeHash(
                Enumerable.Range( 0, s.Length )
                    .Where( x => x % 2 == 0 )
                    .Select( x => Convert.ToByte( s.Substring( x, 2 ), 16 ) )
                    .ToArray() ).Select( b => b.ToString( "x2" ) ) );

        public async Task<bool> ValidateChecksumsAsync()
        {
            var actualChecksums = new Dictionary<string, string>();

            foreach ( FileInfo fileInfo in _expectedChecksums
                         .Select( expectedChecksum => expectedChecksum.Key )
                         .Where( fileInfo => fileInfo.Exists )
                    )
            {
                SHA1 sha1 = await CalculateSha1Async( fileInfo );
                actualChecksums[fileInfo.Name] = BitConverter.ToString( sha1.Hash ).Replace( "-", "" );
            }

            bool allChecksumsMatch = actualChecksums.Count == _expectedChecksums.Count
                && actualChecksums
                    .All( x =>
                        _expectedChecksums.TryGetValue( new FileInfo( Path.Combine( _destinationPath, x.Key ) ), out SHA1 expectedSha1 )
                        && BitConverter.ToString( expectedSha1.Hash ).Replace( "-", "" ).Equals( x.Value, StringComparison.OrdinalIgnoreCase )
                    );

            if ( allChecksumsMatch ) return allChecksumsMatch;

            await Logger.LogAsync( "Checksum validation failed for the following files:" );
            bool thisMatch = true;
            foreach ( KeyValuePair<FileInfo, SHA1> expectedChecksum in _expectedChecksums )
            {
                FileInfo expectedFileInfo = expectedChecksum.Key;
                SHA1 expectedSha1 = expectedChecksum.Value;
                string expectedSha1String = BitConverter.ToString( expectedSha1.Hash ).Replace( "-", "" );

                if ( !actualChecksums.TryGetValue( expectedFileInfo.Name, out string actualSha1 ) )
                {
                    await Logger.LogAsync( $"Problem looking up sha1 of {expectedFileInfo.FullName} - expected: {expectedSha1String}" );
                    thisMatch = false;
                    continue;
                }

                if ( actualSha1.Equals( expectedSha1String, StringComparison.OrdinalIgnoreCase ) )
                {
                    thisMatch = false;
                    continue;
                }

                await Logger.LogAsync(
                    $"  {expectedFileInfo.FullName} - expected: {expectedSha1String}, actual: {actualSha1}" );
                thisMatch = false;
            }

            return allChecksumsMatch;
        }

        public static async Task<SHA1> CalculateSha1Async( FileInfo filePath )
        {
            var sha1 = SHA1.Create();
            using ( FileStream stream = File.OpenRead( filePath.FullName ) )
            {
                byte[] buffer = new byte[81920];
                var tasks = new List<Task>( 65535 );

                int bytesRead;
                long totalBytesRead = 0;

                while ( ( bytesRead = await stream.ReadAsync( buffer, 0, buffer.Length ) ) > 0 )
                {
                    totalBytesRead += bytesRead;

                    byte[] data = new byte[bytesRead];
                    Buffer.BlockCopy( buffer, 0, data, 0, bytesRead );

                    tasks.Add(
                        Task.Run( () => _ = sha1.TransformBlock(
                                     data,
                                     0,
                                     bytesRead,
                                     null,
                                     0
                                 )
                        )
                    );

                    if ( tasks.Count < Environment.ProcessorCount * 2 ) continue;

                    await Task.WhenAll( tasks );
                    tasks.Clear();
                }

                _ = sha1.TransformFinalBlock(
                    buffer,
                    0,
                    bytesRead // Use 'bytesRead' instead of 0
                );

                await Task.WhenAll( tasks );

                return sha1;
            }
        }

        public static async Task SaveChecksumsToFileAsync( string filePath, Dictionary<DirectoryInfo, SHA1> checksums )
        {
            string json = JsonConvert.SerializeObject( checksums );
            using ( var writer = new StreamWriter( filePath ) )
            {
                await writer.WriteAsync( json );
            }
        }

        public static async Task<
            Dictionary<FileInfo, SHA1>
        > LoadChecksumsFromFileAsync( FileInfo filePath )
        {
            if ( !File.Exists( filePath.FullName ) )
                return new Dictionary<FileInfo, SHA1>();

            var checksums = new Dictionary<FileInfo, SHA1>();

            using ( var reader = new StreamReader( filePath.FullName ) )
            {
                string line;
                while ( ( line = await reader.ReadLineAsync() ) != null )
                {
                    string[] parts = line.Split( ',' );
                    if ( parts.Length != 2 ) continue;

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
                        _ = await fileStream.ReadAsync( fileBytes, 0, fileBytes.Length );

                        if ( !TryConvertHexStringToBytes( hash, out byte[] hashBytes ) )
                        {
                            await Logger.LogAsync( $"Failed to convert hash string: {hash}" );
                            continue;
                        }

                        await Logger.LogAsync(
                            $"Hash for {fileInfo.FullName}: {BitConverter.ToString( hashBytes )}"
                        );

                        var sha1 = SHA1.Create();
                        byte[] computedHash = sha1.ComputeHash( fileBytes );
                        await Logger.LogAsync(
                            $"Computed hash for {fileInfo.FullName}: {BitConverter.ToString( computedHash )}"
                        );

                        if ( computedHash.SequenceEqual( hashBytes ) )
                            checksums[fileInfo] = sha1;
                    }
                }
            }

            return checksums;
        }

        private static bool TryConvertHexStringToBytes( string hexString, out byte[] bytes )
        {
            int numberChars = hexString.Length;
            if ( numberChars % 2 != 0 )
            {
                bytes = null;
                return false;
            }

            bytes = new byte[numberChars / 2];
            for ( int i = 0; i < numberChars; i += 2 )
            {
                if ( byte.TryParse(
                    hexString.Substring( i, 2 ),
                    NumberStyles.HexNumber,
                    null,
                    out bytes[i / 2]
                ) )
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
