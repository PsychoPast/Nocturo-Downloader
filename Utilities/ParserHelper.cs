using System;
using System.Runtime.CompilerServices;

namespace Nocturo.Downloader.Utilities
{
    internal static unsafe class ParserHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TValue FromStringBlob<TValue>(ReadOnlySpan<byte> input)
            where TValue : unmanaged
        {
            var bufferSize = sizeof(TValue);
            var buffer = stackalloc byte[bufferSize];
            AtoiStringBlob(input, buffer, bufferSize);
            return Util.ReinterpretCast<TValue>((nint)buffer); // converts the buffer pointed to by 'buffer' to the given numeric type
        }

        internal static string HashBlobToHexString(ReadOnlySpan<byte> input, int hashBufferSize, bool reversed)
        {
            var buffer = stackalloc byte[hashBufferSize];

            // since we need to store it in reverse (for chunk hashes) and keep a reference to the original value
            var bBuffer = reversed ? buffer + hashBufferSize - 1 : buffer;
            AtoiStringBlob(input, bBuffer, hashBufferSize, reversed);

            // hex conversion
            {
                var hashStringSize = hashBufferSize * 2;
                var cBuffer = stackalloc char[hashStringSize * sizeof(char)]; // 1 byte = 2 hex chars

                // to preserve cBuffer's initial value (else we have to sub (pHashBuffer * 2) from cBuffer in the string ctor)
                var pHashBuffer = cBuffer;
                var i = 1;
                do
                {
                    /*
                     * These 4 lines convert a byte into 2 hex chars. How does it work?
                     * Let's take 122 for example. Its hex representation is 7A
                     * To understand more, let's break 122 into its binary representation on 8 bit: 0111 1010
                     * If you're familiar with binary, you know that 0111 is 7 and 1010 is 10
                     * A nibble is 4 bit. rawByte (unsigned byte) will never be less than 0 (0000 0000) or exceed 255 (1111 1111) therefore we can safely assert that
                     * The 2 hex chars represent the 2 nibbles (higher and lower) of the byte. All we gonna do is filter them using bitwise operations
                     * >> is called right-shift operator. It shifts the bits inside the byte towards the right 4 times
                     * 0111 1010 >> 4 will give us 0000 0111 -> here we go we filtered the first nibble. Little-Endian 0000 0111 represents the decimal 7. We convert it to char, add it to the buffer then increment it
                     * next & 0x0F is called AND operator. It's a mask. 0x0F is 15 dec. 15 dec is 0000 1111 bin. (0111 1010 & 0000 1111) -> sets the higher nibble to 0 and keeps the lower one as it is
                     * 0000 1010 -> which gives us 10 in decimal. Hex is base 16 that means from 0 to 15. 0-9 are normal numbers and 10-15 are represented by the letters A-F. So 10 -> A. We put A in buffer, increment it then increment the byte array and start over
                     */
                    var rawByte = (byte)(*buffer >> 4);
                    *pHashBuffer++ = GetHexCharFromByte(rawByte, true);
                    rawByte = (byte)(*buffer++ & 0x0F);
                    *pHashBuffer++ = GetHexCharFromByte(rawByte, true);
                }
                while (i++ != hashBufferSize);

                return new string(cBuffer, 0, hashStringSize);
            }
        }

        // Ref: https://github.com/EpicGames/UnrealEngine/blob/7074f6c5957e245d18576013b95221a77dc9246f/Engine/Source/Runtime/Core/Private/Containers/String.cpp#L730
        private static void AtoiStringBlob(ReadOnlySpan<byte> input, byte* buffer, int size, bool reversed = false)
        {
            /*
             * Numbers are stored as string blobs in the manifest. This code basically does the same as UE4
             * Why unsafe? Well, creating arrays with *new* allocates them on the Heap. The Garbage Collector will then have to clean them when not reference anymore
             * Each chunk allocates 4 arrays (1 for offset, 1 for size and 2 for hash). There are around 40k chunks so 40k * 4 = 160,000 arrays allocated on the heap
             * So we decided to use *stackalloc* since we're dealing with small size arrays. stackalloc allocates the array directly on the method *stack* which is "destroyed" when the method returns
             * This way we're removing some stress off the GC.
             */
            var length = input.Length;
            if (size < length / 3 || length % 3 != 0)
                throw new ArgumentException("The buffer should be at least half the string size and the string should have an even number of chars.", nameof(input));

            const char zero = '0';
            fixed (byte* pInput = input)
            {
                var bInput = pInput;
                for (var i = 0; i < length; i += 3)
                {
                    var conv0 = (byte)((*bInput++ - zero) * 100);
                    var conv1 = (byte)((*bInput++ - zero) * 10);
                    var conv2 = (byte)(*bInput++ - zero);
                    // var conv3 = '0' always: ConvBuffer[3] = TEXT('\0');

                    var sum = (byte)(conv0 + conv1 + conv2);

                    // hashes are stored in reverse
                    if (reversed)
                        *buffer-- = sum;
                    else
                        *buffer++ = sum;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char GetHexCharFromByte(byte rawByte, bool upperCase)
        {
            var firstRef = upperCase ? 'A' : 'a';
            return rawByte switch
            {
                // since we're converting an int to its char representation, we simply add the rawByte value (0-9) and the char '0' (48)
                >= 0 and <= 9 => (char)(rawByte + '0'),

                //same thing as before however in that case we sub 10 from the result since A-F represents 10-15
                >= 10 and <= 15 => (char)(rawByte + firstRef - 10),

                // in that case rawByte is outside the valid range 0-15
                _ => throw new ArgumentOutOfRangeException(nameof(rawByte), "rawByte value must be between 0 and 15")
            };
        }
    }
}