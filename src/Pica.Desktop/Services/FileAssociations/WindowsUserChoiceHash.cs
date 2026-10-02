// SPDX-License-Identifier: MPL-2.0
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Pica.Desktop.Services.FileAssociations;

internal static class WindowsUserChoiceHash
{
    private const int WordSize = sizeof(uint);
    private const int WordsPerBlock = 2;
    private const string UserExperience =
        "User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804003BAFA0B}";

    private static readonly uint[][] FirstScrambleCoefficients =
    [
        new uint[] { 0xCF98B111, 0x87085B9F, 0x12CEB96D, 0x257E1D83 },
        new uint[] { 0xA27416F5, 0xD38396FF, 0x7C932B89, 0xBFA49F69 }
    ];
    private static readonly uint[][] SecondScrambleCoefficients =
    [
        new uint[] { 0xEF0569FB, 0x689B6B9F, 0x79F8A395, 0xC3EFEA97 },
        new uint[] { 0xC31713DB, 0xDDCD1F0F, 0x59C3AF2D, 0x35BD1EC9 }
    ];

    internal static string Calculate(string extension, string userSid, string programId, long fileTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(programId);
        long minute = fileTime - (fileTime % TimeSpan.TicksPerMinute);
        string text = $"{extension}{userSid}{programId}{minute.ToString("x16", CultureInfo.InvariantCulture)}{UserExperience}";
        byte[] input = Encoding.Unicode.GetBytes(text.ToLowerInvariant() + '\0');
        byte[] digest = MD5.HashData(input);
        uint[] multipliers =
        [
            BinaryPrimitives.ReadUInt32LittleEndian(digest) | 1u,
            BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(WordSize)) | 1u
        ];
        uint first = 0;
        uint second = 0;
        uint firstTotal = 0;
        uint secondTotal = 0;
        int blockCount = input.Length / (WordSize * WordsPerBlock);

        unchecked
        {
            for (int i = 0; i < blockCount; i++)
            {
                for (int j = 0; j < WordsPerBlock; j++)
                {
                    uint word = BinaryPrimitives.ReadUInt32LittleEndian(
                        input.AsSpan(((i * WordsPerBlock) + j) * WordSize));
                    uint[] firstCoefficients = FirstScrambleCoefficients[j];
                    uint[] secondCoefficients = SecondScrambleCoefficients[j];
                    first = (first + word) * multipliers[j];

                    foreach (uint coefficient in firstCoefficients)
                    {
                        first = SwapWords(first) * coefficient;
                    }

                    firstTotal += first;
                    second += word;
                    second = (SwapWords(second) * secondCoefficients[0]) + (second * multipliers[j]);
                    second = ((second >> 16) * secondCoefficients[1]) + (second * secondCoefficients[2]);
                    second = (SwapWords(second) * secondCoefficients[3]) + second;
                    secondTotal += second;
                }
            }
        }

        byte[] result = new byte[WordSize * WordsPerBlock];
        BinaryPrimitives.WriteUInt32LittleEndian(result, first ^ second);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(WordSize), firstTotal ^ secondTotal);

        return Convert.ToBase64String(result);
    }

    private static uint SwapWords(uint value)
    {
        return (value >> 16) | (value << 16);
    }
}
